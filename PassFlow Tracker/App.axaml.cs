using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models.IPC;
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
        private IpcServer? _ipcServer;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            // --- »Õ»÷»¿À»«¿÷»ﬂ —≈–¬»—Œ¬ ---
            var db = new DbConnectionFactory();
            var json = new JsonImportService(db);
            var analytics = new TransportAnalytics(db);

            // --- IPC ---
            var dispatcher = new CommandDispatcher(json, analytics);
            _ipcServer = new IpcServer(dispatcher);

            _ = _ipcServer.StartAsync(); // Á‡ÔÛÒÍ‡ÂÏ ÒÂ‚Â ‚ ÙÓÌÂ

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow();
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