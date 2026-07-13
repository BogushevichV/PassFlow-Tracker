using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class CalendarDayItemViewModel : ObservableObject
    {
        public int DayNumber { get; init; }
        public DateOnly? Date { get; init; }
        public bool IsEmpty => Date == null;
        public bool IsSaturday => Date?.DayOfWeek == DayOfWeek.Saturday;
        public bool IsSunday => Date?.DayOfWeek == DayOfWeek.Sunday;

        [ObservableProperty]
        private bool isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Foreground));
            OnPropertyChanged(nameof(FontWeight));
        }

        public IRelayCommand? ToggleCommand { get; set; }

        public IBrush Background => IsSelected
            ? new SolidColorBrush(Color.Parse("#DBEAFE"))
            : Brushes.Transparent;

        public IBrush Foreground
        {
            get
            {
                if (IsSelected) return new SolidColorBrush(Color.Parse("#1D4ED8"));
                if (IsSunday) return new SolidColorBrush(Color.Parse("#EF4444"));
                if (IsSaturday) return new SolidColorBrush(Color.Parse("#64748B"));
                return new SolidColorBrush(Color.Parse("#334155"));
            }
        }

        public FontWeight FontWeight => IsSelected ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public partial class CalendarMonthItemViewModel : ObservableObject
    {
        public int MonthNumber { get; init; }
        public string ShortName { get; init; } = "";

        [ObservableProperty]
        private bool isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Foreground));
            OnPropertyChanged(nameof(FontWeight));
        }

        public IRelayCommand? ToggleCommand { get; set; }

        public IBrush Background => IsSelected
            ? new SolidColorBrush(Color.Parse("#DBEAFE"))
            : Brushes.Transparent;

        public IBrush Foreground => IsSelected
            ? new SolidColorBrush(Color.Parse("#1D4ED8"))
            : new SolidColorBrush(Color.Parse("#334155"));

        public FontWeight FontWeight => IsSelected ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public partial class CalendarWeekRowViewModel : ObservableObject
    {
        public CalendarDayItemViewModel[] Days { get; } = new CalendarDayItemViewModel[7];
    }
}
