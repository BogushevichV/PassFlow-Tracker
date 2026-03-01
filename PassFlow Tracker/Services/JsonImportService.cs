using Npgsql;
using PassFlow_Tracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Services
{
    public class JsonImportService
    {
        private const string ConnectionString =
            "Host=localhost;Port=5532;Username=postgres;Password=mysecretpassword;Database=postgres";

        public async Task ImportAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Файл не найден.");
                return;
            }

            string json = await File.ReadAllTextAsync(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var records = JsonSerializer.Deserialize<List<RootRecord>>(json, options);

            if (records == null)
            {
                Console.WriteLine("Ошибка десериализации JSON.");
                return;
            }

            foreach (var record in records)
            {
                await SaveRecordAsync(record);
            }

            Console.WriteLine("Импорт завершён.");
        }

        private async Task SaveRecordAsync(RootRecord data)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
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
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
