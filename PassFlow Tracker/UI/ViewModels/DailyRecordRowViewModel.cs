using CommunityToolkit.Mvvm.ComponentModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class DailyRecordRowViewModel : ViewModelBase
    {
        [ObservableProperty] private string _unitName = string.Empty;
        [ObservableProperty] private string _recordDate = string.Empty;
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
    }
}
