using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // --- Data ---
        public ObservableCollection<TripStopRowViewModel> TripStops { get; } = new();
        // --- View Mode ---
        [ObservableProperty] private bool _isTableView = true;
        [ObservableProperty] private bool _isChartView = false;

        // --- Active Tab ---
        [ObservableProperty] private string _activeTab = "trip_stops";

        // --- Gradient ---
        [ObservableProperty] private bool _gradientActive = true;
        [ObservableProperty] private bool _showColorMenu = false;
        [ObservableProperty] private string _gradientDirection = "min-to-max";

        // --- Calendar ---
        [ObservableProperty] private bool _showCalendar = false;
        [ObservableProperty] private string _calendarMode = "days"; // days | months | years
        [ObservableProperty] private int _calendarYear = 2025;
        [ObservableProperty] private int _calendarMonth = 6; // 1-12

        // --- Computed labels ---
        public string GradientButtonLabel => GradientActive ? "Вкл. Градиент" : "Выкл. Градиент";

        public string CalendarTitle
        {
            get
            {
                string[] months = { "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
                                    "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь" };
                if (CalendarMode == "days")
                    return $"{months[CalendarMonth - 1]} {CalendarYear}";
                if (CalendarMode == "months")
                    return $"{CalendarYear}";
                return $"{CalendarYear - 5} – {CalendarYear + 6}";
            }
        }

        // --- Commands ---
        [RelayCommand]
        private void SwitchToTable() { IsTableView = true; IsChartView = false; }

        [RelayCommand]
        private void SwitchToChart() { IsTableView = false; IsChartView = true; }

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
                if (CalendarMonth < 1) { CalendarMonth = 12; CalendarYear--; }
            }
            else if (CalendarMode == "months") CalendarYear--;
            else if (CalendarMode == "years") CalendarYear -= 12;
        }

        [RelayCommand]
        private void CalendarNext()
        {
            if (CalendarMode == "days")
            {
                CalendarMonth++;
                if (CalendarMonth > 12) { CalendarMonth = 1; CalendarYear++; }
            }
            else if (CalendarMode == "months") CalendarYear++;
            else if (CalendarMode == "years") CalendarYear += 12;
        }
        partial void OnGradientActiveChanged(bool value) =>
            OnPropertyChanged(nameof(GradientButtonLabel));

        partial void OnCalendarModeChanged(string value) =>
            OnPropertyChanged(nameof(CalendarTitle));

        partial void OnCalendarMonthChanged(int value) =>
            OnPropertyChanged(nameof(CalendarTitle));

        partial void OnCalendarYearChanged(int value) =>
            OnPropertyChanged(nameof(CalendarTitle));
    }
}
