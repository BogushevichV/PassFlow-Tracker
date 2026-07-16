using CommunityToolkit.Mvvm.ComponentModel;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Infrastructure.Helpers;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class TripDetailViewModel : ViewModelBase
    {
        [ObservableProperty] private int _tripId;
        [ObservableProperty] private string _timeFrom = "";
        [ObservableProperty] private string _timeTo = "";
        [ObservableProperty] private string _vehicleName = "";
        [ObservableProperty] private string _modelName = "";
        [ObservableProperty] private int _capacity;
        [ObservableProperty] private bool _isHighlighted;
        [ObservableProperty] private bool _isReplaced;

        public ObservableCollection<TripStopDetailViewModel> Stops { get; } = new();

        public TripDetailViewModel(TripDetailSummary trip)
        {
            TripId = trip.TripId;
            TimeFrom = trip.TimeFrom;
            TimeTo = trip.TimeTo;
            VehicleName = trip.VehicleName;
            ModelName = trip.ModelName;
            Capacity = trip.Capacity;

            for (int i = 0; i < trip.Stops.Count; i++)
            {
                var s = trip.Stops[i];
                var fillPercent = s.FillPercent;

                Stops.Add(new TripStopDetailViewModel
                {
                    StopNumber = s.StopNumber,
                    StopName = s.StopName,
                    TimeFrom = s.TimeFrom,
                    TimeTo = s.TimeTo,
                    Entered = s.Entered,
                    Exited = s.Exited,
                    Transported = s.Transported,
                    FillPercent = fillPercent,
                    LineColor = LineColorHelper.GetLineColor(fillPercent),
                    PreviousLineColor = i > 0
                        ? LineColorHelper.GetLineColor(trip.Stops[i - 1].FillPercent)
                        : "#CBD5E1", 
                    IsFirst = i == 0,
                    IsLast = i == trip.Stops.Count - 1,
                    EnteredLabel = s.Entered > 0 ? $"+{s.Entered}" : s.Entered.ToString(),
                    ExitedLabel = s.Exited > 0 ? $"-{s.Exited}" : s.Exited.ToString()
                });
            }
        }
    }

    public partial class TripStopDetailViewModel : ViewModelBase
    {
        [ObservableProperty] private int _stopNumber;
        [ObservableProperty] private string _stopName = "";
        [ObservableProperty] private string _timeFrom = "";
        [ObservableProperty] private string _timeTo = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
        [ObservableProperty] private double _fillPercent; 
        [ObservableProperty] private string _previousLineColor = "#CBD5E1";
        [ObservableProperty] private string _lineColor = "#94A3B8";
        [ObservableProperty] private bool _isFirst;
        [ObservableProperty] private bool _isLast;
        [ObservableProperty] private string _enteredLabel = "";
        [ObservableProperty] private string _exitedLabel = "";
    }
}
