using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.UI.ViewModels;
using PassFlow_Tracker.UI.Views;
using System;
using System.IO;
using System.Linq;

namespace PassFlow_Tracker
{
    public partial class App : Avalonia.Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            var db = new DbConnectionFactory();

            var dbInitializer = new DatabaseInitializer(db);
            await dbInitializer.StartAndInitializeAsync();

            Console.WriteLine("¬ведите путь к JSON файлу:");
            string? path = Console.ReadLine();

            var analytics = new TransportAnalytics(db);

            if (!string.IsNullOrWhiteSpace(path))
            {
                var importer = new JsonImportService(db);
                await importer.ImportAsync(path);
            }

            await analytics.PrintReportAsync();

            await dbInitializer.PrintAllTablesAsync();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}