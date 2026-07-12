using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    // ViewModel одного месяца в режиме "Месяцы"
    public partial class CalendarMonthViewModel : ViewModelBase
    {
        public int Month { get; init; }
        public string Name { get; init; } = "";

        [ObservableProperty] private bool _isSelected;
        partial void OnIsSelectedChanged(bool _)
        {
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Foreground));
        }

        public string Background => IsSelected ? "#DBEAFE" : "Transparent";
        public string Foreground => IsSelected ? "#1D4ED8" : "#334155";
    }

    // ViewModel одного года в режиме "Годы"
    public partial class CalendarYearViewModel : ViewModelBase
    {
        public int Year { get; init; }

        [ObservableProperty] private bool _isSelected;
        partial void OnIsSelectedChanged(bool _)
        {
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Foreground));
        }

        public string Background => IsSelected ? "#DBEAFE" : "Transparent";
        public string Foreground => IsSelected ? "#1D4ED8" : "#334155";
    }

    public partial class CalendarViewModel : ViewModelBase
    {
        public ObservableCollection<CalendarDayViewModel>   CalendarDays   { get; } = new();
        public ObservableCollection<CalendarMonthViewModel> CalendarMonths { get; } = new();
        public ObservableCollection<CalendarYearViewModel>  CalendarYears  { get; } = new();

        public static string[] DayOfWeekHeaders { get; } = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

        private static readonly string[] _monthNames =
        {
            "Янв", "Фев", "Мар", "Апр", "Май", "Июн",
            "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"
        };

        [ObservableProperty]
        private int _calendarYear = DateTime.Now.Year;
        partial void OnCalendarYearChanged(int value)
        {
            BuildCalendar();
            OnPropertyChanged(nameof(CalendarTitle));
        }

        [ObservableProperty]
        private int _calendarMonth = DateTime.Now.Month;
        partial void OnCalendarMonthChanged(int value)
        {
            BuildCalendar();
            OnPropertyChanged(nameof(CalendarTitle));
        }

        [ObservableProperty]
        private string _calendarMode = "days";
        partial void OnCalendarModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsDaysMode));
            OnPropertyChanged(nameof(IsMonthsMode));
            OnPropertyChanged(nameof(IsYearsMode));
            BuildCalendar();
            OnPropertyChanged(nameof(CalendarTitle));
        }

        public bool IsDaysMode   => CalendarMode == "days";
        public bool IsMonthsMode => CalendarMode == "months";
        public bool IsYearsMode  => CalendarMode == "years";

        [ObservableProperty]
        private DateOnly? _filterStartDate;

        [ObservableProperty]
        private DateOnly? _filterEndDate;

        [ObservableProperty]
        private bool _showCalendar;

        private DateOnly? _selectingStart;
        private bool _isSelectingRange;

        [ObservableProperty]
        private bool _isFilterApplied;
        
        private DateOnly? _pendingStartDate;
        private DateOnly? _pendingEndDate;

        // Выбранные месяцы (год, месяц) — для режима "Месяцы"
        private readonly HashSet<(int Year, int Month)> _selectedMonths = new();
        // Выбранные годы — для режима "Годы"
        private readonly HashSet<int> _selectedYears = new();

        private readonly Action? _onFilterApplied;

        public string CalendarTitle
        {
            get
            {
                string[] months =
                {
                    "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
                    "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
                };

                if (CalendarMode == "days")
                    return $"{months[CalendarMonth - 1]} {CalendarYear}";
                if (CalendarMode == "months")
                    return $"{CalendarYear}";
                return $"{CalendarYear - 5} – {CalendarYear + 6}";
            }
        }

        public bool HasActiveFilter => FilterStartDate != null || FilterEndDate != null
                                    || _selectedMonths.Count > 0
                                    || _selectedYears.Count > 0;

        public CalendarViewModel(Action? onFilterApplied = null)
        {
            _onFilterApplied = onFilterApplied;
            BuildCalendar();
        }

        private void BuildCalendar()
        {
            if (CalendarMode == "months")      BuildMonths();
            else if (CalendarMode == "years")  BuildYears();
            else                               BuildDays();
        }

        private void BuildDays()
        {
            CalendarDays.Clear();

            var firstDay = new DateOnly(CalendarYear, CalendarMonth, 1);
            int daysInMonth = DateTime.DaysInMonth(CalendarYear, CalendarMonth);
            var today = DateOnly.FromDateTime(DateTime.Now);

            int startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7;

            for (int i = 0; i < startDayOfWeek; i++)
            {
                CalendarDays.Add(new CalendarDayViewModel
                {
                    Day = 0,
                    IsCurrentMonth = false,
                    Date = null
                });
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(CalendarYear, CalendarMonth, day);

                bool hasFlow = _flowByDate.TryGetValue(date, out var flow);
                double intensity = 0;
                if (hasFlow && _maxFlow > _minFlow)
                    intensity = (flow - _minFlow) / (double)(_maxFlow - _minFlow);
                else if (hasFlow && _maxFlow > 0)
                    intensity = 1.0;

                CalendarDays.Add(new CalendarDayViewModel
                {
                    Day = day,
                    IsCurrentMonth = true,
                    IsToday = date == today,
                    Date = date,
                    IsSelected = date == FilterStartDate || date == FilterEndDate,
                    IsRangeStart = date == FilterStartDate,
                    IsRangeEnd = date == FilterEndDate && FilterEndDate != FilterStartDate,
                    IsInRange = IsDateInRange(date),
                    HasFlowData = hasFlow,
                    Flow = hasFlow ? flow : 0,
                    FlowIntensity = intensity
                });
            }

            while (CalendarDays.Count < 42)
            {
                CalendarDays.Add(new CalendarDayViewModel
                {
                    Day = 0,
                    IsCurrentMonth = false,
                    Date = null
                });
            }
        }

        private void BuildMonths()
        {
            CalendarMonths.Clear();
            for (int m = 1; m <= 12; m++)
            {
                CalendarMonths.Add(new CalendarMonthViewModel
                {
                    Month = m,
                    Name = _monthNames[m - 1],
                    IsSelected = _selectedMonths.Contains((CalendarYear, m))
                });
            }
        }

        private void BuildYears()
        {
            CalendarYears.Clear();
            int start = CalendarYear - 5;
            int end   = CalendarYear + 6;
            for (int y = start; y <= end; y++)
            {
                CalendarYears.Add(new CalendarYearViewModel
                {
                    Year = y,
                    IsSelected = _selectedYears.Contains(y)
                });
            }
        }

        private bool IsDateInRange(DateOnly date)
        {
            if (FilterStartDate == null || FilterEndDate == null) return false;
            return date >= FilterStartDate.Value && date <= FilterEndDate.Value;
        }

        [RelayCommand]
        private async Task SelectCalendarDay(CalendarDayViewModel? day)
        {
            if (day?.Date == null || !day.IsCurrentMonth) return;

            if (!_isSelectingRange || _selectingStart == null)
            {
                _selectingStart = day.Date;
                _isSelectingRange = true;
                FilterStartDate = day.Date;
                FilterEndDate = null;
            }
            else
            {
                if (day.Date.Value < _selectingStart.Value)
                {
                    FilterEndDate = _selectingStart;
                    FilterStartDate = day.Date;
                }
                else
                {
                    FilterEndDate = day.Date;
                }

                _isSelectingRange = false;
                _selectingStart = null;

                _pendingStartDate = FilterStartDate;
                _pendingEndDate = FilterEndDate;

                await LoadPassengerFlowForRange();
            }

            BuildDays();
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        [RelayCommand]
        private void ToggleMonth(CalendarMonthViewModel? month)
        {
            if (month == null) return;
            var key = (CalendarYear, month.Month);
            if (_selectedMonths.Contains(key))
                _selectedMonths.Remove(key);
            else
                _selectedMonths.Add(key);

            month.IsSelected = _selectedMonths.Contains(key);
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        [RelayCommand]
        private void ToggleYear(CalendarYearViewModel? year)
        {
            if (year == null) return;
            if (_selectedYears.Contains(year.Year))
                _selectedYears.Remove(year.Year);
            else
                _selectedYears.Add(year.Year);

            year.IsSelected = _selectedYears.Contains(year.Year);
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            if (CalendarMode == "months" && _selectedMonths.Count > 0)
            {
                // Преобразуем выбранные месяцы в диапазон дат
                var sorted = _selectedMonths.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
                var first = sorted.First();
                var last  = sorted.Last();
                FilterStartDate = new DateOnly(first.Year, first.Month, 1);
                var lastDays = DateTime.DaysInMonth(last.Year, last.Month);
                FilterEndDate = new DateOnly(last.Year, last.Month, lastDays);
                _pendingStartDate = FilterStartDate;
                _pendingEndDate   = FilterEndDate;
                IsFilterApplied = true;
                ShowCalendar = false;
                _onFilterApplied?.Invoke();
            }
            else if (CalendarMode == "years" && _selectedYears.Count > 0)
            {
                var minYear = _selectedYears.Min();
                var maxYear = _selectedYears.Max();
                FilterStartDate = new DateOnly(minYear, 1, 1);
                FilterEndDate   = new DateOnly(maxYear, 12, 31);
                _pendingStartDate = FilterStartDate;
                _pendingEndDate   = FilterEndDate;
                IsFilterApplied = true;
                ShowCalendar = false;
                _onFilterApplied?.Invoke();
            }
            else if (_pendingStartDate != null && _pendingEndDate != null)
            {
                FilterStartDate = _pendingStartDate;
                FilterEndDate   = _pendingEndDate;
                IsFilterApplied = true;
                ShowCalendar = false;
                _onFilterApplied?.Invoke();
            }
        }

        [RelayCommand]
        private void ResetDateFilter()
        {
            FilterStartDate = null;
            FilterEndDate = null;
            _pendingStartDate = null;
            _pendingEndDate = null;
            _isSelectingRange = false;
            _selectingStart = null;
            _selectedMonths.Clear();
            _selectedYears.Clear();
            IsFilterApplied = false;
            ShowCalendar = false;

            _flowByDate.Clear();
            _maxFlow = 0;
            _minFlow = 0;

            BuildCalendar();
            OnPropertyChanged(nameof(HasActiveFilter));
            _onFilterApplied?.Invoke();
        }

        [RelayCommand]
        private void ToggleCalendar() => ShowCalendar = !ShowCalendar;

        [RelayCommand]
        private void SetCalendarMode(string mode) => CalendarMode = mode;

        [RelayCommand]
        private void CalendarPrev()
        {
            if (CalendarMode == "days")
            {
                CalendarMonth--;
                if (CalendarMonth < 1) { CalendarMonth = 12; CalendarYear--; }
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
                if (CalendarMonth > 12) { CalendarMonth = 1; CalendarYear++; }
            }
            else if (CalendarMode == "months") CalendarYear++;
            else CalendarYear += 12;
        }

        private Dictionary<DateOnly, long> _flowByDate = new();
        private long _maxFlow;
        private long _minFlow;

        private readonly Func<DateOnly, DateOnly, Task<Dictionary<DateOnly, long>>>? _flowLoader;

        public CalendarViewModel(
            Action? onFilterApplied = null,
            Func<DateOnly, DateOnly, Task<Dictionary<DateOnly, long>>>? flowLoader = null)
        {
            _onFilterApplied = onFilterApplied;
            _flowLoader = flowLoader;
            BuildCalendar();
        }

        private async Task LoadPassengerFlowForRange()
        {
            if (_flowLoader == null || FilterStartDate == null || FilterEndDate == null) return;

            _flowByDate = await _flowLoader(FilterStartDate.Value, FilterEndDate.Value);

            if (_flowByDate.Count > 0)
            {
                _maxFlow = _flowByDate.Values.Max();
                _minFlow = _flowByDate.Values.Min();
            }

            foreach (var day in CalendarDays)
            {
                if (day.Date != null && _flowByDate.TryGetValue(day.Date.Value, out var flow))
                {
                    day.Flow = flow;
                    day.HasFlowData = true;
                    day.FlowIntensity = _maxFlow > 0
                        ? (flow - _minFlow) / (double)(_maxFlow - _minFlow)
                        : 0;
                }
                else
                {
                    day.HasFlowData = false;
                    day.FlowIntensity = 0;
                }
            }
        }

        [RelayCommand]
        private void CloseCalendar()
        {
            ShowCalendar = false;
            _isSelectingRange = false;
            _selectingStart = null;
        }
    }
}

          