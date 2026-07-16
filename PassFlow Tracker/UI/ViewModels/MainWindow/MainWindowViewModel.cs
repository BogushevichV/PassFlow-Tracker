using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Logging;
using PassFlow_Tracker.UI.Views;
using PassFlow_Tracker.UI.ViewModels.Core;
using PassFlow_Tracker.UI.ViewModels.Formatting;
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
        public ObservableCollection<VehicleViewModel> Vehicles { get; } = new();
        public ObservableCollection<VehicleModelViewModel> AvailableModels { get; } = new();
        public ObservableCollection<VehicleModelViewModel> VehicleModels { get; } = new();

        public AnalyticsViewModel Analytics { get; }

        public bool ShowTripStops    => ActiveTab == "trip_stops";
        public bool ShowRounds       => ActiveTab == "rounds";
        public bool ShowTrips        => ActiveTab == "trips";
        public bool ShowDailyRecords => ActiveTab == "daily_records";
        public bool ShowAllData      => ActiveTab == "all_data";
        public bool ShowVehicles     => ActiveTab == "vehicles";
        public bool ShowVehModels    => ActiveTab == "vehicle_models";
        public bool ShowRouteAnalytics => ActiveTab == "route_analytics";

        partial void OnActiveTabChanged(string value)
        {
            OnPropertyChanged(nameof(ShowTripStops));
            OnPropertyChanged(nameof(ShowRounds));
            OnPropertyChanged(nameof(ShowTrips));
            OnPropertyChanged(nameof(ShowDailyRecords));
            OnPropertyChanged(nameof(ShowAllData));
            OnPropertyChanged(nameof(ShowVehicles));
            OnPropertyChanged(nameof(ShowVehicleModels));
            OnPropertyChanged(nameof(ShowRouteAnalytics));
            UpdateAvailableColumns();
        }

        private const string LogContext = "MainWindowViewModel";

        public MainWindowViewModel()
        {
            Analytics = new AnalyticsViewModel(_ipc, setStatus: msg => Status = msg);

            Calendar = new CalendarViewModel(
                onFilterApplied: () => _ = SetActiveTab(ActiveTab),
                flowLoader: async (from, to) =>
                {
                    var response = await _ipc.SendAsync(new IpcRequest
                    {
                        Command = "daily_flow",
                        Parameters = new()
                        {
                            ["from"] = from.ToString("yyyy-MM-dd"),
                            ["to"] = to.ToString("yyyy-MM-dd")
                        }
                    });

                    if (response.Success && response.Data != null)
                    {
                        var json = JsonSerializer.Serialize(response.Data);
                        return JsonSerializer.Deserialize<Dictionary<DateOnly, long>>(json)
                               ?? new Dictionary<DateOnly, long>();
                    }

                    return new Dictionary<DateOnly, long>();
                }
            );

            UpdateAvailableColumns();
        }

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
            UpdateAvailableColumns();
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
            UpdateGradientVisibility();
        }

        [ObservableProperty]
        private bool showColorMenu;

        [ObservableProperty]
        private bool showCellFilterMenu;

        [ObservableProperty]
        private int gradientDirectionIndex;

        [ObservableProperty]
        private int selectedColorSchemeIndex;

        [ObservableProperty]
        private double gradientSteps = 5;

        [ObservableProperty]
        private ObservableCollection<ColumnItem> availableColumns = new();

        [ObservableProperty]
        private ColumnItem? selectedColumn;

        private readonly Dictionary<string, HashSet<string>> _formattedColumnsByTab = new();

        private static readonly Dictionary<string, List<ColumnItem>> TabColumns = new()
        {
            ["trip_stops"] =
            [
                new() { Header = "№ Остановки", PropertyName = nameof(TripStopRowViewModel.StopNumber), IsNumeric = true },
                new() { Header = "Название", PropertyName = nameof(TripStopRowViewModel.StopName), IsNumeric = false },
                new() { Header = "Период", PropertyName = nameof(TripStopRowViewModel.Period), IsNumeric = false },
                new() { Header = "Имя маршрута", PropertyName = nameof(TripStopRowViewModel.RouteName), IsNumeric = false },
                new() { Header = "Вошло", PropertyName = nameof(TripStopRowViewModel.Entered), IsNumeric = true },
                new() { Header = "Вышло", PropertyName = nameof(TripStopRowViewModel.Exited), IsNumeric = true },
                new() { Header = "Перевезено", PropertyName = nameof(TripStopRowViewModel.Transported), IsNumeric = true },
            ],
            ["rounds"] =
            [
                new() { Header = "Автобус", PropertyName = nameof(RoundRowViewModel.UnitName), IsNumeric = false },
                new() { Header = "Откуда", PropertyName = nameof(RoundRowViewModel.StartPoint), IsNumeric = false },
                new() { Header = "Куда", PropertyName = nameof(RoundRowViewModel.EndPoint), IsNumeric = false },
                new() { Header = "Время От", PropertyName = nameof(RoundRowViewModel.TimeFrom), IsNumeric = false },
                new() { Header = "Время До", PropertyName = nameof(RoundRowViewModel.TimeTo), IsNumeric = false },
                new() { Header = "Вошло", PropertyName = nameof(RoundRowViewModel.Entered), IsNumeric = true },
                new() { Header = "Вышло", PropertyName = nameof(RoundRowViewModel.Exited), IsNumeric = true },
                new() { Header = "Перевезено", PropertyName = nameof(RoundRowViewModel.Transported), IsNumeric = true },
            ],
            ["trips"] =
            [
                new() { Header = "Автобус", PropertyName = nameof(TripRowViewModel.UnitName), IsNumeric = false },
                new() { Header = "Откуда", PropertyName = nameof(TripRowViewModel.StartPoint), IsNumeric = false },
                new() { Header = "Куда", PropertyName = nameof(TripRowViewModel.EndPoint), IsNumeric = false },
                new() { Header = "Время От", PropertyName = nameof(TripRowViewModel.TimeFrom), IsNumeric = false },
                new() { Header = "Время До", PropertyName = nameof(TripRowViewModel.TimeTo), IsNumeric = false },
                new() { Header = "Вошло", PropertyName = nameof(TripRowViewModel.Entered), IsNumeric = true },
                new() { Header = "Вышло", PropertyName = nameof(TripRowViewModel.Exited), IsNumeric = true },
                new() { Header = "Перевезено", PropertyName = nameof(TripRowViewModel.Transported), IsNumeric = true },
            ],
            ["daily_records"] =
            [
                new() { Header = "Автобус", PropertyName = nameof(DailyRecordRowViewModel.UnitName), IsNumeric = false },
                new() { Header = "Дата", PropertyName = nameof(DailyRecordRowViewModel.RecordDate), IsNumeric = false },
                new() { Header = "Вошло", PropertyName = nameof(DailyRecordRowViewModel.Entered), IsNumeric = true },
                new() { Header = "Вышло", PropertyName = nameof(DailyRecordRowViewModel.Exited), IsNumeric = true },
                new() { Header = "Перевезено", PropertyName = nameof(DailyRecordRowViewModel.Transported), IsNumeric = true },
            ],
            ["all_data"] =
            [
                new() { Header = "Маршрут / Остановка", PropertyName = "Label", IsNumeric = false },
                new() { Header = "Время", PropertyName = "Time", IsNumeric = false },
                new() { Header = "Вошло", PropertyName = nameof(DayNodeViewModel.Entered), IsNumeric = true },
                new() { Header = "Вышло", PropertyName = nameof(DayNodeViewModel.Exited), IsNumeric = true },
                new() { Header = "В салоне", PropertyName = nameof(DayNodeViewModel.Transported), IsNumeric = true },
            ],
        };

        public string GradientButtonLabel =>
            GradientActive ? "Вкл. Градиент" : "Выкл. Градиент";

        partial void OnGradientDirectionIndexChanged(int value) => RefreshAllFormattedColumns();
        partial void OnSelectedColorSchemeIndexChanged(int value) => RefreshAllFormattedColumns();
        partial void OnGradientStepsChanged(double value) => RefreshAllFormattedColumns();

        public CalendarViewModel Calendar { get; }

        [RelayCommand]
        private void SwitchToTable() => IsTableView = true;

        [RelayCommand]
        private void SwitchToChart() => IsTableView = false;

        [RelayCommand]
        private void ToggleColorMenu() => ShowColorMenu = !ShowColorMenu;

        [RelayCommand]
        private void ToggleCellFilterMenu()
        {
            UpdateAvailableColumns();
            ShowCellFilterMenu = !ShowCellFilterMenu;
        }

        [RelayCommand]
        private void SelectColorScheme(string index)
        {
            if (int.TryParse(index, out var schemeIndex))
                SelectedColorSchemeIndex = schemeIndex;
        }

        [RelayCommand]
        private void ApplyColumnFormatting()
        {
            if (SelectedColumn == null)
            {
                Status = "Выберите колонку";
                return;
            }

            if (!SelectedColumn.IsNumeric)
            {
                Status = "Градиент применим только к числовым колонкам";
                return;
            }

            if (!TabColumns.ContainsKey(ActiveTab))
            {
                Status = "Форматирование недоступно для этой вкладки";
                return;
            }

            if (!_formattedColumnsByTab.TryGetValue(ActiveTab, out var formatted))
            {
                formatted = new HashSet<string>();
                _formattedColumnsByTab[ActiveTab] = formatted;
            }

            formatted.Add(SelectedColumn.PropertyName);
            ApplyFormattingToColumn(ActiveTab, SelectedColumn.PropertyName);
            ShowCellFilterMenu = false;
            Status = $"Форматирование применено к «{SelectedColumn.Header}»";
        }

        [RelayCommand]
        private void ToggleGradient() => GradientActive = !GradientActive;

        private void UpdateAvailableColumns()
        {
            AvailableColumns.Clear();
            if (!TabColumns.TryGetValue(ActiveTab, out var columns))
            {
                SelectedColumn = null;
                return;
            }

            foreach (var column in columns)
            {
                if (ActiveTab == "trip_stops"
                    && column.PropertyName == nameof(TripStopRowViewModel.RouteName)
                    && !IsRouteColumnVisible)
                    continue;

                AvailableColumns.Add(column);
            }

            SelectedColumn = AvailableColumns.FirstOrDefault();
        }

        private GradientSettings BuildGradientSettings() => new()
        {
            MinToMax = GradientDirectionIndex == 0,
            ColorSchemeIndex = SelectedColorSchemeIndex,
            Steps = (int)Math.Round(GradientSteps),
        };

        private void ApplyFormattingToColumn(string tab, string propertyName)
        {
            var settings = BuildGradientSettings();
            GradientFormatter.Apply(GetFormattableRows(tab), propertyName, settings);
        }

        private void RefreshAllFormattedColumns()
        {
            foreach (var (tab, columns) in _formattedColumnsByTab)
            {
                foreach (var column in columns)
                    ApplyFormattingToColumn(tab, column);
            }
        }

        private void UpdateGradientVisibility()
        {
            foreach (var row in GetFormattableRows(ActiveTab))
                row.ShowGradient = GradientActive;

            foreach (var (tab, columns) in _formattedColumnsByTab)
            {
                foreach (var row in GetFormattableRows(tab))
                {
                    row.ShowGradient = GradientActive;
                    row.NotifyFormattingChanged();
                }
            }
        }

        private IEnumerable<IGradientFormattable> GetFormattableRows(string tab) => tab switch
        {
            "trip_stops" => TripStops.Cast<IGradientFormattable>(),
            "trips" => Trips.Cast<IGradientFormattable>(),
            "rounds" => Rounds.Cast<IGradientFormattable>(),
            "daily_records" => DailyRecords.Cast<IGradientFormattable>(),
            "all_data" => GetAllTreeNodes(),
            _ => Enumerable.Empty<IGradientFormattable>(),
        };

        private IEnumerable<IGradientFormattable> GetAllTreeNodes()
        {
            foreach (var day in AllDataTree)
            {
                yield return day;
                foreach (var round in day.Rounds)
                {
                    yield return round;
                    foreach (var trip in round.Trips)
                    {
                        yield return trip;
                        foreach (var stop in trip.Stops)
                            yield return stop;
                    }
                }
            }
        }

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
        private async Task OpenRouteAnalytics()
        {
            ActiveTab = "route_analytics";
            OnPropertyChanged(nameof(ShowRouteAnalytics));
            await Analytics.LoadRoutesAsync();
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
                string? dateFilterJson = GetDateFilter();

                var parameters = new Dictionary<string, string>();
                if (dateFilterJson != null) parameters["dateFilter"] = dateFilterJson;

                var routesResp = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "routes",
                    Parameters = parameters.Count > 0 ? parameters : null
                });

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
        private async Task OpenVehicles()
        {
            AppLogger.Info($"[{LogContext}] Запрос: транспорт");

            ActiveTab = "vehicles";

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest { Command = "vehicles" });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<VehicleInfo>>(json);

                    Vehicles.Clear();
                    if (data != null)
                        foreach (var v in data)
                            Vehicles.Add(new VehicleViewModel
                            {
                                Id = v.Id,
                                UnitName = v.UnitName,
                                ModelId = v.ModelId,
                                Description = v.Description ?? ""
                            });

                    Status = $"Транспорт: {data?.Count ?? 0}";
                    AppLogger.Info($"[{LogContext}] Транспорт загружен: {data?.Count ?? 0} записей");
                }

                var modelsResp = await _ipc.SendAsync(new IpcRequest { Command = "vehicle_models" });
                if (modelsResp.Success && modelsResp.Data != null)
                {
                    var json = JsonSerializer.Serialize(modelsResp.Data);
                    var data = JsonSerializer.Deserialize<List<VehicleModelInfo>>(json);
                    AvailableModels.Clear();
                    if (data != null)
                        foreach (var m in data)
                            AvailableModels.Add(new VehicleModelViewModel
                            {
                                Id = m.Id,
                                Name = m.Name,
                                Seats = m.Seats,
                                Capacity = m.Capacity,
                                Description = m.Description ?? ""
                            });

                    foreach (var vehicle in Vehicles)
                    {
                        vehicle.SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == vehicle.ModelId);
                    }

                    AppLogger.Info($"[{LogContext}] Доступные модели загружены: {data?.Count ?? 0} записей");
                }

            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки транспорта", ex);
                Status = $"Ошибка: {ex.Message}";
            }
        }

        private string _vehiclesSubTab = "list";
        public bool ShowVehicleModels => ShowVehicles && _vehiclesSubTab == "models";
        public bool ShowVehiclesList => ShowVehicles && _vehiclesSubTab == "list";

        [RelayCommand]
        private void SetVehiclesSubTab(string tab)
        {
            _vehiclesSubTab = tab;
            OnPropertyChanged(nameof(ShowVehicleModels));
            OnPropertyChanged(nameof(ShowVehiclesList));
        }

        [RelayCommand]
        private async Task OpenVehicleModels()
        {
            AppLogger.Info($"[{LogContext}] Запрос: модели транспорта");

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest { Command = "vehicle_models" });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<VehicleModelInfo>>(json);

                    VehicleModels.Clear();
                    if (data != null)
                        foreach (var m in data)
                            VehicleModels.Add(new VehicleModelViewModel
                            {
                                Id = m.Id,
                                Name = m.Name,
                                Seats = m.Seats,
                                Capacity = m.Capacity,
                                Description = m.Description ?? ""
                            });

                    Status = $"Модели транспорта: {data?.Count ?? 0}";
                    AppLogger.Info($"[{LogContext}] Модели транспорта загружены: {data?.Count ?? 0} записей");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки моделей транспорта", ex);
                Status = $"Ошибка: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SaveVehicle(VehicleViewModel vehicle)
        {
            AppLogger.Info($"[{LogContext}] Сохранение: транспорт");

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "update_vehicle",
                    Parameters = new()
                    {
                        ["id"] = vehicle.Id.ToString(),
                        ["unit_name"] = vehicle.UnitName,
                        ["model_id"] = vehicle.ModelId.ToString(),
                        ["description"] = vehicle.Description
                    }
                });

                Status = response.Message;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка сохранения транспорта", ex);
                Status = $"Ошибка: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SaveModel(VehicleModelViewModel model)
        {
            AppLogger.Info($"[{LogContext}] Сохранение: модели транспорт");

            try
            {
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "update_vehicle_model",
                    Parameters = new()
                    {
                        ["id"] = model.Id.ToString(),
                        ["seats"] = model.Seats.ToString(),
                        ["capacity"] = model.Capacity.ToString(),
                        ["description"] = model.Description
                    }
                });
                Status = response.Message;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка сохранения моделей транспорта", ex);
                Status = $"Ошибка: {ex.Message}";
            }
        }

        private string? GetDateFilter()
        {
            if (Calendar.IsFilterApplied && Calendar.FilterStartDate != null && Calendar.FilterEndDate != null)
            {
                return JsonSerializer.Serialize(new
                {
                    from = Calendar.FilterStartDate.Value.ToString("yyyy-MM-dd"),
                    to = Calendar.FilterEndDate.Value.ToString("yyyy-MM-dd")
                });
            }

            return null;
        } 

        [RelayCommand]
        private async Task LoadTripStops()
        {
            AppLogger.Info($"[{LogContext}] Загрузка остановок");
            Status = "Загрузка остановок...";

            CurrentTopStopsMode = TopStopsMode.AllTime;

            string? dateFilterJson = GetDateFilter();

            try
            {
                await LoadDataToCollection(
                command: "trip_stops",
                idsJson: null,
                dateFilterJson: dateFilterJson,
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

            string? dateFilterJson = GetDateFilter();

            try
            {
                await LoadDataToCollection(
                    command: "all_data",
                    idsJson: null,
                    dateFilterJson: dateFilterJson,
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

            string? dateFilterJson = GetDateFilter();

            try
            {
                await LoadDataToCollection(
                command: "daily_records",
                idsJson: null,
                dateFilterJson: dateFilterJson,
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

            string? dateFilterJson = GetDateFilter();

            try
            {
                await LoadDataToCollection(
                command: "rounds",
                idsJson: null,
                dateFilterJson: dateFilterJson,
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

            string? dateFilterJson = GetDateFilter();

            try
            {
                await LoadDataToCollection(
                command: "trips",
                idsJson: null,
                dateFilterJson: dateFilterJson,
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
                            dateFilterJson: null,
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
                            dateFilterJson: null,
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
                            dateFilterJson: null,
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
                            dateFilterJson: null,
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
                            dateFilterJson: null,
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
            string? dateFilterJson,
            Action<string> onSuccess,
            string errorMessage)
        {
            try
            {
                var parameters = new Dictionary<string, string>();
                if (idsJson != null) parameters["ids"] = idsJson;
                if (dateFilterJson != null) parameters["dateFilter"] = dateFilterJson; 

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