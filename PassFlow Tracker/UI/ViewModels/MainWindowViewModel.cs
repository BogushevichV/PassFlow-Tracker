using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Drawing.Charts;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public Window? MainWindow { get; set; }

        private readonly IpcClient _ipc = new();

        public ObservableCollection<TripStopRowViewModel> TripStops { get; } = new();

        private const string LogContext = "MainWindowViewModel";

        public MainWindowViewModel() { }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            AppLogger.Info($"[{LogContext}] Инициализация главного окна");
            await SetActiveTab("trip_stops");
            AppLogger.Info($"[{LogContext}] Главное окно инициализировано");
        }

        [ObservableProperty]
        private bool isTableView = true;

        public bool IsChartView => !IsTableView;

        partial void OnIsTableViewChanged(bool value)
        {
            OnPropertyChanged(nameof(IsChartView));
        }

        [ObservableProperty]
        private int topN = 10;

        [ObservableProperty]
        private int threshold = 10;

        [ObservableProperty]
        private string status = "Готов";


        [ObservableProperty]
        private string activeTab = "trip_stops";


        [ObservableProperty]
        private bool gradientActive = true;

        partial void OnGradientActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(GradientButtonLabel));
        }

        [ObservableProperty]
        private bool showColorMenu;

        [ObservableProperty]
        private string gradientDirection = "min-to-max";

        public string GradientButtonLabel =>
            GradientActive ? "Вкл. Градиент" : "Выкл. Градиент";


        [ObservableProperty]
        private bool showCalendar;

        [ObservableProperty]
        private string calendarMode = "days";

        partial void OnCalendarModeChanged(string value)
        {
            OnPropertyChanged(nameof(CalendarTitle));
        }

        [ObservableProperty]
        private int calendarYear = 2025;

        partial void OnCalendarYearChanged(int value)
        {
            OnPropertyChanged(nameof(CalendarTitle));
        }

        [ObservableProperty]
        private int calendarMonth = 6;

        partial void OnCalendarMonthChanged(int value)
        {
            OnPropertyChanged(nameof(CalendarTitle));
        }

        public string CalendarTitle
        {
            get
            {
                string[] months =
                {
                "Январь","Февраль","Март","Апрель","Май","Июнь",
                "Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"
            };

                if (CalendarMode == "days")
                    return $"{months[CalendarMonth - 1]} {CalendarYear}";

                if (CalendarMode == "months")
                    return $"{CalendarYear}";

                return $"{CalendarYear - 5} – {CalendarYear + 6}";
            }
        }


        [RelayCommand]
        private void SwitchToTable() => IsTableView = true;

        [RelayCommand]
        private void SwitchToChart() => IsTableView = false;

        [RelayCommand]
        private void ToggleColorMenu() => ShowColorMenu = !ShowColorMenu;

        [RelayCommand]
        private void ToggleGradient() => GradientActive = !GradientActive;

        [RelayCommand]
        private void ToggleCalendar() => ShowCalendar = !ShowCalendar;

        [RelayCommand]
        private void SetCalendarMode(string mode) => CalendarMode = mode;

        [RelayCommand]
        private async Task SetActiveTab(string tab)
        {
            AppLogger.Info($"[{LogContext}] Переключение на вкладку: {tab}");

            ActiveTab = tab;

            try
            {
                switch (tab)
                {
                    case "trip_stops":
                        await RunTopStops();
                        Status = "Загружены остановки";
                        break;
                    case "trips":
                        // TODO: Реализовать загрузку рейсов
                        TripStops.Clear();
                        Status = "Рейсы (в разработке)";
                        break;
                    case "rounds":
                        // TODO: Реализовать загрузку кругов
                        TripStops.Clear();
                        Status = "Круги (в разработке)";
                        break;
                    case "daily_records":
                        // TODO: Реализовать загрузку дней
                        TripStops.Clear();
                        Status = "Дни (в разработке)";
                        break;
                    case "all_data":
                        // TODO: Реализовать загрузку всех данных
                        TripStops.Clear();
                        Status = "Все данные (в разработке)";
                        break;
                }
                AppLogger.Info($"[{LogContext}] Вкладка '{tab}' загружена успешно");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки вкладки '{tab}'", ex);
                Status = $"Ошибка: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CalendarPrev()
        {
            if (CalendarMode == "days")
            {
                CalendarMonth--;
                if (CalendarMonth < 1)
                {
                    CalendarMonth = 12;
                    CalendarYear--;
                }
            }
            else if (CalendarMode == "months") CalendarYear--;
            else CalendarYear -= 12;
        }

        [RelayCommand]
        private void CalendarNext()
        {
            if (CalendarMode == "days")
            {
                CalendarMonth++;
                if (CalendarMonth > 12)
                {
                    CalendarMonth = 1;
                    CalendarYear++;
                }
            }
            else if (CalendarMode == "months") CalendarYear++;
            else CalendarYear += 12;
        }

        [RelayCommand]
        private async Task LoadJson()
        {
            if (MainWindow == null) return;

            var files = await MainWindow.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Выберите JSON",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"]
                    }
                    ]
                });

            var file = files.FirstOrDefault();
            if (file == null)
            {
                AppLogger.Warning($"[{LogContext}] Импорт JSON: файл не выбран");
                return;
            }

            AppLogger.Info($"[{LogContext}] Импорт JSON: {file.Path.LocalPath}");
            Status = "Импорт...";

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "import_json",
                Parameters = new()
                {
                    ["path"] = file.Path.LocalPath,
                }
            });

            if (response.Success)
            {
                AppLogger.Info($"[{LogContext}] JSON успешно импортирован");
            }
            else
            {
                AppLogger.Error($"[{LogContext}] Ошибка импорта JSON: {response.Message}");
            }

            Status = response.Message;
        }


        [RelayCommand]
        private async Task RunPeakHours()
        {
            AppLogger.Info($"[{LogContext}] Запрос: часы пик");
            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "peak_hours"
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<PeakHour>>(json);

                    TripStops.Clear();
                    foreach (var d in data)
                    {
                        TripStops.Add(new TripStopRowViewModel
                        {
                            StopName = $"Час {d.Hour}",
                            Transported = (int)d.Flow
                        });
                    }

                    Status = "Часы пик";
                    AppLogger.Info($"[{LogContext}] Часы пик загружены: {data.Count} записей");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки часов пик", ex);
                Status = "Ошибка загрузки";
            }
        }

        [RelayCommand]
        private async Task RunTopStops()
        {
            AppLogger.Info($"[{LogContext}] Запрос: топ-{TopN} остановок");

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "top_stops",
                    Parameters = new()
                    {
                        ["limit"] = TopN.ToString()
                    }
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<StopLoad>>(json);

                    TripStops.Clear();

                    foreach (var d in data)
                    {
                        TripStops.Add(new TripStopRowViewModel
                        {
                            StopName = d.Name,
                            Transported = (int)d.Load
                        });
                    }
                    Status = $"Топ-{TopN} остановок";
                    AppLogger.Info($"[{LogContext}] Топ-{TopN} остановок загружен: {data.Count} записей");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки топ остановок", ex);
                Status = "Ошибка загрузки";
            }
        }

        [RelayCommand]
        private async Task RunLowActivity()
        {
            AppLogger.Info($"[{LogContext}] Запрос: низкая активность (порог={Threshold})");

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "low_activity",
                    Parameters = new()
                    {
                        ["threshold"] = Threshold.ToString()
                    }
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<LowTrip>>(json);

                    TripStops.Clear();
                    foreach (var d in data)
                    {
                        TripStops.Add(new TripStopRowViewModel
                        {
                            StopName = $"Рейс {d.Id}",
                            Transported = d.Count
                        });
                    }

                    Status = "Низкая активность";
                    AppLogger.Info($"[{LogContext}] Низкая активность загружена: {data.Count} записей");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки низкой активности", ex);
                Status = "Ошибка загрузки";
            }
        }

    }
}