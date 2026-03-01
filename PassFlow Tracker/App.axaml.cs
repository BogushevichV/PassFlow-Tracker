using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PassFlow_Tracker.Services;
using PassFlow_Tracker.ViewModels;
using PassFlow_Tracker.Views;
using System;
using System.IO;
using System.Linq;

namespace PassFlow_Tracker
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            var dbInitializer = new DatabaseInitializer();
            await dbInitializer.StartAndInitializeAsync();

            Console.WriteLine("¬ведите путь к JSON файлу:");
            string? path = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(path))
            {
                var importer = new JsonImportService();
                await importer.ImportAsync(path);
            }

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