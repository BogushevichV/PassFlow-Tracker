using CommunityToolkit.Mvvm.ComponentModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    /// <summary>Один столбец гистограммы часов пик.</summary>
    public partial class PeakHourBarViewModel : ViewModelBase
    {
        [ObservableProperty] private int    _hour;
        [ObservableProperty] private long   _flow;
        [ObservableProperty] private double _heightRatio; // 0..1 относительно максимума
        [ObservableProperty] private bool   _isPeak;

        public string HourLabel => $"{Hour:D2}";
        public string FlowLabel => Flow.ToString();
    }
}
