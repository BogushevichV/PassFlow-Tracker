using CommunityToolkit.Mvvm.ComponentModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class RoundRowViewModel : ViewModelBase
    {
        [ObservableProperty] private string _unitName = string.Empty;
        [ObservableProperty] private string _startPoint = string.Empty;
        [ObservableProperty] private string _endPoint = string.Empty;
        [ObservableProperty] private string _timeFrom = string.Empty;
        [ObservableProperty] private string _timeTo = string.Empty;
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
    }
}
