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
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using JsonSerializerDefaults = PassFlow_Tracker.Infrastructure.Serialization.JsonSerializerDefaults;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public Window? MainWindow { get; set; }

        private readonly IpcClient _ipc = new();

        public ObservableCollection<TripStopRowViewModel> TripStops { get; } = new();
        public ObservableCollection<RoundRowViewModel> Rounds { get; } = new();
        public ObservableCollection<TripRowViewModel> Trips { get; } = new();
        public ObservableCollection<DailyRecordRowViewModel> DailyRecords { get; } = new();
        public ObservableCollection<DayNodeViewModel> AllDataTree { get; } = new();

        public bool ShowTripStops    => ActiveTab == "trip_stops";
        public bool ShowRounds       => ActiveTab == "rounds";
        public bool ShowTrips        => ActiveTab == "trips";
        public bool ShowDailyRecords => ActiveTab == "daily_records";
        public bool ShowAllData      => ActiveTab == "all_data";

        partial void OnActiveTabChanged(string value)
        {
            OnPropertyChanged(nameof(ShowTripStops));
            OnPropertyChanged(nameof(ShowRounds));
            OnPropertyChanged(nameof(ShowTrips));
            OnPropertyChanged(nameof(ShowDailyRecords));
            OnPropertyChanged(nameof(ShowAllData));
        }

        private const string LogContext = "MainWindowViewModel";

        public MainWindowViewModel() { }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            AppLogger.Info($"[{LogContext}] Инициализация главного окна");
            await LoadTripStops();
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
                        await LoadTripStops();
                        break;
                    case "trips":
                        await LoadTrips();
                        break;
                    case "rounds":
                        await LoadRounds();
                        break;
                    case "daily_records":
                        await LoadDailyRecords();
                        break;
                    case "all_data":
                        await LoadAllData();
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

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "import_json",
                    Parameters = new()
                    {
                        ["path"] = file.Path.LocalPath,
                    }
                });

                if (response.Success && response.Data != null)
                {
                    var idsJson = JsonSerializer.Serialize(response.Data);
                    var importedIds = JsonSerializer.Deserialize<List<int>>(idsJson);

                    if (importedIds != null && importedIds.Count > 0)
                    {
                        await LoadImportedDataForCurrentTab(importedIds);
                        Status = $"Импортировано: {importedIds.Count} записей";
                        AppLogger.Info($"[{LogContext}] JSON импортирован: {importedIds.Count} записей");
                    }
                    else
                    {
                        Status = "Нет данных для импорта";
                    }
                }
                else
                {
                    Status = $"Ошибка: {response.Message}";
                    AppLogger.Error($"[{LogContext}] Ошибка импорта JSON: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Ошибка импорта", ex);
            } 
        }
        
        [RelayCommand]
        private async Task RunPeakHours()
        {
            AppLogger.Info($"[{LogContext}] Запрос: часы пик");

            ActiveTab = "trip_stops";

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "peak_hours"
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<PeakHour>>(json, JsonSerializerDefaults.SafeOptions);

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

            ActiveTab = "trip_stops";

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
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<StopLoad>>(json, JsonSerializerDefaults.SafeOptions);

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

            ActiveTab = "trips";

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
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<LowTrip>>(json, JsonSerializerDefaults.SafeOptions);

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

        [RelayCommand]
        private async Task LoadTripStops()
        {
            AppLogger.Info($"[{LogContext}] Загрузка остановок");
            Status = "Загрузка остановок...";

            try
            {
                await LoadDataToCollection(
                command: "trip_stops",
                idsJson: null,
                onSuccess: json =>
                {
                    var data = JsonSerializer.Deserialize<List<TripStopRow>>(json);
                    TripStops.Clear();
                    data?.ForEach(d => TripStops.Add(new TripStopRowViewModel
                    {
                        StopNumber = d.StopNumber,
                        StopName = d.StopName,
                        Entered = d.Entered,
                        Exited = d.Exited,
                        Transported = d.Transported
                    }));
                    Status = $"Остановки: {data?.Count ?? 0}";
                },
                errorMessage: "Ошибка загрузки остановок");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке остановок", ex);
            }
        }

        private async Task LoadAllData()
        {
            AppLogger.Info($"[{LogContext}] Загрузка всех данных (дерево)");
            Status = "Загрузка всех данных...";
            try
            {
                var response = await _ipc.SendAsync(new IpcRequest { Command = "all_data" });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<AllDataDayDto>>(json, JsonSerializerDefaults.SafeOptions);

                    AllDataTree.Clear();
                    if (data != null)
                        foreach (var day in data)
                        {
                            var dayNode = new DayNodeViewModel
                            {
                                UnitName    = day.UnitName,
                                RecordDate  = day.RecordDate,
                                Entered     = day.Entered,
                                Exited      = day.Exited,
                                Transported = day.Transported
                            };
                            foreach (var round in day.Rounds)
                            {
                                var roundNode = new RoundNodeViewModel
                                {
                                    StartPoint  = round.StartPoint,
                                    EndPoint    = round.EndPoint,
                                    TimeFrom    = round.TimeFrom,
                                    TimeTo      = round.TimeTo,
                                    Entered     = round.Entered,
                                    Exited      = round.Exited,
                                    Transported = round.Transported
                                };
                                foreach (var trip in round.Trips)
                                {
                                    var tripNode = new TripNodeViewModel
                                    {
                                        StartPoint  = trip.StartPoint,
                                        EndPoint    = trip.EndPoint,
                                        TimeFrom    = trip.TimeFrom,
                                        TimeTo      = trip.TimeTo,
                                        Entered     = trip.Entered,
                                        Exited      = trip.Exited,
                                        Transported = trip.Transported
                                    };
                                    foreach (var stop in trip.Stops)
                                        tripNode.Stops.Add(new StopNodeViewModel
                                        {
                                            StopNumber  = stop.StopNumber,
                                            StopName    = stop.StopName,
                                            IsDuplicate = stop.IsDuplicate,
                                            IsSkipped   = stop.IsSkipped,
                                            TimeFrom    = stop.TimeFrom,
                                            TimeTo      = stop.TimeTo,
                                            Entered     = stop.Entered,
                                            Exited      = stop.Exited,
                                            Transported = stop.Transported
                                        });
                                    roundNode.Trips.Add(tripNode);
                                }
                                dayNode.Rounds.Add(roundNode);
                            }
                            AllDataTree.Add(dayNode);
                        }

                    Status = $"Все данные: {data?.Count ?? 0} дней";
                    AppLogger.Info($"[{LogContext}] Дерево загружено: {data?.Count ?? 0} дней");
                }
                else
                {
                    Status = $"Ошибка: {response.Message}";
                    AppLogger.Error($"[{LogContext}] Ошибка загрузки дерева: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке дерева", ex);
            }
        }

        private async Task LoadDailyRecords()        {
            AppLogger.Info($"[{LogContext}] Загрузка дней");
            Status = "Загрузка дней...";
            try
            {
                await LoadDataToCollection(
                command: "daily_records",
                idsJson: null,
                onSuccess: json =>
                {
                    var data = JsonSerializer.Deserialize<List<DailyRecordRow>>(json);
                    DailyRecords.Clear();
                    data?.ForEach(d => DailyRecords.Add(new DailyRecordRowViewModel
                    {
                        UnitName = d.UnitName,
                        RecordDate = d.RecordDate,
                        Entered = d.Entered,
                        Exited = d.Exited,
                        Transported = d.Transported
                    }));
                    Status = $"Дни: {data?.Count ?? 0}";
                },
                errorMessage: "Ошибка загрузки дней");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке дней", ex);
            }
        }

        private async Task LoadRounds()        
        {
            AppLogger.Info($"[{LogContext}] Загрузка кругов");
            Status = "Загрузка кругов...";

            try
            {
                await LoadDataToCollection(
                command: "rounds",
                idsJson: null,
                onSuccess: json =>
                {
                    var data = JsonSerializer.Deserialize<List<RoundRow>>(json);
                    Rounds.Clear();
                    data?.ForEach(d => Rounds.Add(new RoundRowViewModel
                    {
                        UnitName = d.UnitName,
                        StartPoint = d.StartPoint,
                        EndPoint = d.EndPoint,
                        TimeFrom = d.TimeFrom,
                        TimeTo = d.TimeTo,
                        Entered = d.Entered,
                        Exited = d.Exited,
                        Transported = d.Transported
                    }));
                    Status = $"Круги: {data?.Count ?? 0}";
                },
                errorMessage: "Ошибка загрузки кругов");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке кругов", ex);
            }
        }

        [RelayCommand]
        private async Task LoadTrips()
        {
            AppLogger.Info($"[{LogContext}] Загрузка рейсов");
            Status = "Загрузка рейсов...";

            try
            {
                await LoadDataToCollection(
                command: "trips",
                idsJson: null,
                onSuccess: json =>
                {
                    var data = JsonSerializer.Deserialize<List<TripRow>>(json);
                    Trips.Clear();
                    data?.ForEach(d => Trips.Add(new TripRowViewModel
                    {
                        UnitName = d.UnitName,
                        StartPoint = d.StartPoint,
                        EndPoint = d.EndPoint,
                        TimeFrom = d.TimeFrom,
                        TimeTo = d.TimeTo,
                        Entered = d.Entered,
                        Exited = d.Exited,
                        Transported = d.Transported
                    }));
                    Status = $"Рейсы: {data?.Count ?? 0}";
                },
                errorMessage: "Ошибка загрузки рейсов");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке рейсов", ex);
            }
        }

        private async Task LoadImportedDataForCurrentTab(List<int> dailyRecordIds)
        {
            var idsJson = JsonSerializer.Serialize(dailyRecordIds);

            try
            {
                switch (ActiveTab)
                {
                    case "trip_stops":
                    case "all_data":
                        await LoadDataToCollection(
                            command: "trip_stops",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<TripStopRow>>(json);
                                TripStops.Clear();
                                data?.ForEach(d => TripStops.Add(new TripStopRowViewModel
                                {
                                    StopNumber = d.StopNumber,
                                    StopName = d.StopName,
                                    Entered = d.Entered,
                                    Exited = d.Exited,
                                    Transported = d.Transported
                                }));
                                Status = $"Импортировано остановок: {data?.Count ?? 0}";
                            },
                            errorMessage: "Ошибка загрузки импортированных остановок");
                        break;

                    case "trips":
                        await LoadDataToCollection(
                            command: "trips",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<TripRow>>(json);
                                Trips.Clear();
                                data?.ForEach(d => Trips.Add(new TripRowViewModel
                                {
                                    UnitName = d.UnitName,
                                    StartPoint = d.StartPoint,
                                    EndPoint = d.EndPoint,
                                    TimeFrom = d.TimeFrom,
                                    TimeTo = d.TimeTo,
                                    Entered = d.Entered,
                                    Exited = d.Exited,
                                    Transported = d.Transported
                                }));
                                Status = $"Импортировано рейсов: {data?.Count ?? 0}";
                            },
                            errorMessage: "Ошибка загрузки импортированных рейсов");
                        break;

                    case "rounds":
                        await LoadDataToCollection(
                            command: "rounds",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<RoundRow>>(json);
                                Rounds.Clear();
                                data?.ForEach(d => Rounds.Add(new RoundRowViewModel
                                {
                                    UnitName = d.UnitName,
                                    StartPoint = d.StartPoint,
                                    EndPoint = d.EndPoint,
                                    TimeFrom = d.TimeFrom,
                                    TimeTo = d.TimeTo,
                                    Entered = d.Entered,
                                    Exited = d.Exited,
                                    Transported = d.Transported
                                }));
                                Status = $"Импортировано кругов: {data?.Count ?? 0}";
                            },
                            errorMessage: "Ошибка загрузки импортированных кругов");
                        break;

                    case "daily_records":
                        await LoadDataToCollection(
                            command: "daily_records",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<DailyRecordRow>>(json);
                                DailyRecords.Clear();
                                data?.ForEach(d => DailyRecords.Add(new DailyRecordRowViewModel
                                {
                                    UnitName = d.UnitName,
                                    RecordDate = d.RecordDate,
                                    Entered = d.Entered,
                                    Exited = d.Exited,
                                    Transported = d.Transported
                                }));
                                Status = $"Импортировано дней: {data?.Count ?? 0}";
                            },
                            errorMessage: "Ошибка загрузки импортированных дней");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки импортированных данных", ex);
                Status = "Ошибка отображения импорта";
            }
        }

        private async Task LoadDataToCollection(
            string command,
            string? idsJson,
            Action<string> onSuccess,
            string errorMessage)
        {
            try
            {
                var parameters = new Dictionary<string, string>();
                if (idsJson != null)
                {
                    parameters["ids"] = idsJson;
                }

                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = command,
                    Parameters = parameters.Count > 0 ? parameters : null
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    onSuccess(json);
                    AppLogger.Info($"[{LogContext}] Данные '{command}' загружены успешно");
                }
                else
                {
                    Status = $"Ошибка: {response.Message}";
                    AppLogger.Error($"[{LogContext}] {errorMessage}: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] {errorMessage}", ex);
            }
        }
    }
}