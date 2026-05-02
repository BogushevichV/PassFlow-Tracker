using Docker.DotNet;
using Docker.DotNet.Models;
using Npgsql;
using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Docker;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DatabaseInitializer
{
    private const string LogContext = "DatabaseInitializer";

    private readonly DbConnectionFactory _db;

    public DatabaseInitializer(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task StartAndInitializeAsync()
    {
        AppLogger.Info($"[{LogContext}] Начало инициализации БД");

        try
        {
            var docker = new DockerPostgresManager();

            await docker.StartAsync();

            // 1. Проверяем существует ли БД
            await EnsureDatabaseExistsAsync();

            // 2. Инициализируем таблицы
            await CreateTablesAsync();

            AppLogger.Info($"[{LogContext}] Инициализация БД завершена успешно");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[{LogContext}] Ошибка инициализации БД", ex);
            throw;
        }
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        AppLogger.Info($"[{LogContext}] Проверка наличия БД");

        using var connection = _db.CreateAdminConnection();
        await connection.OpenAsync();

        string checkSql = "SELECT 1 FROM pg_database WHERE datname = 'passflowtrackerdb'";

        using var checkCmd = new NpgsqlCommand(checkSql, connection);
        var exists = await checkCmd.ExecuteScalarAsync();

        if (exists == null)
        {
            AppLogger.Info($"[{LogContext}] БД не найдена, создаём...");

            using var createCmd = new NpgsqlCommand(
                "CREATE DATABASE passflowtrackerdb",
                connection);

            await createCmd.ExecuteNonQueryAsync();

            AppLogger.Info($"[{LogContext}] БД создана");
        }
        else
        {
            AppLogger.Info($"[{LogContext}] БД уже существует");
        }
    }

    private async Task CreateTablesAsync()
    {
        AppLogger.Info($"[{LogContext}] Создание таблиц...");

        string sql = @"
            CREATE TABLE IF NOT EXISTS daily_records (
                id SERIAL PRIMARY KEY,
                unit_name VARCHAR(255) NOT NULL,
                record_date DATE NOT NULL,
                entered INT DEFAULT 0,
                exited INT DEFAULT 0,
                transported INT DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS rounds (
                id SERIAL PRIMARY KEY,
                daily_record_id INT NOT NULL REFERENCES daily_records(id) ON DELETE CASCADE,
                start_point VARCHAR(255),
                end_point VARCHAR(255),
                time_from TIMESTAMPTZ,
                time_to TIMESTAMPTZ,
                entered INT DEFAULT 0,
                exited INT DEFAULT 0,
                transported INT DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS trips (
                id SERIAL PRIMARY KEY,
                round_id INT NOT NULL REFERENCES rounds(id) ON DELETE CASCADE,
                start_point VARCHAR(255),
                end_point VARCHAR(255),
                time_from TIMESTAMPTZ,
                time_to TIMESTAMPTZ,
                entered INT DEFAULT 0,
                exited INT DEFAULT 0,
                transported INT DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS trip_stops (
                id SERIAL PRIMARY KEY,
                trip_id INT NOT NULL REFERENCES trips(id) ON DELETE CASCADE,
                stop_number INT,
                stop_name VARCHAR(255),
                is_duplicate BOOLEAN DEFAULT FALSE,
                is_skipped BOOLEAN DEFAULT FALSE,
                time_from TIMESTAMPTZ,
                time_to TIMESTAMPTZ,
                entered INT DEFAULT 0,
                exited INT DEFAULT 0,
                transported INT DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_daily_records_date ON daily_records(record_date);
            CREATE INDEX IF NOT EXISTS idx_trips_transported ON trips(transported);
            CREATE INDEX IF NOT EXISTS idx_trip_stops_name ON trip_stops(stop_name);
            CREATE INDEX IF NOT EXISTS idx_trip_stops_time_from ON trip_stops(time_from);";

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        AppLogger.Info($"[{LogContext}] Таблицы созданы успешно");
    }

    public async Task PrintAllTablesAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        string[] tables =
        {
            "daily_records",
            "rounds",
            "trips",
            "trip_stops"
        };

        foreach (var table in tables)
        {
            Console.WriteLine($"\n===== Содержимое таблицы {table} =====");

            using var command = new NpgsqlCommand($"SELECT * FROM {table}", connection);
            using var reader = await command.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                Console.WriteLine("Таблица пустая.");
            }
            else
            {
                while (await reader.ReadAsync())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        Console.Write($"{reader.GetName(i)}: {reader.GetValue(i)} | ");
                    }
                    Console.WriteLine();
                }
            }

            await reader.CloseAsync();
        }
    }
}