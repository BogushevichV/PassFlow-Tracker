using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Npgsql;

public class DatabaseInitializer
{
    private const string ImageName = "postgres";
    private const string Tag = "latest";
    private const string ContainerName = "my_postgres";
    private const string Password = "mysecretpassword";
    private const string DbPort = "5532";

    // Connection string для подключения к уже запущенному контейнеру
    private const string ConnectionString = "Host=localhost;Port=5532;Username=postgres;Password=mysecretpassword;Database=postgres";

    public async Task StartAndInitializeAsync()
    {
        var client = new DockerClientConfiguration().CreateClient();

        // 1. Проверяем наличие образа и скачиваем, если его нет
        await EnsureImageExists(client);

        // 2. Создаем и запускаем контейнер
        await StartContainer(client);

        // 3. Ждем готовности БД (Postgres требуется время на старт)
        Console.WriteLine("Ожидание готовности базы данных...");
        await Task.Delay(5000);

        // 4. Инициализируем таблицы
        await CreateTablesAsync();

        Console.WriteLine("Инициализация успешно завершена!");
    }

    private async Task EnsureImageExists(DockerClient client)
    {
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = ImageName, Tag = Tag },
            null,
            new Progress<JSONMessage>(m => Console.WriteLine($"Download: {m.Status}")));
    }

    private async Task StartContainer(DockerClient client)
    {
        // Настройка портов (5532 на хосте -> 5432 в контейнере)
        var hostConfig = new HostConfig
        {
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                { "5432/tcp", new List<PortBinding> { new PortBinding { HostPort = DbPort } } }
            },
            Binds = new List<string> { "pgdata:/var/lib/postgresql/data" }
        };

        // Создание контейнера
        var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = $"{ImageName}:{Tag}",
            Name = ContainerName,
            Env = new List<string> { $"POSTGRES_PASSWORD={Password}" },
            HostConfig = hostConfig
        });

        // Запуск
        await client.Containers.StartContainerAsync(response.ID, null);
        Console.WriteLine($"Контейнер {ContainerName} запущен.");
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

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Таблицы и индексы созданы.");
    }
}