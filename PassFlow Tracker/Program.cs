using Avalonia;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Infrastructure.Database;
using System;
using System.Threading.Tasks;

namespace PassFlow_Tracker
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static async Task Main(string[] args)
        {
            try
            {
                var db = new DbConnectionFactory();

                var dbInitializer = new DatabaseInitializer(db);
                await dbInitializer.StartAndInitializeAsync();

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка запуска: " + ex.Message);
            }
        }



        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
