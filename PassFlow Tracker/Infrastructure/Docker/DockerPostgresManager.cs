using Docker.DotNet;
using Docker.DotNet.Models;
using Npgsql;
using PassFlow_Tracker.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Infrastructure.Docker
{
    public class DockerPostgresManager
    {
        private readonly DockerClient _client;
        private readonly DockerSettings _settings;

        public DockerPostgresManager()
        {
            _client = new DockerClientConfiguration().CreateClient();
            _settings = AppConfig.Docker;
        }

        public async Task StartAsync()
        {
            await CheckDockerRunning();
            await EnsureImageExists();
            await StartContainer();
            await WaitForPostgres();
        }


        private async Task CheckDockerRunning()
        {
            Console.WriteLine("\t1. Проверка Docker daemon");
            try
            {
                await _client.System.PingAsync();
                Console.WriteLine("Docker daemon доступен.");
            }
            catch
            {
                Console.WriteLine("Docker daemon недоступен. Запустите Docker Desktop.");
                Environment.Exit(1);
            }
        }

        private async Task EnsureImageExists()
        {
            Console.WriteLine("\n\t2. Проверка наличия образа");

            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = _settings.ImageName, Tag = _settings.Tag },
                null,
                new Progress<JSONMessage>(m => Console.WriteLine($"Download: {m.Status}")));
        }

        private async Task StartContainer()
        {
            Console.WriteLine("\n\t3. Проверка наличия контейнера");

            var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });

            var existingContainer = containers
                .FirstOrDefault(c => c.Names.Any(n => n.Trim('/') == _settings.ContainerName));

            if (existingContainer != null)
            {
                Console.WriteLine("Контейнер уже существует.");

                if (existingContainer.State != "running")
                {
                    Console.WriteLine("Контейнер остановлен. Запуск...");
                    await _client.Containers.StartContainerAsync(existingContainer.ID, null);
                }
                else
                {
                    Console.WriteLine("Контейнер уже запущен.");
                }

                return;
            }

            Console.WriteLine("Создание нового контейнера...");

            var hostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                { "5432/tcp", new List<PortBinding> { new PortBinding { HostPort = _settings.DbPort } } }
            },
                Binds = new List<string> { "pgdata:/var/lib/postgresql/data" }
            };

            // Создание контейнера
            var response = await _client.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = $"{_settings.ImageName}:{_settings.Tag}",
                    Name = _settings.ContainerName,
                    Env = new List<string> { $"POSTGRES_PASSWORD={_settings.Password}" },
                    HostConfig = hostConfig
                });

            // Запуск
            await _client.Containers.StartContainerAsync(response.ID, null);
            Console.WriteLine($"Контейнер {_settings.ContainerName} запущен.");
        }

        private async Task WaitForPostgres(int maxRetries = 10, int delayMs = 2000)
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

    }
}
