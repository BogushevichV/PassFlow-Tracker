using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Drawing.Charts;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Infrastructure.Database;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public Window? MainWindow { get; set; }

        private readonly JsonImportService _jsonService;
        private readonly TransportAnalytics _analytics;

        public ObservableCollection<TripStopRowViewModel> TripStops { get; } = new();
        public ObservableCollection<RoundRowViewModel> Rounds { get; } = new();
        public ObservableCollection<TripRowViewModel> Trips { get; } = new();

        public bool ShowTripStops => ActiveTab == "trip_stops";
        public bool ShowRounds    => ActiveTab == "rounds";
        public bool ShowTrips     => ActiveTab == "trips";

        partial void OnActiveTabChanged(string value)
        {
            OnPropertyChanged(nameof(ShowTripStops));
            OnPropertyChanged(nameof(ShowRounds));
            OnPropertyChanged(nameof(ShowTrips));
        }

        public MainWindowViewModel()
        {
            var db = new DbConnectionFactory();

            _jsonService = new JsonImportService(db);
            _analytics = new TransportAnalytics(db);
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            await LoadTripStops();
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
            ActiveTab = tab;

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
                    Status = "Загрузка дней...";
                    break;
                case "all_data":
                    Status = "Загрузка всех данных...";
                    break;
            }
        }

        private async Task LoadTripStops()
        {
            Status = "Загрузка остановок...";
            try
            {
                var data = await _analytics.GetTripStopsAsync();
                TripStops.Clear();
                foreach (var d in data)
                {
                    TripStops.Add(new TripStopRowViewModel
                    {
                        StopNumber  = d.StopNumber,
                        StopName    = d.StopName,
                        Entered     = d.Entered,
                        Exited      = d.Exited,
                        Transported = d.Transported
                    });
                }
                Status = $"Остановки: {data.Count}";
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
            }
        }

        private async Task LoadRounds()
        {
            Status = "Загрузка кругов...";
            try
            {
                var data = await _analytics.GetRoundsAsync();
                Rounds.Clear();
                foreach (var d in data)
                    Rounds.Add(new RoundRowViewModel
                    {
                        UnitName    = d.UnitName,
                        StartPoint  = d.StartPoint,
                        EndPoint    = d.EndPoint,
                        TimeFrom    = d.TimeFrom,
                        TimeTo      = d.TimeTo,
                        Entered     = d.Entered,
                        Exited      = d.Exited,
                        Transported = d.Transported
                    });
                Status = $"Круги: {data.Count}";
            }
            catch (Exception ex) { Status = $"Ошибка: {ex.Message}"; }
        }

        private async Task LoadTrips()
        {
            Status = "Загрузка рейсов...";
            try
            {
                var data = await _analytics.GetTripsAsync();
                Trips.Clear();
                foreach (var d in data)
                    Trips.Add(new TripRowViewModel
                    {
                        UnitName    = d.UnitName,
                        StartPoint  = d.StartPoint,
                        EndPoint    = d.EndPoint,
                        TimeFrom    = d.TimeFrom,
                        TimeTo      = d.TimeTo,
                        Entered     = d.Entered,
                        Exited      = d.Exited,
                        Transported = d.Transported
                    });
                Status = $"Рейсы: {data.Count}";
            }
            catch (Exception ex) { Status = $"Ошибка: {ex.Message}"; }
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
            if (file == null) return;

            Status = "Импорт...";

            try
            {
                await _jsonService.ImportAsync(file.Path.LocalPath);
                Status = "JSON загружен";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }


        [RelayCommand]
        private async Task RunPeakHours()
        {
            var data = await _analytics.GetPeakHoursAsync();

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
        }

        [RelayCommand]
        private async Task RunTopStops()
        {
            var data = await _analytics.GetTopStopsAsync(TopN);

            TripStops.Clear();
            foreach (var d in data)
            {
                TripStops.Add(new TripStopRowViewModel
                {
                    StopName = d.Name,
                    Transported = (int)d.Load
                });
            }

            Status = $"Топ {TopN}";
        }

        [RelayCommand]
        private async Task RunLowActivity()
        {
            var data = await _analytics.GetLowActivityTripsAsync(Threshold);

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
        }
    }
}