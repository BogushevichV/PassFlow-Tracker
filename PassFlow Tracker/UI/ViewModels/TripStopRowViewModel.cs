using CommunityToolkit.Mvvm.ComponentModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class TripStopRowViewModel : ViewModelBase
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private int _stopNumber;
        [ObservableProperty] private string _stopName = string.Empty;
        [ObservableProperty] private string _timeFrom = string.Empty;
        [ObservableProperty] private string _timeTo = string.Empty;
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
    }
}
