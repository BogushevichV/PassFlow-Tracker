using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Drawing.Charts;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Database;
using PassFlow_Tracker.Infrastructure.Logging;
using PassFlow_Tracker.UI.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        public ObservableCollection<PeakHourBarViewModel> PeakHourBars { get; } = new();

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
        private bool hasUnsavedChanges;

        [ObservableProperty]
        private bool canEdit = true;

        private bool CheckUnsavedChanges()
        {
            return CanEdit && ActiveTab switch
            {
                "trip_stops" => TripStops.Any(ts => ts.IsDirty),
                "trips" => Trips.Any(t => t.IsDirty),
                "rounds" => Rounds.Any(r => r.IsDirty),
                "daily_records" => DailyRecords.Any(dr => dr.IsDirty),
                _ => false
            };
        }

        [ObservableProperty]
        private bool isTableView = true;

        public bool IsChartView => !IsTableView;

        partial void OnIsTableViewChanged(bool value)
        {
            OnPropertyChanged(nameof(IsChartView));
        }

        [ObservableProperty]
        private TopStopsMode currentTopStopsMode = TopStopsMode.AllTime;

        public bool IsRouteColumnVisible => CurrentTopStopsMode == TopStopsMode.PerRecord;

        partial void OnCurrentTopStopsModeChanged(TopStopsMode value)
        {
            OnPropertyChanged(nameof(IsRouteColumnVisible));
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
        private int calendarYear = 2026;

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
            if (CheckUnsavedChanges())
            {
                HasUnsavedChanges = true;

                var box = MessageBoxManager.GetMessageBoxStandard(
                    "Несохранённые изменения",
                    "Есть несохранённые изменения.\n" +
                    "(Внимание: при переключении на другую вкладку " +
                    "все несохранённые изменения будут утеряны)\n" +
                    "Желаете продолжить?",
                    ButtonEnum.YesNo);
                Status = "Есть несохранённые изменения! Сохраните или отмените.";

                var result = await box.ShowAsync();

                if (result != ButtonResult.Yes)
                {
                    return;
                }
            }

            HasUnsavedChanges = false;
            ActiveTab = tab;
            CanEdit = true;

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
        private async Task ExportJson()
        {
            if (MainWindow == null) return;

            var file = await MainWindow.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Сохранить JSON",
                    DefaultExtension = "json",
                    FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
                });

            if (file == null) return;

            Status = "Экспорт...";

            try
            {
                List<RootRecord> exportData = ActiveTab switch
                {
                    "trip_stops" => JsonExportService.ExportTripStops(TripStops),
                    "trips" => JsonExportService.ExportTrips(Trips),
                    "rounds" => JsonExportService.ExportRounds(Rounds),
                    "daily_records" => JsonExportService.ExportDailyRecords(DailyRecords),
                    "all_data" => JsonExportService.ExportAllData(AllDataTree),
                    _ => throw new InvalidOperationException("Нечего экспортировать")
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(file.Path.LocalPath, json);

                Status = $"Экспортировано в: {Path.GetFileName(file.Path.LocalPath)}";
                AppLogger.Info($"[{LogContext}] Экспорт: {file.Path.LocalPath} ({exportData.Count} записей)");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Ошибка экспорта", ex);
            }
        }

        [RelayCommand]
        private async Task ExportExcel()
        {
            if (MainWindow == null) return;

            var file = await MainWindow.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Сохранить Excel",
                    DefaultExtension = "xlsx",
                    FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
                });

            if (file == null) return;

            Status = "Экспорт в Excel...";

            try
            {
                var tempPath = Path.GetTempFileName() + ".xlsx";

                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();

                    switch (ActiveTab)
                    {
                        case "trip_stops":
                            ExcelExportService.ExportTripStopsToExcel(workbook, TripStops);
                            break;
                        case "trips":
                            ExcelExportService.ExportTripsToExcel(workbook, Trips);
                            break;
                        case "rounds":
                            ExcelExportService.ExportRoundsToExcel(workbook, Rounds);
                            break;
                        case "daily_records":
                            ExcelExportService.ExportDailyRecordsToExcel(workbook, DailyRecords);
                            break;
                        case "all_data":
                            ExcelExportService.ExportAllDataToExcel(workbook, AllDataTree);
                            break;
                    }

                    workbook.SaveAs(tempPath);
                });

                File.Copy(tempPath, file.Path.LocalPath, true);
                File.Delete(tempPath);

                Status = $"Экспортировано в: {Path.GetFileName(file.Path.LocalPath)}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = file.Path.LocalPath,
                    UseShellExecute = true
                });

                AppLogger.Info($"[{LogContext}] Экспорт Excel: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Ошибка экспорта Excel", ex);
            }
        }

        [ObservableProperty]
        private bool showPeakHoursChart = false;

        partial void OnShowPeakHoursChartChanged(bool value)
        {
            OnPropertyChanged(nameof(HidePeakHoursChart));
        }

        public bool HidePeakHoursChart => !ShowPeakHoursChart;

        [ObservableProperty]
        private string peakHoursChartTitle = "Распределение пассажиропотока по часам суток";

        [RelayCommand]
        private async Task RunPeakHours()
        {
            AppLogger.Info($"[{LogContext}] Запрос: часы пик — открытие диалога");

            // 1. Загружаем список маршрутов
            List<RouteItem>? routes = null;
            try
            {
                var routesResp = await _ipc.SendAsync(new IpcRequest { Command = "routes" });
                if (routesResp.Success && routesResp.Data != null)
                {
                    var json = JsonSerializer.Serialize(routesResp.Data, JsonSerializerDefaults.OutputOptions);
                    routes = JsonSerializer.Deserialize<List<RouteItem>>(json, JsonSerializerDefaults.SafeOptions);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки маршрутов", ex);
            }

            // 2. Показываем диалог
            var dialog = new PeakHoursDialog();
            dialog.SetRoutes(routes ?? new List<RouteItem>());
            var result = await dialog.ShowDialog<(bool Confirmed, string? Unit)>(MainWindow);

            if (!result.Confirmed)
            {
                AppLogger.Info($"[{LogContext}] Часы пик: отменено");
                return;
            }

            // 3. Переключаемся на вкладку графиков
            IsTableView = false;

            try
            {
                var req = new IpcRequest
                {
                    Command = "peak_hours_chart",
                    Parameters = new()
                };
                if (!string.IsNullOrEmpty(result.Unit))
                    req.Parameters["unit"] = result.Unit;

                var response = await _ipc.SendAsync(req);

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<PeakHourChart>>(json, JsonSerializerDefaults.SafeOptions);

                    PeakHourBars.Clear();
                    if (data != null && data.Count > 0)
                    {
                        long maxFlow = data.Max(x => x.Flow);
                        foreach (var d in data)
                        {
                            PeakHourBars.Add(new PeakHourBarViewModel
                            {
                                Hour        = d.Hour,
                                Flow        = d.Flow,
                                HeightRatio = maxFlow > 0 ? (double)d.Flow / maxFlow : 0,
                                IsPeak      = d.IsPeak
                            });
                        }
                    }

                    ShowPeakHoursChart = true;
                    PeakHoursChartTitle = string.IsNullOrEmpty(result.Unit)
                        ? "Пассажиропоток по часам — вся сеть"
                        : $"Пассажиропоток по часам — {result.Unit}";

                    var peak = data?.FirstOrDefault(x => x.IsPeak);
                    Status = peak != null
                        ? $"Час пик: {peak.Hour:D2}:00 ({peak.Flow} пасс.)"
                        : "Часы пик загружены";

                    AppLogger.Info($"[{LogContext}] Гистограмма построена");
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
            AppLogger.Info($"[{LogContext}] Запрос: топ-{TopN} остановок — открытие диалога");

            var dialog = new TopStopsDialog();
            var result = await dialog.ShowDialog<TopStopsMode?>(MainWindow);

            if (result == null)
            {
                AppLogger.Info($"[{LogContext}] Топ остановок: отменено пользователем");
                return;
            }

            CurrentTopStopsMode = (TopStopsMode)result;

            ActiveTab = "trip_stops";

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "top_stops_detailed",
                    Parameters = new()
                    {
                        ["limit"] = TopN.ToString(),
                        ["mode"]  = result.Value.ToString()
                    }
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data, JsonSerializerDefaults.OutputOptions);
                    var data = JsonSerializer.Deserialize<List<TopStopRow>>(json, JsonSerializerDefaults.SafeOptions);

                    TripStops.Clear();
                    if (data != null)
                    {
                        foreach (var d in data)
                        {
                            TripStops.Add(new TripStopRowViewModel
                            {
                                StopNumber  = d.StopNumber,
                                StopName    = d.StopName,
                                Period = d.Period,
                                RouteName = d.RouteName,
                                Entered     = d.Entered,
                                Exited      = d.Exited,
                                Transported = d.Transported
                            });
                        }
                    }

                    var modeLabel = result.Value switch
                    {
                        TopStopsMode.PerDay    => "за день",
                        TopStopsMode.AllTime   => "за всё время",
                        _                      => "по записям"
                    };

                    CanEdit = false;

                    Status = $"Топ-{TopN} остановок ({modeLabel}): {data?.Count ?? 0}";
                    AppLogger.Info($"[{LogContext}] Топ-{TopN} загружен: {data?.Count ?? 0} записей");
                }
                else
                {
                    Status = $"Ошибка: {response.Message}";
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
                    var data = JsonSerializer.Deserialize<List<TripRow>>(json, JsonSerializerDefaults.SafeOptions);

                    Trips.Clear();
                    if (data != null)
                    {
                        foreach (var d in data)
                        {
                            Trips.Add(new TripRowViewModel
                            {
                                UnitName = d.UnitName,
                                StartPoint = d.StartPoint,
                                EndPoint = d.EndPoint,
                                TimeFrom = d.TimeFrom,
                                TimeTo = d.TimeTo,
                                Entered = d.Entered,
                                Exited = d.Exited,
                                Transported = d.Transported
                            });
                        }
                    }

                    CanEdit = false;

                    Status = $"Низкая активность: {data?.Count ?? 0} рейсов";
                    AppLogger.Info($"[{LogContext}] Низкая активность загружена: {data?.Count ?? 0} записей");
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

            CurrentTopStopsMode = TopStopsMode.AllTime;

            try
            {
                await LoadDataToCollection(
                command: "trip_stops",
                idsJson: null,
                onSuccess: json =>
                {
                    var data = JsonSerializer.Deserialize<List<TripStopRow>>(json);
                    TripStops.Clear();
                    data?.ForEach(d =>
                    {
                        var vm = new TripStopRowViewModel();
                        vm.SetOriginalValues(d.Id,
                            d.StopNumber, d.StopName, d.Period, "",
                            d.Entered, d.Exited, d.Transported);
                        TripStops.Add(vm);
                    });
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

        [RelayCommand]
        private async Task LoadAllData()
        {
            AppLogger.Info($"[{LogContext}] Загрузка всех данных (дерево)");
            Status = "Загрузка всех данных...";

            try
            {
                await LoadDataToCollection(
                    command: "all_data",
                    idsJson: null,
                    onSuccess: json =>
                    {
                        var data = JsonSerializer.Deserialize<List<AllDataDayDto>>(json, JsonSerializerDefaults.SafeOptions);

                        AllDataTree.Clear();
                        if (data != null)
                            foreach (var day in data)
                            {
                                var dayNode = new DayNodeViewModel
                                {
                                    UnitName = day.UnitName,
                                    RecordDate = day.RecordDate,
                                    Entered = day.Entered,
                                    Exited = day.Exited,
                                    Transported = day.Transported
                                };
                                foreach (var round in day.Rounds)
                                {
                                    var roundNode = new RoundNodeViewModel
                                    {
                                        StartPoint = round.StartPoint,
                                        EndPoint = round.EndPoint,
                                        TimeFrom = round.TimeFrom,
                                        TimeTo = round.TimeTo,
                                        Entered = round.Entered,
                                        Exited = round.Exited,
                                        Transported = round.Transported
                                    };
                                    foreach (var trip in round.Trips)
                                    {
                                        var tripNode = new TripNodeViewModel
                                        {
                                            StartPoint = trip.StartPoint,
                                            EndPoint = trip.EndPoint,
                                            TimeFrom = trip.TimeFrom,
                                            TimeTo = trip.TimeTo,
                                            Entered = trip.Entered,
                                            Exited = trip.Exited,
                                            Transported = trip.Transported
                                        };
                                        foreach (var stop in trip.Stops)
                                            tripNode.Stops.Add(new StopNodeViewModel
                                            {
                                                StopNumber = stop.StopNumber,
                                                StopName = stop.StopName,
                                                IsDuplicate = stop.IsDuplicate,
                                                IsSkipped = stop.IsSkipped,
                                                TimeFrom = stop.TimeFrom,
                                                TimeTo = stop.TimeTo,
                                                Entered = stop.Entered,
                                                Exited = stop.Exited,
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
                    },
                    errorMessage: "Ошибка загрузки дерева");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Исключение при загрузке дерева", ex);
            }
        }

        [RelayCommand]
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
                    data?.ForEach(d =>
                    {
                        var vm = new DailyRecordRowViewModel();
                        vm.SetOriginalValues(
                            id: d.Id,
                            unitName: d.UnitName,
                            recordDate: d.RecordDate,
                            entered: d.Entered,
                            exited: d.Exited,
                            transported: d.Transported);
                        DailyRecords.Add(vm);
                    });
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

        [RelayCommand]
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
                    data?.ForEach(d =>
                    {
                        var vm = new RoundRowViewModel();
                        vm.SetOriginalValues(
                            id: d.Id,
                            unitName: d.UnitName,
                            startPoint: d.StartPoint,
                            endPoint: d.EndPoint,
                            timeFrom: d.TimeFrom,
                            timeTo: d.TimeTo,
                            entered: d.Entered,
                            exited: d.Exited,
                            transported: d.Transported);
                        Rounds.Add(vm);
                    });
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
                    data?.ForEach(d =>
                    {
                        var vm = new TripRowViewModel();
                        vm.SetOriginalValues(
                            id: d.Id,
                            unitName: d.UnitName,
                            startPoint: d.StartPoint,
                            endPoint: d.EndPoint,
                            timeFrom: d.TimeFrom,
                            timeTo: d.TimeTo,
                            entered: d.Entered,
                            exited: d.Exited,
                            transported: d.Transported);
                        Trips.Add(vm);
                    });
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
                        await LoadDataToCollection(
                            command: "trip_stops",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<TripStopRow>>(json);
                                TripStops.Clear();
                                data?.ForEach(d =>
                                {
                                    var vm = new TripStopRowViewModel();
                                    vm.SetOriginalValues(
                                        d.Id,
                                        d.StopNumber, d.StopName, d.Period, "",
                                        d.Entered, d.Exited, d.Transported);
                                    TripStops.Add(vm);
                                });
                                Status = $"Импортировано остановок: {data?.Count ?? 0}";
                            },
                            errorMessage: "Ошибка загрузки импортированных остановок");
                        break;

                    case "all_data":
                        await LoadDataToCollection(
                            command: "all_data",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<AllDataDayDto>>(json, JsonSerializerDefaults.SafeOptions);

                                AllDataTree.Clear();
                                if (data != null)
                                    foreach (var day in data)
                                    {
                                        var dayNode = new DayNodeViewModel
                                        {
                                            UnitName = day.UnitName,
                                            RecordDate = day.RecordDate,
                                            Entered = day.Entered,
                                            Exited = day.Exited,
                                            Transported = day.Transported
                                        };
                                        foreach (var round in day.Rounds)
                                        {
                                            var roundNode = new RoundNodeViewModel
                                            {
                                                StartPoint = round.StartPoint,
                                                EndPoint = round.EndPoint,
                                                TimeFrom = round.TimeFrom,
                                                TimeTo = round.TimeTo,
                                                Entered = round.Entered,
                                                Exited = round.Exited,
                                                Transported = round.Transported
                                            };
                                            foreach (var trip in round.Trips)
                                            {
                                                var tripNode = new TripNodeViewModel
                                                {
                                                    StartPoint = trip.StartPoint,
                                                    EndPoint = trip.EndPoint,
                                                    TimeFrom = trip.TimeFrom,
                                                    TimeTo = trip.TimeTo,
                                                    Entered = trip.Entered,
                                                    Exited = trip.Exited,
                                                    Transported = trip.Transported
                                                };
                                                foreach (var stop in trip.Stops)
                                                    tripNode.Stops.Add(new StopNodeViewModel
                                                    {
                                                        StopNumber = stop.StopNumber,
                                                        StopName = stop.StopName,
                                                        IsDuplicate = stop.IsDuplicate,
                                                        IsSkipped = stop.IsSkipped,
                                                        TimeFrom = stop.TimeFrom,
                                                        TimeTo = stop.TimeTo,
                                                        Entered = stop.Entered,
                                                        Exited = stop.Exited,
                                                        Transported = stop.Transported
                                                    });
                                                roundNode.Trips.Add(tripNode);
                                            }
                                            dayNode.Rounds.Add(roundNode);
                                        }
                                        AllDataTree.Add(dayNode);
                                    }

                                Status = $"Импортировано (дерево): {data?.Count ?? 0} дней";
                            },
                            errorMessage: "Ошибка загрузки импортированного дерева");
                        break;

                    case "trips":
                        await LoadDataToCollection(
                            command: "trips",
                            idsJson: idsJson,
                            onSuccess: json =>
                            {
                                var data = JsonSerializer.Deserialize<List<TripRow>>(json);
                                Trips.Clear();
                                data?.ForEach(d =>
                                {
                                    var vm = new TripRowViewModel();
                                    vm.SetOriginalValues(
                                        id: d.Id,
                                        unitName: d.UnitName,
                                        startPoint: d.StartPoint,
                                        endPoint: d.EndPoint,
                                        timeFrom: d.TimeFrom,
                                        timeTo: d.TimeTo,
                                        entered: d.Entered,
                                        exited: d.Exited,
                                        transported: d.Transported);
                                    Trips.Add(vm);
                                });
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
                                data?.ForEach(d =>
                                {
                                    var vm = new RoundRowViewModel();
                                    vm.SetOriginalValues(
                                        id: d.Id,
                                        unitName: d.UnitName,
                                        startPoint: d.StartPoint,
                                        endPoint: d.EndPoint,
                                        timeFrom: d.TimeFrom,
                                        timeTo: d.TimeTo,
                                        entered: d.Entered,
                                        exited: d.Exited,
                                        transported: d.Transported);
                                    Rounds.Add(vm);
                                });
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
                                data?.ForEach(d =>
                                {
                                    var vm = new DailyRecordRowViewModel();
                                    vm.SetOriginalValues(
                                        id: d.Id,
                                        unitName: d.UnitName,
                                        recordDate: d.RecordDate,
                                        entered: d.Entered,
                                        exited: d.Exited,
                                        transported: d.Transported);
                                    DailyRecords.Add(vm);
                                });
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

        [RelayCommand]
        private async Task SaveChanges()
        {
            if (!CheckUnsavedChanges())
            {
                Status = "Нет изменений для сохранения";
                return;
            }

            Status = "Сохранение...";

            try
            {
                string? resultMessage = null;

                switch (ActiveTab)
                {
                    case "trip_stops":
                        resultMessage = await SaveTripStops();
                        break;
                    case "trips":
                        resultMessage = await SaveTrips();
                        break;
                    case "rounds":
                        resultMessage = await SaveRounds();
                        break;
                    case "daily_records":
                        resultMessage = await SaveDailyRecords();
                        break;
                    case "all_data":
                        Status = "Вкладка 'Все данные' не поддерживает редактирование";
                        return;
                }

                HasUnsavedChanges = false;
                Status = resultMessage ?? "Изменения сохранены";
                AppLogger.Info($"[{LogContext}] {Status}");
            }
            catch (Exception ex)
            {
                Status = $"Ошибка сохранения: {ex.Message}";
                AppLogger.Error($"[{LogContext}] Ошибка сохранения", ex);
            }
        }

        [RelayCommand]
        private void CancelChanges()
        {
            int revertedCount = 0;

            switch (ActiveTab)
            {
                case "trip_stops":
                    revertedCount = TripStops.Count(ts => ts.IsDirty);
                    foreach (var item in TripStops.Where(ts => ts.IsDirty))
                        item.RejectChanges();
                    break;
                case "trips":
                    revertedCount = Trips.Count(t => t.IsDirty);
                    foreach (var item in Trips.Where(t => t.IsDirty))
                        item.RejectChanges();
                    break;
                case "rounds":
                    revertedCount = Rounds.Count(r => r.IsDirty);
                    foreach (var item in Rounds.Where(r => r.IsDirty))
                        item.RejectChanges();
                    break;
                case "daily_records":
                    revertedCount = DailyRecords.Count(dr => dr.IsDirty);
                    foreach (var item in DailyRecords.Where(dr => dr.IsDirty))
                        item.RejectChanges();
                    break;
            }

            HasUnsavedChanges = false;
            Status = revertedCount > 0 ? $"Отменено изменений: {revertedCount}" : "Нет изменений";
            AppLogger.Info($"[{LogContext}] {Status}");
        }

        private async Task<string> SaveTripStops()
        {
            var changed = TripStops.Where(ts => ts.IsDirty).ToList();
            if (!changed.Any()) return "Нет изменений";

            var data = changed.Select(ts => new
            {
                Id = ts.TripStopId,
                ts.StopNumber,
                ts.StopName,
                ts.TimeFrom,
                ts.TimeTo,
                ts.Entered,
                ts.Exited,
                ts.Transported
            }).ToList();

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "update_trip_stops",
                Parameters = new() { ["data"] = JsonSerializer.Serialize(data) }
            });

            if (!response.Success)
                throw new Exception(response.Message);

            foreach (var item in changed)
                item.AcceptChanges();

            return $"Сохранено остановок: {changed.Count}";
        }

        private async Task<string> SaveTrips()
        {
            var changed = Trips.Where(t => t.IsDirty).ToList();
            if (!changed.Any()) return "Нет изменений";

            var data = changed.Select(t =>
            {
                var fromLocal = DateTime.Parse(t.TimeFrom);
                var toLocal = DateTime.Parse(t.TimeTo);

                var fromUtc = fromLocal.Kind == DateTimeKind.Utc
                    ? fromLocal
                    : TimeZoneInfo.ConvertTimeToUtc(fromLocal, TimeZoneInfo.Local);
                var toUtc = toLocal.Kind == DateTimeKind.Utc
                    ? toLocal
                    : TimeZoneInfo.ConvertTimeToUtc(toLocal, TimeZoneInfo.Local);

                return new
                {
                    Id = t.TripId,
                    t.UnitName,
                    t.StartPoint,
                    t.EndPoint,
                    TimeFrom = fromUtc.ToString("yyyy-MM-dd HH:mm"),
                    TimeTo = toUtc.ToString("yyyy-MM-dd HH:mm"),
                    t.Entered,
                    t.Exited,
                    t.Transported
                };
            }).ToList();

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "update_trips",
                Parameters = new() { ["data"] = JsonSerializer.Serialize(data) }
            });

            if (!response.Success)
                throw new Exception(response.Message);

            foreach (var item in changed)
                item.AcceptChanges();

            return $"Сохранено рейсов: {changed.Count}";
        }

        private async Task<string> SaveRounds()
        {
            var changed = Rounds.Where(r => r.IsDirty).ToList();
            if (!changed.Any()) return "Нет изменений";

            var data = changed.Select(r =>
            {
                var fromLocal = DateTime.Parse(r.TimeFrom);
                var toLocal = DateTime.Parse(r.TimeTo);

                var fromUtc = fromLocal.Kind == DateTimeKind.Utc
                    ? fromLocal
                    : TimeZoneInfo.ConvertTimeToUtc(fromLocal, TimeZoneInfo.Local);
                var toUtc = toLocal.Kind == DateTimeKind.Utc
                    ? toLocal
                    : TimeZoneInfo.ConvertTimeToUtc(toLocal, TimeZoneInfo.Local);

                return new
                {
                    Id = r.RoundId,
                    r.UnitName,
                    r.StartPoint,
                    r.EndPoint,
                    TimeFrom = fromUtc.ToString("yyyy-MM-dd HH:mm"),
                    TimeTo = toUtc.ToString("yyyy-MM-dd HH:mm"),
                    r.Entered,
                    r.Exited,
                    r.Transported
                };
            }).ToList();

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "update_rounds",
                Parameters = new() { ["data"] = JsonSerializer.Serialize(data) }
            });

            if (!response.Success)
                throw new Exception(response.Message);

            foreach (var item in changed)
                item.AcceptChanges();

            return $"Сохранено кругов: {changed.Count}";
        }

        private async Task<string> SaveDailyRecords()
        {
            var changed = DailyRecords.Where(dr => dr.IsDirty).ToList();
            if (!changed.Any()) return "Нет изменений";

            var data = changed.Select(dr => new
            {
                Id = dr.DailyRecordId,
                dr.UnitName,
                dr.RecordDate,
                dr.Entered,
                dr.Exited,
                dr.Transported
            }).ToList();

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "update_daily_records",
                Parameters = new() { ["data"] = JsonSerializer.Serialize(data) }
            });

            if (!response.Success)
                throw new Exception(response.Message);

            foreach (var item in changed)
                item.AcceptChanges();

            return $"Сохранено дней: {changed.Count}";
        }
    }
}