using Docker.DotNet;
using Docker.DotNet.Models;
using Npgsql;
using PassFlow_Tracker.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DatabaseInitializer
{
    private readonly DockerSettings _settings = AppConfig.Docker;

    public async Task StartAndInitializeAsync()
    {
        var client = new DockerClientConfiguration().CreateClient();

        // 1. Проверяем запущен ли Docker
        await CheckDockerRunningAsync(client);

        // 2. Проверяем наличие образа и скачиваем, если его нет
        await EnsureImageExists(client);

        // 3. Создаем и запускаем контейнер
        await StartContainer(client);

        // 4. Ждем готовности БД
        await WaitForPostgresAsync();

        // 5. Проверяем существует ли БД
        await EnsureDatabaseExistsAsync();

        // 6. Инициализируем таблицы
        await CreateTablesAsync();

        Console.WriteLine("\nИнициализация успешно завершена!\n");
    }

    private async Task CheckDockerRunningAsync(DockerClient client)
    {
        Console.WriteLine("\t1. Проверка Docker daemon");
        try
        {
            await client.System.PingAsync();
            Console.WriteLine("Docker daemon доступен.");
        }
        catch
        {
            Console.WriteLine("Docker daemon недоступен. Запустите Docker Desktop.");
            Environment.Exit(1);
        }
    }

    private async Task EnsureImageExists(DockerClient client)
    {
        Console.WriteLine("\n\t2. Проверка наличия образа");

        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = _settings.ImageName, Tag = _settings.Tag },
            null,
            new Progress<JSONMessage>(m => Console.WriteLine($"Download: {m.Status}")));
    }

    private async Task StartContainer(DockerClient client)
    {
        Console.WriteLine("\n\t3. Проверка наличия контейнера");

        var containers = await client.Containers.ListContainersAsync(
        new ContainersListParameters { All = true });

        var existingContainer = containers
            .FirstOrDefault(c => c.Names.Any(n => n.Trim('/') == _settings.ContainerName));

        if (existingContainer != null)
        {
            Console.WriteLine("Контейнер уже существует.");

            if (existingContainer.State != "running")
            {
                Console.WriteLine("Контейнер остановлен. Запуск...");
                await client.Containers.StartContainerAsync(existingContainer.ID, null);
            }
            else
            {
                Console.WriteLine("Контейнер уже запущен.");
            }

            return;
        }

        Console.WriteLine("Создание нового контейнера...");

        // Настройка портов (5532 на хосте -> 5432 в контейнере)
        var hostConfig = new HostConfig
        {
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                { "5432/tcp", new List<PortBinding> { new PortBinding { HostPort = _settings.DbPort } } }
            },
            Binds = new List<string> { "pgdata:/var/lib/postgresql/data" }
        };

        // Создание контейнера
        var response = await client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = $"{_settings.ImageName}:{_settings.Tag}",
                Name = _settings.ContainerName,
                Env = new List<string> { $"POSTGRES_PASSWORD={_settings.Password}" },
                HostConfig = hostConfig
            });

        // Запуск
        await client.Containers.StartContainerAsync(response.ID, null);
        Console.WriteLine($"Контейнер {_settings.ContainerName} запущен.");
    }

    private async Task WaitForPostgresAsync(int maxRetries = 10, int delayMs = 2000)
    {
        Console.WriteLine("\n\t4. Проверка готовности БД\n");
        Console.WriteLine("Ожидание готовности базы данных PostgreSQL...");

        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                using var connection = new NpgsqlConnection(AppConfig.AdminConnection);
                await connection.OpenAsync();

                Console.WriteLine("PostgreSQL готов к работе.");
                return;
            }
            catch
            {
                Console.WriteLine($"Попытка {i}/{maxRetries}: PostgreSQL ещё запускается...");
                await Task.Delay(delayMs);
            }
        }

        Console.WriteLine("Ошибка: PostgreSQL не запустился.");
        Environment.Exit(1);
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        Console.WriteLine("\n\t5. Проверка наличия БД");

        using var connection = new NpgsqlConnection(AppConfig.AdminConnection);
        await connection.OpenAsync();

        string checkSql = "SELECT 1 FROM pg_database WHERE datname = 'passflowtrackerdb'";

        using var checkCmd = new NpgsqlCommand(checkSql, connection);
        var exists = await checkCmd.ExecuteScalarAsync();

        if (exists == null)
        {
            Console.WriteLine("База данных passflowtrackerdb не найдена. Создаём...");

            using var createCmd = new NpgsqlCommand(
                "CREATE DATABASE passflowtrackerdb",
                connection);

            await createCmd.ExecuteNonQueryAsync();

            Console.WriteLine("База данных создана.\n");
        }
        else
        {
            Console.WriteLine("База данных уже существует.\n");
        }
    }

    private async Task CreateTablesAsync()
    {
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

        using var connection = new NpgsqlConnection(AppConfig.MainConnection);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Таблицы и индексы созданы.");
    }

    public async Task PrintAllTablesAsync()
    {
        using var connection = new NpgsqlConnection(AppConfig.MainConnection);
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