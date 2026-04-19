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

        public MainWindowViewModel()
        {
            var db = new DbConnectionFactory();

            _jsonService = new JsonImportService(db);
            _analytics = new TransportAnalytics(db);
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
        private void SetActiveTab(string tab) => ActiveTab = tab;

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