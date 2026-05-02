using Microsoft.Extensions.Configuration;
using System;

namespace PassFlow_Tracker.Configuration
{
    public static class AppConfig
    {
        private static IConfigurationRoot? _config;

        public static IConfigurationRoot Config
        {
            get
            {
                if (_config == null)
                {
                    _config = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();
                }

                return _config;
            }
        }

        public static DockerSettings Docker => Config.GetSection("Docker").Get<DockerSettings>()!;

        public static IpcSettings Ipc => Config.GetSection("Ipc").Get<IpcSettings>()!;
        
        public static string MainConnection => Config.GetConnectionString("MainConnection")!;

        public static string AdminConnection => Config.GetConnectionString("AdminConnection")!;
    }
}
