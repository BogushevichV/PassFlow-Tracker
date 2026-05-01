using Npgsql;
using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Application.Services
{
    public class JsonImportService
    {
        private const string LogContext = "JsonImportService";

        private readonly DbConnectionFactory _db;

        public JsonImportService(DbConnectionFactory db)
        {
            _db = db;
        }

        public async Task ImportAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AppLogger.Warning($"[{LogContext}] Файл не найден: {filePath}");
                Console.WriteLine("Файл не найден.");
                return;
            }

            AppLogger.Info($"[{LogContext}] Начало импорта: {filePath}");
            var startTime = DateTime.Now;

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                AppLogger.Info($"[{LogContext}] Файл прочитан: {json.Length} байт");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var records = JsonSerializer.Deserialize<List<RootRecord>>(json, options);

                if (records == null)
                {
                    AppLogger.Error($"[{LogContext}] Ошибка десериализации JSON");
                    Console.WriteLine("Ошибка десериализации JSON.");
                    return;
                }

                AppLogger.Info($"[{LogContext}] Десериализовано записей: {records.Count}");

                int successCount = 0;
                int errorCount = 0;

                foreach (var record in records)
                {
                    try
                    {
                        await SaveRecordAsync(record);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        AppLogger.Error($"[{LogContext}] Ошибка сохранения записи '{record.Unit}' от {record.Date}", ex);
                    }
                }

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Импорт завершён за {duration:F0}мс. Успешно: {successCount}, ошибок: {errorCount}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Критическая ошибка импорта", ex);
                throw;
            }
        }


        private async Task SaveRecordAsync(RootRecord data)
        {
            AppLogger.Info($"[{LogContext}] Сохранение записи: {data.Unit}, {data.Date}");

            await using var connection = _db.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var dailyCmd = new NpgsqlCommand(@"
            INSERT INTO daily_records (unit_name, record_date, entered, exited, transported)
            VALUES (@unit, @date, @entered, @exited, @transported)
            RETURNING id;", connection);

                dailyCmd.Parameters.AddWithValue("@unit", data.Unit);
                dailyCmd.Parameters.AddWithValue("@date", DateTime.Parse(data.Date));
                dailyCmd.Parameters.AddWithValue("@entered", data.Count.Entered);
                dailyCmd.Parameters.AddWithValue("@exited", data.Count.Exited);
                dailyCmd.Parameters.AddWithValue("@transported", data.Count.Transported);

                int dailyId = (int)(await dailyCmd.ExecuteScalarAsync())!;

                foreach (var round in data.Rounds)
                {
                    var roundCmd = new NpgsqlCommand(@"
                INSERT INTO rounds (daily_record_id, start_point, end_point, time_from, time_to, entered, exited, transported)
                VALUES (@dailyId, @start, @end, @from, @to, @entered, @exited, @transported)
                RETURNING id;", connection);

                    roundCmd.Parameters.AddWithValue("@dailyId", dailyId);
                    roundCmd.Parameters.AddWithValue("@start", round.Start);
                    roundCmd.Parameters.AddWithValue("@end", round.End);
                    roundCmd.Parameters.AddWithValue("@from", DateTime.Parse(round.TimeFrom));
                    roundCmd.Parameters.AddWithValue("@to", DateTime.Parse(round.TimeTo));
                    roundCmd.Parameters.AddWithValue("@entered", round.Count.Entered);
                    roundCmd.Parameters.AddWithValue("@exited", round.Count.Exited);
                    roundCmd.Parameters.AddWithValue("@transported", round.Count.Transported);

                    int roundId = (int)(await roundCmd.ExecuteScalarAsync())!;

                    foreach (var trip in round.Trips)
                    {
                        var tripCmd = new NpgsqlCommand(@"
                    INSERT INTO trips (round_id, start_point, end_point, time_from, time_to, entered, exited, transported)
                    VALUES (@roundId, @start, @end, @from, @to, @entered, @exited, @transported)
                    RETURNING id;", connection);

                        tripCmd.Parameters.AddWithValue("@roundId", roundId);
                        tripCmd.Parameters.AddWithValue("@start", trip.Start);
                        tripCmd.Parameters.AddWithValue("@end", trip.End);
                        tripCmd.Parameters.AddWithValue("@from", DateTime.Parse(trip.TimeFrom));
                        tripCmd.Parameters.AddWithValue("@to", DateTime.Parse(trip.TimeTo));
                        tripCmd.Parameters.AddWithValue("@entered", trip.Count.Entered);
                        tripCmd.Parameters.AddWithValue("@exited", trip.Count.Exited);
                        tripCmd.Parameters.AddWithValue("@transported", trip.Count.Transported);

                        int tripId = (int)(await tripCmd.ExecuteScalarAsync())!;

                        foreach (var stop in trip.Stops)
                        {
                            var stopCmd = new NpgsqlCommand(@"
                        INSERT INTO trip_stops (trip_id, stop_number, stop_name, is_duplicate, is_skipped, time_from, time_to, entered, exited, transported)
                        VALUES (@tripId, @num, @name, @dup, @skip, @from, @to, @entered, @exited, @transported);", connection);

                            stopCmd.Parameters.AddWithValue("@tripId", tripId);
                            stopCmd.Parameters.AddWithValue("@num", stop.Id);
                            stopCmd.Parameters.AddWithValue("@name", stop.Name);
                            stopCmd.Parameters.AddWithValue("@dup", stop.Duplicate);
                            stopCmd.Parameters.AddWithValue("@skip", stop.Skipped);
                            stopCmd.Parameters.AddWithValue("@from", DateTime.Parse(stop.TimeFrom));
                            stopCmd.Parameters.AddWithValue("@to", DateTime.Parse(stop.TimeTo));
                            stopCmd.Parameters.AddWithValue("@entered", stop.Count.Entered);
                            stopCmd.Parameters.AddWithValue("@exited", stop.Count.Exited);
                            stopCmd.Parameters.AddWithValue("@transported", stop.Count.Transported);

                            await stopCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                await transaction.CommitAsync();

                AppLogger.Info($"[{LogContext}] Запись '{data.Unit}' от {data.Date} сохранена успешно");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка сохранения записи '{data.Unit}' от {data.Date}", ex);
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
