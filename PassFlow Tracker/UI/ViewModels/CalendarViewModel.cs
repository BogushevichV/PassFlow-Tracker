using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class CalendarViewModel : ViewModelBase
    {
        public ObservableCollection<CalendarDayViewModel> CalendarDays { get; } = new();

        public static string[] DayOfWeekHeaders { get; } = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

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
            BuildCalendar();
            OnPropertyChanged(nameof(CalendarTitle));
        }

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

        public bool HasActiveFilter => FilterStartDate != null || FilterEndDate != null;

        public CalendarViewModel(Action? onFilterApplied = null)
        {
            _onFilterApplied = onFilterApplied;
            BuildCalendar();
        }

        private void BuildCalendar()
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
                {
                    intensity = (flow - _minFlow) / (double)(_maxFlow - _minFlow);
                }
                else if (hasFlow && _maxFlow > 0)
                {
                    intensity = 1.0; 
                }

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

            BuildCalendar();
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            if (_pendingStartDate != null && _pendingEndDate != null)
            {
                FilterStartDate = _pendingStartDate;
                FilterEndDate = _pendingEndDate;
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
            IsFilterApplied = false;
            ShowCalendar = false;

            _flowByDate.Clear();
            _maxFlow = 0;
            _minFlow = 0;

            BuildCalendar();
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
            else if (CalendarMode == "months")
            {
                CalendarYear--;
            }
            else
            {
                CalendarYear -= 12;
            }
        }

        [RelayCommand]
        private void CalendarNext()
        {
            if (CalendarMode == "days")
            {
                CalendarMonth++;
                if (CalendarMonth > 12) { CalendarMonth = 1; CalendarYear++; }
            }
            else if (CalendarMode == "months")
            {
                CalendarYear++;
            }
            else
            {
                CalendarYear += 12;
            }
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
            if (_flowLoader == null || FilterStartDate == null || FilterEndDate == null)
                return;

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
