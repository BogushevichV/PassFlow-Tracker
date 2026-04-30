using Npgsql;
using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Application.Services
{
    public class TransportAnalytics
    {
        private readonly DbConnectionFactory _db;

        public TransportAnalytics(DbConnectionFactory db)
        {
            _db = db;
        }

        // --- Методы получения данных (Аналитический модуль) ---

        // 1. Определение часа пик (Гистограмма)
        public async Task<List<PeakHour>> GetPeakHoursAsync()
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

            return data;
        }

        // 2. Анализ загруженности остановок (Топ-10)
        public async Task<List<StopLoad>> GetTopStopsAsync(int limit = 10)
        {
            var data = new List<StopLoad>();
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            // Используем прямое подставление лимита, чтобы избежать проблем с параметрами,
            // которые вы видите в DBeaver. Значение приходит из кода (int), риск инъекции отсутствует.
            string sql = $@"
                SELECT stop_name, SUM(entered + exited) as total 
                FROM trip_stops 
                GROUP BY stop_name 
                ORDER BY total DESC 
                LIMIT {limit}";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                data.Add(new StopLoad(rdr["stop_name"].ToString(), Convert.ToInt64(rdr["total"])));

            return data;
        }

        // 3. Рейсы с низкой активностью (Настраиваемый порог)
        public async Task<List<LowTrip>> GetLowActivityTripsAsync(int threshold = 10)
        {
            var data = new List<LowTrip>();
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            // Аналогично убираем параметр и подставляем порог напрямую.
            // Дополнительно приводим время к часовому поясу Europe/Moscow,
            // чтобы в .NET оно отображалось так же, как в БД.
            string sql = $@"
                SELECT t.id,
                       t.time_from AT TIME ZONE 'Europe/Moscow' AS time_from_local,
                       t.transported,
                       dr.unit_name
                FROM trips t
                JOIN rounds r ON t.round_id = r.id
                JOIN daily_records dr ON r.daily_record_id = dr.id
                WHERE t.transported < {threshold}
                ORDER BY t.time_from DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                data.Add(new LowTrip(
                    (int)rdr["id"],
                    (DateTime)rdr["time_from_local"],
                    (int)rdr["transported"],
                    rdr["unit_name"].ToString()));

            return data;
        }

        // 4. Остановки сгруппированные (для вкладки trip_stops)
        public async Task<List<TripStopRow>> GetTripStopsAsync()
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

            return data;
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