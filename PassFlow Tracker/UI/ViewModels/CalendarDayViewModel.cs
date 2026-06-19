using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class CalendarDayViewModel : ViewModelBase
    {
        [ObservableProperty] private int _day;
        [ObservableProperty] private bool _isCurrentMonth;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isInRange;
        [ObservableProperty] private bool _isRangeStart;
        [ObservableProperty] private bool _isRangeEnd;
        [ObservableProperty] private bool _isToday;
        [ObservableProperty] private bool _hasFlowData;
        [ObservableProperty] private double _flowIntensity;
        [ObservableProperty] private long _flow;

        public DateOnly? Date { get; set; }

        public string BorderColor => IsRangeStart || IsRangeEnd ? "#b52168" : "Transparent";

        public string BackgroundColor
        {
            get
            {
                if (HasFlowData)
                {
                    byte r = (byte)(219 - FlowIntensity * 160); 
                    byte g = (byte)(234 - FlowIntensity * 104); 
                    byte b = (byte)(254 - FlowIntensity * 15);   
                    return $"#{r:X2}{g:X2}{b:X2}";
                }

                if (IsInRange) return "#fffce3";
                if (IsToday) return "#F0F9FF"; 
                return "Transparent";
            }
        }

        public string TextColor
        {
            get
            {
                if (FlowIntensity > 0.6)
                    return "#FFFFFF";
                return IsCurrentMonth ? "#334155" : "#CBD5E1";
            }
        }
    }
}
