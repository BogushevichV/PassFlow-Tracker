using CommunityToolkit.Mvvm.ComponentModel;
using PassFlow_Tracker.UI.ViewModels.Core;
using System.Collections.ObjectModel;

namespace PassFlow_Tracker.UI.ViewModels
{
    // Узел остановки (листовой)
    public partial class StopNodeViewModel : ViewModelBase
    {
        [ObservableProperty] private int _stopNumber;
        [ObservableProperty] private string _stopName = "";
        [ObservableProperty] private bool _isDuplicate;
        [ObservableProperty] private bool _isSkipped;
        [ObservableProperty] private string _timeFrom = "";
        [ObservableProperty] private string _timeTo = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;

        public string Label =>
            $"#{StopNumber} {StopName}  {TimeFrom}–{TimeTo}" +
            $"  ↑{Entered} ↓{Exited}" +
            (IsDuplicate ? "  [дубль]" : "") +
            (IsSkipped   ? "  [пропуск]" : "");
    }

    // Узел рейса
    public partial class TripNodeViewModel : ViewModelBase
    {
        [ObservableProperty] private string _startPoint = "";
        [ObservableProperty] private string _endPoint = "";
        [ObservableProperty] private string _timeFrom = "";
        [ObservableProperty] private string _timeTo = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
        [ObservableProperty] private bool _isExpanded;

        public ObservableCollection<StopNodeViewModel> Stops { get; } = new();

        public string Label =>
            $"Рейс {StartPoint} → {EndPoint}  {TimeFrom}–{TimeTo}" +
            $"  ↑{Entered} ↓{Exited}  в салоне: {Transported}";
    }

    // Узел круга
    public partial class RoundNodeViewModel : ViewModelBase
    {
        [ObservableProperty] private string _startPoint = "";
        [ObservableProperty] private string _endPoint = "";
        [ObservableProperty] private string _timeFrom = "";
        [ObservableProperty] private string _timeTo = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
        [ObservableProperty] private bool _isExpanded;

        public ObservableCollection<TripNodeViewModel> Trips { get; } = new();

        public string Label =>
            $"Круг {StartPoint} → {EndPoint}  {TimeFrom}–{TimeTo}" +
            $"  ↑{Entered} ↓{Exited}  перевезено: {Transported}";
    }

    // Узел дня (корневой)
    public partial class DayNodeViewModel : ViewModelBase
    {
        [ObservableProperty] private string _unitName = "";
        [ObservableProperty] private string _recordDate = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
        [ObservableProperty] private bool _isExpanded;

        public ObservableCollection<RoundNodeViewModel> Rounds { get; } = new();

        public string Label =>
            $"{UnitName}  {RecordDate}  ↑{Entered} ↓{Exited}  перевезено: {Transported}";
    }
}
