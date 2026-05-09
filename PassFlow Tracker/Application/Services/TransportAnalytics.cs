using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Presentation;
using Npgsql;
using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Application.Services
{
    public class TransportAnalytics
    {
        private const string LogContext = "TransportAnalytics";

        private readonly DbConnectionFactory _db;

        public TransportAnalytics(DbConnectionFactory db)
        {
            _db = db;
        }

        // --- Методы получения данных (Аналитический модуль) ---

        // 1. Определение часа пик (Гистограмма)
        public async Task<List<PeakHour>> GetPeakHoursAsync()
        {
            AppLogger.Info($"[{LogContext}] Запрос часов пик");
            var startTime = DateTime.Now;
            try
            {
                var data = new List<PeakHour>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                // Извлекаем ЧАС в нужном часовом поясе и считаем сумму вход+выход.
                // AT TIME ZONE 'Europe/Moscow' убирает сдвиг к UTC, который вы видите как "лондонское" время.
                const string sql = @"
                SELECT EXTRACT(HOUR FROM time_from AT TIME ZONE 'Europe/Moscow') as hr,
                       SUM(entered + exited) as flow
                FROM trip_stops
                GROUP BY hr ORDER BY flow DESC";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new PeakHour(Convert.ToInt32(rdr["hr"]), Convert.ToInt64(rdr["flow"])));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Часы пик получены за {duration:F0}мс, записей: {data.Count}");

                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения часов пик", ex);
                throw;
            }
        }

        // 2. Анализ загруженности остановок (Топ-10)
        public async Task<List<StopLoad>> GetTopStopsAsync(int limit = 10)
        {
            AppLogger.Info($"[{LogContext}] Запрос топ-{limit} остановок");
            var startTime = DateTime.Now;

            try
            {
                var data = new List<StopLoad>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                // Используем прямое подставление лимита, чтобы избежать проблем с параметрами,
                // которые вы видите в DBeaver. Значение приходит из кода (int), риск инъекции отсутствует.
                const string sql = @"
                    SELECT stop_name, SUM(entered + exited) as total 
                    FROM trip_stops 
                    GROUP BY stop_name 
                    ORDER BY total DESC 
                    LIMIT @limit";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new StopLoad(rdr["stop_name"].ToString(), Convert.ToInt64(rdr["total"])));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Топ-{limit} остановок получен за {duration:F0}мс, записей: {data.Count}");
                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения топ остановок", ex);
                throw;
            }
        }

        // 3. Рейсы с низкой активностью (фильтр по entered)
        public async Task<List<TripRow>> GetLowActivityTripsAsync(int threshold = 10)
        {
            AppLogger.Info($"[{LogContext}] Запрос рейсов с entered < {threshold}");
            var startTime = DateTime.Now;

            try
            {
                var data = new List<TripRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                const string sql = @"
                SELECT dr.unit_name,
                       t.start_point, t.end_point,
                       t.time_from AT TIME ZONE 'Europe/Moscow' AS tf,
                       t.time_to   AT TIME ZONE 'Europe/Moscow' AS tt,
                       t.entered, t.exited, t.transported
                FROM trips t
                JOIN rounds r ON t.round_id = r.id
                JOIN daily_records dr ON r.daily_record_id = dr.id
                WHERE t.entered < @threshold
                ORDER BY t.time_from DESC";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@threshold", threshold);

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new TripRow(
                        0,
                        rdr["unit_name"].ToString() ?? "",
                        rdr["start_point"].ToString() ?? "",
                        rdr["end_point"].ToString() ?? "",
                        ((DateTime)rdr["tf"]).ToString("dd.MM.yyyy HH:mm"),
                        ((DateTime)rdr["tt"]).ToString("dd.MM.yyyy HH:mm"),
                        Convert.ToInt32(rdr["entered"]),
                        Convert.ToInt32(rdr["exited"]),
                        Convert.ToInt32(rdr["transported"])
                    ));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Рейсы получены за {duration:F0}мс, записей: {data.Count}");
                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения рейсов с низкой активностью", ex);
                throw;
            }
        }

        // 4. Остановки сгруппированные (для вкладки trip_stops)
        public async Task<List<TripStopRow>> GetTripStopsAsync(List<int>? dailyRecordIds = null)
        {
            AppLogger.Info($"[{LogContext}] Запрос остановок");
            var startTime = DateTime.Now;

            try
            {
                var data = new List<TripStopRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                string sql = @"
                SELECT MIN(ts.id) AS id,
                       ts.stop_number, ts.stop_name,
                       SUM(ts.entered)     AS total_entered,
                       SUM(ts.exited)      AS total_exited,
                       SUM(ts.transported) AS total_transported
                FROM trip_stops ts
                JOIN trips t ON ts.trip_id = t.id
                JOIN rounds r ON t.round_id = r.id
                WHERE ts.is_duplicate = FALSE";

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                    sql += " AND r.daily_record_id = ANY(@ids)";

                sql += " GROUP BY ts.stop_number, ts.stop_name ORDER BY ts.stop_number";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                    cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new TripStopRow(
                        Convert.ToInt32(rdr["id"]),
                        Convert.ToInt32(rdr["stop_number"]),
                        rdr["stop_name"].ToString() ?? "",
                        Convert.ToInt32(rdr["total_entered"]),
                        Convert.ToInt32(rdr["total_exited"]),
                        Convert.ToInt32(rdr["total_transported"])
                    ));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Остановки получены за {duration:F0}мс, записей: {data.Count}");
                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения остановок", ex);
                throw;
            }
        }

        // 4b. Топ-N остановок с режимом агрегации
        public async Task<List<TopStopRow>> GetTopStopsDetailedAsync(
            int limit, TopStopsMode mode, List<int>? dailyRecordIds = null)
        {
            AppLogger.Info($"[{LogContext}] Топ-{limit} остановок, режим={mode}");
            var startTime = DateTime.Now;

            try
            {
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                string sql;
                NpgsqlCommand cmd;

                switch (mode)
                {
                    // Каждая запись trip_stop отдельно
                    case TopStopsMode.PerRecord:
                        sql = @"
                        SELECT ts.stop_number, ts.stop_name,
                               ts.entered, ts.exited, ts.transported,
                               ts.time_from AT TIME ZONE 'Europe/Moscow' AS ts_tf,
                               ts.time_to   AT TIME ZONE 'Europe/Moscow' AS ts_tt
                        FROM trip_stops ts
                        JOIN trips t ON ts.trip_id = t.id
                        JOIN rounds r ON t.round_id = r.id
                        WHERE ts.is_duplicate = FALSE";
                        if (dailyRecordIds?.Count > 0) sql += " AND r.daily_record_id = ANY(@ids)";
                        sql += @"
                        ORDER BY (ts.entered + ts.exited) DESC
                        LIMIT @limit";
                        cmd = new NpgsqlCommand(sql, conn);
                        if (dailyRecordIds?.Count > 0)
                            cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                        cmd.Parameters.AddWithValue("@limit", limit);
                        break;

                    // Суммировать по остановке за каждый день
                    case TopStopsMode.PerDay:
                        sql = @"
                        SELECT ts.stop_number, ts.stop_name,
                               dr.record_date,
                               SUM(ts.entered)     AS total_entered,
                               SUM(ts.exited)      AS total_exited,
                               SUM(ts.transported) AS total_transported
                        FROM trip_stops ts
                        JOIN trips t ON ts.trip_id = t.id
                        JOIN rounds r ON t.round_id = r.id
                        JOIN daily_records dr ON r.daily_record_id = dr.id
                        WHERE ts.is_duplicate = FALSE";
                        if (dailyRecordIds?.Count > 0) sql += " AND dr.id = ANY(@ids)";
                        sql += @"
                        GROUP BY ts.stop_number, ts.stop_name, dr.record_date
                        ORDER BY (SUM(ts.entered) + SUM(ts.exited)) DESC
                        LIMIT @limit";
                        cmd = new NpgsqlCommand(sql, conn);
                        if (dailyRecordIds?.Count > 0)
                            cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                        cmd.Parameters.AddWithValue("@limit", limit);
                        break;

                    // Суммировать по остановке за всё время
                    default: // AllTime
                        sql = @"
                        SELECT ts.stop_number, ts.stop_name,
                               MIN(dr.record_date) AS date_from,
                               MAX(dr.record_date) AS date_to,
                               SUM(ts.entered)     AS total_entered,
                               SUM(ts.exited)      AS total_exited,
                               SUM(ts.transported) AS total_transported
                        FROM trip_stops ts
                        JOIN trips t ON ts.trip_id = t.id
                        JOIN rounds r ON t.round_id = r.id
                        JOIN daily_records dr ON r.daily_record_id = dr.id
                        WHERE ts.is_duplicate = FALSE";
                        if (dailyRecordIds?.Count > 0) sql += " AND dr.id = ANY(@ids)";
                        sql += @"
                        GROUP BY ts.stop_number, ts.stop_name
                        ORDER BY (SUM(ts.entered) + SUM(ts.exited)) DESC
                        LIMIT @limit";
                        cmd = new NpgsqlCommand(sql, conn);
                        if (dailyRecordIds?.Count > 0)
                            cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                        cmd.Parameters.AddWithValue("@limit", limit);
                        break;
                }

                var data = new List<TopStopRow>();
                using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int num  = Convert.ToInt32(rdr["stop_number"]);
                    string name = rdr["stop_name"].ToString() ?? "";

                    int entered, exited, transported;
                    string label;

                    if (mode == TopStopsMode.PerRecord)
                    {
                        entered     = Convert.ToInt32(rdr["entered"]);
                        exited      = Convert.ToInt32(rdr["exited"]);
                        transported = Convert.ToInt32(rdr["transported"]);
                        label = $"{name}  {((DateTime)rdr["ts_tf"]).ToString("dd.MM.yyyy HH:mm")}–{((DateTime)rdr["ts_tt"]).ToString("HH:mm")}";
                    }
                    else if (mode == TopStopsMode.PerDay)
                    {
                        entered     = Convert.ToInt32(rdr["total_entered"]);
                        exited      = Convert.ToInt32(rdr["total_exited"]);
                        transported = Convert.ToInt32(rdr["total_transported"]);
                        label = $"{name}  {((DateOnly)rdr["record_date"]).ToString("dd.MM.yyyy")}";
                    }
                    else // AllTime
                    {
                        entered     = Convert.ToInt32(rdr["total_entered"]);
                        exited      = Convert.ToInt32(rdr["total_exited"]);
                        transported = Convert.ToInt32(rdr["total_transported"]);
                        label = $"{name}  {((DateOnly)rdr["date_from"]).ToString("dd.MM.yyyy")}–{((DateOnly)rdr["date_to"]).ToString("dd.MM.yyyy")}";
                    }

                    data.Add(new TopStopRow(0, num, name, label, entered, exited, transported));
                }

                cmd.Dispose();
                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Топ-{limit} получен за {duration:F0}мс, записей: {data.Count}");
                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения топ остановок (детально)", ex);
                throw;
            }
        }

        // 5. Дни (daily_records)
        public async Task<List<DailyRecordRow>> GetDailyRecordsAsync(List<int>? ids = null)
        {
            var data = new List<DailyRecordRow>();
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT id, unit_name, record_date, entered, exited, transported
                FROM daily_records";

            if (ids != null && ids.Count > 0)
            {
                sql += " WHERE id = ANY(@ids)";
            }

            sql += " ORDER BY record_date DESC";

            using var cmd = new NpgsqlCommand(sql, conn);

            if (ids != null && ids.Count > 0)
            {
                cmd.Parameters.AddWithValue("@ids", ids.ToArray());
            }
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                data.Add(new DailyRecordRow(
                    Convert.ToInt32(rdr["id"]),
                    rdr["unit_name"].ToString() ?? "",
                    ((DateOnly)rdr["record_date"]).ToString("dd.MM.yyyy"),
                    Convert.ToInt32(rdr["entered"]),
                    Convert.ToInt32(rdr["exited"]),
                    Convert.ToInt32(rdr["transported"])
                ));

            return data;
        }

        // 6. Круги с номером автобуса
        public async Task<List<RoundRow>> GetRoundsAsync(List<int>? dailyRecordIds = null)
        {
            AppLogger.Info($"[{LogContext}] Запрос кругов");
            var startTime = DateTime.Now;
            try
            {
                var data = new List<RoundRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                var sql = @"
                    SELECT dr.id,
                           dr.unit_name,
                           r.start_point, r.end_point,
                           r.time_from AT TIME ZONE 'Europe/Moscow' AS tf,
                           r.time_to   AT TIME ZONE 'Europe/Moscow' AS tt,
                           r.entered, r.exited, r.transported
                    FROM rounds r
                    JOIN daily_records dr ON r.daily_record_id = dr.id";

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    sql += " WHERE dr.id = ANY(@ids)";
                }

                sql += " ORDER BY r.time_from";

                using var cmd = new NpgsqlCommand(sql, conn);

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                }
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new RoundRow(
                        Convert.ToInt32(rdr["id"]),
                        rdr["unit_name"].ToString() ?? "",
                        rdr["start_point"].ToString() ?? "",
                        rdr["end_point"].ToString() ?? "",
                        ((DateTime)rdr["tf"]).ToString("dd.MM.yyyy HH:mm"),
                        ((DateTime)rdr["tt"]).ToString("dd.MM.yyyy HH:mm"),
                        Convert.ToInt32(rdr["entered"]),
                        Convert.ToInt32(rdr["exited"]),
                        Convert.ToInt32(rdr["transported"])
                    ));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Круги получены за {duration:F0}мс, записей: {data.Count}");

                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения кругов", ex);
                throw;
            }
        }

        // 6. Рейсы с номером автобуса
        public async Task<List<TripRow>> GetTripsAsync(List<int>? dailyRecordIds = null)
        {
            AppLogger.Info($"[{LogContext}] Запрос рейсов");
            var startTime = DateTime.Now;
            try
            {
                var data = new List<TripRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                var sql = @"
                    SELECT t.id,
                           dr.unit_name,
                           t.start_point, t.end_point,
                           t.time_from AT TIME ZONE 'Europe/Moscow' AS tf,
                           t.time_to   AT TIME ZONE 'Europe/Moscow' AS tt,
                           t.entered, t.exited, t.transported
                    FROM trips t
                    JOIN rounds r ON t.round_id = r.id
                    JOIN daily_records dr ON r.daily_record_id = dr.id";

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    sql += " WHERE dr.id = ANY(@ids)";
                }

                sql += " ORDER BY t.time_from";

                using var cmd = new NpgsqlCommand(sql, conn);

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                }
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new TripRow(
                        Convert.ToInt32(rdr["id"]),
                        rdr["unit_name"].ToString() ?? "",
                        rdr["start_point"].ToString() ?? "",
                        rdr["end_point"].ToString() ?? "",
                        ((DateTime)rdr["tf"]).ToString("dd.MM.yyyy HH:mm"),
                        ((DateTime)rdr["tt"]).ToString("dd.MM.yyyy HH:mm"),
                        Convert.ToInt32(rdr["entered"]),
                        Convert.ToInt32(rdr["exited"]),
                        Convert.ToInt32(rdr["transported"])
                    ));

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Рейсы получены за {duration:F0}мс, записей: {data.Count}");

                return data;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения рейсов", ex);
                throw;
            }
        }

        // 8. Все данные иерархически (для вкладки all_data)
        public async Task<List<AllDataDayDto>> GetAllDataAsync(List<int>? dailyRecordIds = null)
        {
            AppLogger.Info($"[{LogContext}] Запрос всех данных");
            var startTime = DateTime.Now;
            try
            {
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                var sql = @"
                SELECT
                    dr.id        AS dr_id,
                    dr.unit_name, dr.record_date,
                    dr.entered   AS dr_entered,
                    dr.exited    AS dr_exited,
                    dr.transported AS dr_transported,

                    r.id         AS r_id,
                    r.start_point AS r_start, r.end_point AS r_end,
                    r.time_from AT TIME ZONE 'Europe/Moscow' AS r_tf,
                    r.time_to   AT TIME ZONE 'Europe/Moscow' AS r_tt,
                    r.entered   AS r_entered,
                    r.exited    AS r_exited,
                    r.transported AS r_transported,

                    t.id         AS t_id,
                    t.start_point AS t_start, t.end_point AS t_end,
                    t.time_from AT TIME ZONE 'Europe/Moscow' AS t_tf,
                    t.time_to   AT TIME ZONE 'Europe/Moscow' AS t_tt,
                    t.entered   AS t_entered,
                    t.exited    AS t_exited,
                    t.transported AS t_transported,

                    ts.stop_number, ts.stop_name,
                    ts.is_duplicate, ts.is_skipped,
                    ts.time_from AT TIME ZONE 'Europe/Moscow' AS ts_tf,
                    ts.time_to   AT TIME ZONE 'Europe/Moscow' AS ts_tt,
                    ts.entered  AS ts_entered,
                    ts.exited   AS ts_exited,
                    ts.transported AS ts_transported

                FROM daily_records dr
                JOIN rounds r  ON r.daily_record_id = dr.id
                JOIN trips  t  ON t.round_id = r.id
                JOIN trip_stops ts ON ts.trip_id = t.id";

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    sql += " WHERE dr.id = ANY(@ids)";
                }

                sql += " ORDER BY dr.record_date DESC, r.time_from, t.time_from, ts.stop_number";

                using var cmd = new NpgsqlCommand(sql, conn);

                if (dailyRecordIds != null && dailyRecordIds.Count > 0)
                {
                    cmd.Parameters.AddWithValue("@ids", dailyRecordIds.ToArray());
                }

                using var rdr = await cmd.ExecuteReaderAsync();

                var days = new Dictionary<int, AllDataDayDto>();
                var rounds = new Dictionary<int, AllDataRoundDto>();
                var trips = new Dictionary<int, AllDataTripDto>();

                while (await rdr.ReadAsync())
                {
                    int drId = Convert.ToInt32(rdr["dr_id"]);
                    int rId = Convert.ToInt32(rdr["r_id"]);
                    int tId = Convert.ToInt32(rdr["t_id"]);

                    if (!days.TryGetValue(drId, out var day))
                    {
                        day = new AllDataDayDto
                        {
                            UnitName = rdr["unit_name"].ToString() ?? "",
                            RecordDate = ((DateOnly)rdr["record_date"]).ToString("dd.MM.yyyy"),
                            Entered = Convert.ToInt32(rdr["dr_entered"]),
                            Exited = Convert.ToInt32(rdr["dr_exited"]),
                            Transported = Convert.ToInt32(rdr["dr_transported"])
                        };
                        days[drId] = day;
                    }

                    if (!rounds.TryGetValue(rId, out var round))
                    {
                        round = new AllDataRoundDto
                        {
                            StartPoint = rdr["r_start"].ToString() ?? "",
                            EndPoint = rdr["r_end"].ToString() ?? "",
                            TimeFrom = ((DateTime)rdr["r_tf"]).ToString("dd.MM.yyyy HH:mm"),
                            TimeTo = ((DateTime)rdr["r_tt"]).ToString("dd.MM.yyyy HH:mm"),
                            Entered = Convert.ToInt32(rdr["r_entered"]),
                            Exited = Convert.ToInt32(rdr["r_exited"]),
                            Transported = Convert.ToInt32(rdr["r_transported"])
                        };
                        rounds[rId] = round;
                        day.Rounds.Add(round);
                    }

                    if (!trips.TryGetValue(tId, out var trip))
                    {
                        trip = new AllDataTripDto
                        {
                            StartPoint = rdr["t_start"].ToString() ?? "",
                            EndPoint = rdr["t_end"].ToString() ?? "",
                            TimeFrom = ((DateTime)rdr["t_tf"]).ToString("dd.MM.yyyy HH:mm"),
                            TimeTo = ((DateTime)rdr["t_tt"]).ToString("dd.MM.yyyy HH:mm"),
                            Entered = Convert.ToInt32(rdr["t_entered"]),
                            Exited = Convert.ToInt32(rdr["t_exited"]),
                            Transported = Convert.ToInt32(rdr["t_transported"])
                        };
                        trips[tId] = trip;
                        round.Trips.Add(trip);
                    }

                    trip.Stops.Add(new AllDataStopDto
                    {
                        StopNumber = Convert.ToInt32(rdr["stop_number"]),
                        StopName = rdr["stop_name"].ToString() ?? "",
                        IsDuplicate = (bool)rdr["is_duplicate"],
                        IsSkipped = (bool)rdr["is_skipped"],
                        TimeFrom = ((DateTime)rdr["ts_tf"]).ToString("dd.MM.yyyy HH:mm"),
                        TimeTo = ((DateTime)rdr["ts_tt"]).ToString("dd.MM.yyyy HH:mm"),
                        Entered = Convert.ToInt32(rdr["ts_entered"]),
                        Exited = Convert.ToInt32(rdr["ts_exited"]),
                        Transported = Convert.ToInt32(rdr["ts_transported"])
                    });
                }

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Все данные получены за {duration:F0}мс, записей: {days.Count}");

                return new List<AllDataDayDto>(days.Values);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка получения всех данных", ex);
                throw;
            }
        }

        public async Task UpdateTripStopsAsync(List<TripStopUpdateDto> stops)
        {
            
            AppLogger.Info($"[{LogContext}] Обновление остановок");
            var startTime = DateTime.Now;

            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                foreach (var stop in stops)
                {
                    const string sql = @"
                        UPDATE trip_stops
                        SET stop_number    = @num,
                            stop_name      = @name,
                            entered        = @entered,
                            exited         = @exited,
                            transported    = @transported
                        WHERE id = @id";


                    //time_from = @from::timestamptz,
                    //time_to = @to::timestamptz,

                    using var cmd = new NpgsqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", stop.Id);
                    cmd.Parameters.AddWithValue("@num", stop.StopNumber);

                    cmd.Parameters.AddWithValue("@name", stop.StopName);
                    //cmd.Parameters.AddWithValue("@from", DateTime.Parse(stop.TimeFrom));
                    //cmd.Parameters.AddWithValue("@to", DateTime.Parse(stop.TimeTo));
                    cmd.Parameters.AddWithValue("@entered", stop.Entered);
                    cmd.Parameters.AddWithValue("@exited", stop.Exited);
                    cmd.Parameters.AddWithValue("@transported", stop.Transported);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Остановки обновлены за {duration:F0}мс, записей: {stops.Count}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.Error($"[{LogContext}] Ошибка обновления остановок", ex);
                throw;
            }
        }

        public async Task UpdateTripsAsync(List<TripUpdateDto> trips)
        {
            AppLogger.Info($"[{LogContext}] Обновление рейсов");
            var startTime = DateTime.Now;

            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                foreach (var trip in trips)
                {
                    const string sql = @"
                        UPDATE trips
                        SET start_point   = @start,
                            end_point     = @end,
                            time_from      = @from::timestamptz,
                            time_to        = @to::timestamptz,
                            entered       = @entered,
                            exited        = @exited,
                            transported   = @transported
                        WHERE id = @id";

                    using var cmd = new NpgsqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", trip.Id);
                    cmd.Parameters.AddWithValue("@start", trip.StartPoint);
                    cmd.Parameters.AddWithValue("@end", trip.EndPoint);
                    cmd.Parameters.AddWithValue("@from", DateTime.Parse(trip.TimeFrom));
                    cmd.Parameters.AddWithValue("@to", DateTime.Parse(trip.TimeTo));
                    cmd.Parameters.AddWithValue("@entered", trip.Entered);
                    cmd.Parameters.AddWithValue("@exited", trip.Exited);
                    cmd.Parameters.AddWithValue("@transported", trip.Transported);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Рейсы обновлены за {duration:F0}мс, записей: {trips.Count}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.Error($"[{LogContext}] Ошибка обновления рейсов", ex);
                throw;
            }
        }

        public async Task UpdateRoundsAsync(List<RoundUpdateDto> rounds)
        {
            AppLogger.Info($"[{LogContext}] Обновление кругов");
            var startTime = DateTime.Now;

            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                foreach (var round in rounds)
                {
                    const string sql = @"
                        UPDATE rounds
                        SET start_point   = @start,
                            end_point     = @end,
                            time_from      = @from::timestamptz,
                            time_to        = @to::timestamptz,
                            entered       = @entered,
                            exited        = @exited,
                            transported   = @transported
                        WHERE id = @id";

                    using var cmd = new NpgsqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", round.Id);
                    cmd.Parameters.AddWithValue("@start", round.StartPoint);
                    cmd.Parameters.AddWithValue("@end", round.EndPoint);
                    cmd.Parameters.AddWithValue("@from", DateTime.Parse(round.TimeFrom));
                    cmd.Parameters.AddWithValue("@to", DateTime.Parse(round.TimeTo));
                    cmd.Parameters.AddWithValue("@entered", round.Entered);
                    cmd.Parameters.AddWithValue("@exited", round.Exited);
                    cmd.Parameters.AddWithValue("@transported", round.Transported);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Круги обновлены за {duration:F0}мс, записей: {rounds.Count}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.Error($"[{LogContext}] Ошибка обновления кругов ", ex);
                throw;
            }
        }

        public async Task UpdateDailyRecordsAsync(List<DailyRecordUpdateDto> records)
        {
            AppLogger.Info($"[{LogContext}] Обновление дней");
            var startTime = DateTime.Now;

            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                foreach (var record in records)
                {
                    const string sql = @"
                        UPDATE daily_records
                        SET unit_name     = @unit,
                            record_date   = @date::date,
                            entered       = @entered,
                            exited        = @exited,
                            transported   = @transported
                        WHERE id = @id";

                    using var cmd = new NpgsqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", record.Id);
                    cmd.Parameters.AddWithValue("@unit", record.UnitName);
                    cmd.Parameters.AddWithValue("@date", DateTime.Parse(record.RecordDate));
                    cmd.Parameters.AddWithValue("@entered", record.Entered);
                    cmd.Parameters.AddWithValue("@exited", record.Exited);
                    cmd.Parameters.AddWithValue("@transported", record.Transported);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Дни обновлены за {duration:F0}мс, записей: {records.Count}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.Error($"[{LogContext}] Ошибка обновления дней ", ex);
                throw;
            }
        }

        public async Task PrintReportAsync()
        {
            Console.WriteLine("=== ОТЧЕТ ПО ПАССАЖИРОПОТОКУ ===");

            var peaks = await GetPeakHoursAsync();
            Console.WriteLine("\n[Час Пик]:");
            if (peaks.Count > 0)
                Console.WriteLine($"Самое загруженное время: {peaks[0].Hour}:00 (Поток: {peaks[0].Flow} чел.)");

            var stops = await GetTopStopsAsync(5);
            Console.WriteLine("\n[Топ-5 Остановок]:");
            stops.ForEach(s => Console.WriteLine($"- {s.Name}: {s.Load} чел."));

            var lows = await GetLowActivityTripsAsync(10);
            Console.WriteLine("\n[Низкая активность (<10)]: ");
            lows.ForEach(l => Console.WriteLine($"- {l.UnitName} {l.StartPoint}→{l.EndPoint}: вошло {l.Entered} в {l.TimeFrom}"));
        }
    }
}