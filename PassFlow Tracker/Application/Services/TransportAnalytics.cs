using DocumentFormat.OpenXml.Drawing;
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

        // 3. Рейсы с низкой активностью (Настраиваемый порог)
        public async Task<List<LowTrip>> GetLowActivityTripsAsync(int threshold = 10)
        {
            AppLogger.Info($"[{LogContext}] Запрос рейсов с порогом < {threshold}");
            var startTime = DateTime.Now;

            try
            {
                var data = new List<LowTrip>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                // Аналогично убираем параметр и подставляем порог напрямую.
                // Дополнительно приводим время к часовому поясу Europe/Moscow,
                // чтобы в .NET оно отображалось так же, как в БД.
                string sql = @"
                SELECT t.id,
                       t.time_from AT TIME ZONE 'Europe/Moscow' AS time_from_local,
                       t.transported,
                       dr.unit_name
                FROM trips t
                JOIN rounds r ON t.round_id = r.id
                JOIN daily_records dr ON r.daily_record_id = dr.id
                WHERE t.transported < @threshold
                ORDER BY t.time_from DESC";

                using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@threshold", threshold);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new LowTrip(
                        (int)rdr["id"],
                        (DateTime)rdr["time_from_local"],
                        (int)rdr["transported"],
                        rdr["unit_name"].ToString()));

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
        public async Task<List<TripStopRow>> GetTripStopsAsync()
        {
            AppLogger.Info($"[{LogContext}] Запрос остановок");
            var startTime = DateTime.Now;

            try
            {
                var data = new List<TripStopRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                // entered, exited, transported — суммируем по всем проходам через остановку
                const string sql = @"
                SELECT stop_number, stop_name,
                       SUM(entered)     AS total_entered,
                       SUM(exited)      AS total_exited,
                       SUM(transported) AS total_transported
                FROM trip_stops
                WHERE is_duplicate = FALSE
                GROUP BY stop_number, stop_name
                ORDER BY stop_number";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new TripStopRow(
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

        // 5. Дни (daily_records)
        public async Task<List<DailyRecordRow>> GetDailyRecordsAsync()
        {
            var data = new List<DailyRecordRow>();
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT unit_name, record_date,
                       entered, exited, transported
                FROM daily_records
                ORDER BY record_date DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                data.Add(new DailyRecordRow(
                    rdr["unit_name"].ToString() ?? "",
                    ((DateOnly)rdr["record_date"]).ToString("dd.MM.yyyy"),
                    Convert.ToInt32(rdr["entered"]),
                    Convert.ToInt32(rdr["exited"]),
                    Convert.ToInt32(rdr["transported"])
                ));

            return data;
        }

        // 6. Круги с номером автобуса
        public async Task<List<RoundRow>> GetRoundsAsync()
        {
            AppLogger.Info($"[{LogContext}] Запрос кругов");
            var startTime = DateTime.Now;
            try
            {
                var data = new List<RoundRow>();
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                const string sql = @"
                SELECT dr.unit_name,
                       r.start_point, r.end_point,
                       r.time_from AT TIME ZONE 'Europe/Moscow' AS tf,
                       r.time_to   AT TIME ZONE 'Europe/Moscow' AS tt,
                       r.entered, r.exited, r.transported
                FROM rounds r
                JOIN daily_records dr ON r.daily_record_id = dr.id
                ORDER BY r.time_from";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new RoundRow(
                        rdr["unit_name"].ToString() ?? "",
                        rdr["start_point"].ToString() ?? "",
                        rdr["end_point"].ToString() ?? "",
                        ((DateTime)rdr["tf"]).ToString("HH:mm"),
                        ((DateTime)rdr["tt"]).ToString("HH:mm"),
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
        public async Task<List<TripRow>> GetTripsAsync()
        {
            AppLogger.Info($"[{LogContext}] Запрос рейсов");
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
                JOIN rounds r  ON t.round_id = r.id
                JOIN daily_records dr ON r.daily_record_id = dr.id
                ORDER BY t.time_from";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    data.Add(new TripRow(
                        rdr["unit_name"].ToString() ?? "",
                        rdr["start_point"].ToString() ?? "",
                        rdr["end_point"].ToString() ?? "",
                        ((DateTime)rdr["tf"]).ToString("HH:mm"),
                        ((DateTime)rdr["tt"]).ToString("HH:mm"),
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
        public async Task<List<AllDataDayDto>> GetAllDataAsync()
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            // Загружаем все данные одним запросом с JOIN
            const string sql = @"
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
                JOIN trip_stops ts ON ts.trip_id = t.id
                ORDER BY dr.record_date DESC, r.time_from, t.time_from, ts.stop_number";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();

            var days   = new Dictionary<int, AllDataDayDto>();
            var rounds = new Dictionary<int, AllDataRoundDto>();
            var trips  = new Dictionary<int, AllDataTripDto>();

            while (await rdr.ReadAsync())
            {
                int drId = Convert.ToInt32(rdr["dr_id"]);
                int rId  = Convert.ToInt32(rdr["r_id"]);
                int tId  = Convert.ToInt32(rdr["t_id"]);

                if (!days.TryGetValue(drId, out var day))
                {
                    day = new AllDataDayDto
                    {
                        UnitName    = rdr["unit_name"].ToString() ?? "",
                        RecordDate  = ((DateOnly)rdr["record_date"]).ToString("dd.MM.yyyy"),
                        Entered     = Convert.ToInt32(rdr["dr_entered"]),
                        Exited      = Convert.ToInt32(rdr["dr_exited"]),
                        Transported = Convert.ToInt32(rdr["dr_transported"])
                    };
                    days[drId] = day;
                }

                if (!rounds.TryGetValue(rId, out var round))
                {
                    round = new AllDataRoundDto
                    {
                        StartPoint  = rdr["r_start"].ToString() ?? "",
                        EndPoint    = rdr["r_end"].ToString() ?? "",
                        TimeFrom    = ((DateTime)rdr["r_tf"]).ToString("HH:mm"),
                        TimeTo      = ((DateTime)rdr["r_tt"]).ToString("HH:mm"),
                        Entered     = Convert.ToInt32(rdr["r_entered"]),
                        Exited      = Convert.ToInt32(rdr["r_exited"]),
                        Transported = Convert.ToInt32(rdr["r_transported"])
                    };
                    rounds[rId] = round;
                    day.Rounds.Add(round);
                }

                if (!trips.TryGetValue(tId, out var trip))
                {
                    trip = new AllDataTripDto
                    {
                        StartPoint  = rdr["t_start"].ToString() ?? "",
                        EndPoint    = rdr["t_end"].ToString() ?? "",
                        TimeFrom    = ((DateTime)rdr["t_tf"]).ToString("HH:mm"),
                        TimeTo      = ((DateTime)rdr["t_tt"]).ToString("HH:mm"),
                        Entered     = Convert.ToInt32(rdr["t_entered"]),
                        Exited      = Convert.ToInt32(rdr["t_exited"]),
                        Transported = Convert.ToInt32(rdr["t_transported"])
                    };
                    trips[tId] = trip;
                    round.Trips.Add(trip);
                }

                trip.Stops.Add(new AllDataStopDto
                {
                    StopNumber  = Convert.ToInt32(rdr["stop_number"]),
                    StopName    = rdr["stop_name"].ToString() ?? "",
                    IsDuplicate = (bool)rdr["is_duplicate"],
                    IsSkipped   = (bool)rdr["is_skipped"],
                    TimeFrom    = ((DateTime)rdr["ts_tf"]).ToString("HH:mm"),
                    TimeTo      = ((DateTime)rdr["ts_tt"]).ToString("HH:mm"),
                    Entered     = Convert.ToInt32(rdr["ts_entered"]),
                    Exited      = Convert.ToInt32(rdr["ts_exited"]),
                    Transported = Convert.ToInt32(rdr["ts_transported"])
                });
            }

            return new List<AllDataDayDto>(days.Values);
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
            lows.ForEach(l => Console.WriteLine($"- Рейс #{l.Id} ({l.Unit}): {l.Count} чел. в {l.Time:HH:mm}"));
        }
    }
}