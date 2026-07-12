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
    public partial class DaySummaryViewModel : ViewModelBase
    {
        [ObservableProperty] private string _date = "";
        [ObservableProperty] private int _tripCount;
        [ObservableProperty] private bool _isExpanded;

        public ObservableCollection<DayVehicleViewModel> Vehicles { get; } = new();
        public ObservableCollection<RouteSchemeViewModel> Stops { get; } = new();

        public DaySummaryViewModel(DaySummary day)
        {
            Date = day.Date;
            TripCount = day.TripCount;

            foreach (var v in day.Vehicles)
                Vehicles.Add(new DayVehicleViewModel
                {
                    VehicleId = v.VehicleId,
                    VehicleName = v.VehicleName,
                    ModelName = v.ModelName,
                    Seats = v.Seats,
                    Capacity = v.Capacity,
                    TripCount = v.TripCount
                });

            foreach (var s in day.Stops)
                Stops.Add(new RouteSchemeViewModel
                {
                    StopNumber = s.StopNumber,
                    StopName = s.StopName,
                    Entered = s.Entered,
                    Exited = s.Exited,
                    Transported = s.Transported,
                    FillPercent = s.FillPercent,
                    LineColor = LineColorHelper.GetLineColor(s.FillPercent),
                    IsFirst = s.StopNumber == day.Stops[0].StopNumber,
                    IsLast = s.StopNumber == day.Stops[^1].StopNumber,
                    TransportedLabel = $"~{s.Transported}",
                    EnteredLabel = $"{s.Entered}",
                    ExitedLabel = $"{s.Exited}"
                });
        }
    }

    public partial class DayVehicleViewModel : ViewModelBase
    {
        [ObservableProperty] private int _vehicleId;
        [ObservableProperty] private string _vehicleName = "";
        [ObservableProperty] private string _modelName = "";
        [ObservableProperty] private int _seats;
        [ObservableProperty] private int _capacity;
        [ObservableProperty] private int _tripCount;
        [ObservableProperty] private bool _isHighlighted;

        [ObservableProperty] private bool _isReplaced;
        [ObservableProperty] private string _originalVehicleName = "";
        [ObservableProperty] private string _originalModelName = "";
        [ObservableProperty] private int _originalCapacity;
        [ObservableProperty] private VehicleInfo? _selectedReplacement;
    }
}
