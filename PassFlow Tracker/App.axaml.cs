using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Logging;
using PassFlow_Tracker.UI.ViewModels;
using PassFlow_Tracker.UI.Views;
using System;
using System.IO;
using System.Linq;

namespace PassFlow_Tracker
{
    public partial class App : Avalonia.Application
    {
        private IpcHost? _IpcHost;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            try
            {
                AppLogger.Info("Запуск приложения");

                var db = new DbConnectionFactory();
                var json = new JsonImportService(db);
                var analytics = new TransportAnalytics(db);

                AppLogger.Info("Сервисы инициализированы");

                var dispatcher = new CommandDispatcher(json, analytics);
                _IpcHost = new IpcHost(dispatcher);

                _ = _IpcHost.StartAsync();

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                    // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins

                    desktop.Exit += (s, e) =>
                    {
                        AppLogger.Info("Приложение завершается, останавливаем IPC-хост...");
                        _IpcHost?.Stop();
                        AppLogger.Info("IPC-хост остановлен");
                    };

                    DisableAvaloniaDataAnnotationValidation();
                    desktop.MainWindow = new MainWindow();
                }

                AppLogger.Info("Приложение успешно запущено");
                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Критическая ошибка при запуске приложения", ex);
                throw;
            }
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