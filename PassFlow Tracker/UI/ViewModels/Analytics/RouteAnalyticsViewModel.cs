using ClosedXML.Parser;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassFlow_Tracker.Application.Services.IPC;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Helpers;
using PassFlow_Tracker.Infrastructure.Logging;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class AnalyticsViewModel : ViewModelBase
    {
        private const string LogContext = "Analytics";
        private readonly IpcClient _backend;
        private readonly Action<string>? _setStatus;
        public bool HasReplacements => _vehicleReplacements.Count > 0;

        public ObservableCollection<RouteInfo> Routes { get; } = new();
        public ObservableCollection<RouteSchemeViewModel> SchemeStops { get; } = new();
        public ObservableCollection<DaySummary> Days { get; } = new();
        public ObservableCollection<TripSummary> Trips { get; } = new();
        public ObservableCollection<VehicleInfo> AvailableVehicles { get; } = new();
        public ObservableCollection<TripDetailViewModel> TripDetails { get; } = new();
        public ObservableCollection<DayVehicleViewModel> VehiclesForDay { get; } = new();
        public ObservableCollection<VehicleInfo> AllVehicles { get; } = new();
        public ObservableCollection<string> StopNames { get; } = new();

        private Dictionary<int, int?> _vehicleReplacements = new();

        [ObservableProperty] private RouteInfo? _selectedRoute;
        [ObservableProperty] private SchemeLevel _currentLevel = SchemeLevel.RouteAllTime;
        [ObservableProperty] private string _levelTitle = "Выберите маршрут";
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _canDrillDown;
        [ObservableProperty] private bool _canSelectRoute = true;
        [ObservableProperty] private bool _canChangeVehicle;
        [ObservableProperty] private bool _isShowingScheme = true;
        [ObservableProperty] private bool _isShowingDays;
        [ObservableProperty] private bool _isShowingTrips;
        [ObservableProperty] private bool _isShowingTripsDetail;
        [ObservableProperty] private DayVehicleViewModel? _highlightedVehicle;

        private DateOnly? _selectedDay;
        private int? _selectedTripId;

        public AnalyticsViewModel(IpcClient backend, Action<string>? setStatus = null)
        {
            _backend = backend;
            _setStatus = setStatus;
        }

        private void SetStatus(string msg)
        {
            if (_setStatus != null) _setStatus(msg);
            AppLogger.Info($"[{LogContext}] {msg}");
        }

        [RelayCommand]
        private void UnhighlightVehicle(DayVehicleViewModel? vehicle)
        {
            if (HighlightedVehicle == vehicle)
            {
                HighlightVehicle(null);
            }
        }

        [RelayCommand]
        public async Task LoadRoutesAsync()
        {
            IsBusy = true;
            SetStatus("Загрузка маршрутов...");
            try
            {
                var response = await _backend.SendAsync(new IpcRequest { Command = "distinct_routes" });
                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<RouteInfo>>(json);
                    Routes.Clear();
                    if (data != null)
                        foreach (var r in data) Routes.Add(r);
                    SetStatus($"Маршрутов: {data?.Count ?? 0}");
                }
            }
            catch (Exception ex) { SetStatus($"Ошибка: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        partial void OnSelectedRouteChanged(RouteInfo? value)
        {
            if (value != null)
            {
                _selectedDay = null;
                _selectedTripId = null;
                _ = LoadSchemeAsync(SchemeLevel.RouteAllTime);
            }
        }

        private async Task LoadSchemeAsync(SchemeLevel level)
        {
            IsBusy = true;
            CurrentLevel = level;
            SetStatus("Загрузка схемы...");

            try
            {
                string command = level switch
                {
                    SchemeLevel.RouteAllTime => "route_scheme_all",
                    SchemeLevel.Day => "route_scheme_day",
                    SchemeLevel.Trip => "route_scheme_trip",
                    _ => "route_scheme_all"
                };

                var parameters = new Dictionary<string, string>
                {
                    ["start"] = SelectedRoute!.StartPoint,
                    ["end"] = SelectedRoute!.EndPoint
                };

                if (level == SchemeLevel.Day && _selectedDay.HasValue)
                    parameters["date"] = _selectedDay.Value.ToString("yyyy-MM-dd");

                if (level == SchemeLevel.Trip && _selectedTripId.HasValue)
                    parameters["trip_id"] = _selectedTripId.Value.ToString();

                var response = await _backend.SendAsync(new IpcRequest { Command = command, Parameters = parameters });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<RouteSchemeData>>(json);
                    BuildScheme(data!);
                }

                UpdateNavigationState();
                UpdateTitle();
            }
            catch (Exception ex) { SetStatus($"Ошибка: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void BuildScheme(List<RouteSchemeData> data)
        {
            SchemeStops.Clear();
            for (int i = 0; i < data.Count; i++)
            {
                var d = data[i];
                SchemeStops.Add(new RouteSchemeViewModel
                {
                    StopNumber = d.StopNumber,
                    StopName = d.StopName,
                    Entered = d.Entered,
                    Exited = d.Exited,
                    Transported = d.Transported,
                    FillPercent = d.FillPercent,
                    LineColor = LineColorHelper.GetLineColor(d.FillPercent),
                    PreviousLineColor = i > 0
                        ? LineColorHelper.GetLineColor(data[i - 1].FillPercent)
                        : "#CBD5E1", 
                    IsFirst = i == 0,
                    IsLast = i == data.Count - 1,
                    TransportedLabel = i < data.Count - 1 ? $"~{d.Transported} чел." : "",
                    EnteredLabel = $"{d.Entered}",
                    ExitedLabel = $"{d.Exited}"
                });
            }
            ShowScheme();
        }

        private void ShowScheme() { IsShowingScheme = true; IsShowingDays = false; IsShowingTrips = false; }
        private void ShowDays() { IsShowingScheme = false; IsShowingDays = true; IsShowingTrips = false; }
        private void ShowTrips() { IsShowingScheme = false; IsShowingDays = false; IsShowingTrips = true; }

        private void UpdateNavigationState()
        {
            CanDrillDown = CurrentLevel != SchemeLevel.Trip;
            CanSelectRoute = CurrentLevel == SchemeLevel.RouteAllTime;
            CanChangeVehicle = CurrentLevel != SchemeLevel.Trip;
        }

        private void UpdateTitle()
        {
            string route = SelectedRoute?.RouteNumber ?? $"{SelectedRoute?.StartPoint} → {SelectedRoute?.EndPoint}";
            LevelTitle = CurrentLevel switch
            {
                SchemeLevel.RouteAllTime => $"Маршрут {route} — за всё время",
                SchemeLevel.Day => $"Маршрут {route} — {_selectedDay:dd.MM.yyyy}",
                SchemeLevel.Trip => $"Маршрут {route} — рейс",
                _ => "Выберите маршрут"
            };
        }

        [RelayCommand]
        private async Task DrillDown()
        {
            if (CurrentLevel == SchemeLevel.RouteAllTime) await LoadDaysAsync();
            else if (CurrentLevel == SchemeLevel.Day) await LoadTripsAsync();
        }

        [RelayCommand]
        private async Task GoBack()
        {
            if (CurrentLevel == SchemeLevel.Day) { _selectedDay = null; await LoadSchemeAsync(SchemeLevel.RouteAllTime); }
            else if (CurrentLevel == SchemeLevel.Trip) { _selectedTripId = null; await LoadSchemeAsync(SchemeLevel.Day); }
        }

        public ObservableCollection<DaySummaryViewModel> DaySummaries { get; } = new();

        private async Task LoadDaysAsync()
        {
            IsBusy = true;
            SetStatus("Загрузка дней...");
            try
            {
                var response = await _backend.SendAsync(new IpcRequest
                {
                    Command = "route_days",
                    Parameters = new()
                    {
                        ["start"] = SelectedRoute!.StartPoint,
                        ["end"] = SelectedRoute!.EndPoint
                    }
                });
                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<DaySummary>>(json);
                    DaySummaries.Clear();
                    if (data != null)
                        foreach (var d in data)
                            DaySummaries.Add(new DaySummaryViewModel(d));
                    ShowDays();
                    SetStatus($"Дней: {data?.Count ?? 0}");
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void ToggleDay(DaySummaryViewModel? day)
        {
            if (day != null) day.IsExpanded = !day.IsExpanded;
        }

        private async Task LoadTripsAsync()
        {
            IsBusy = true;
            SetStatus("Загрузка рейсов...");
            try
            {
                var response = await _backend.SendAsync(new IpcRequest
                {
                    Command = "route_trips",
                    Parameters = new()
                    {
                        ["start"] = SelectedRoute!.StartPoint,
                        ["end"] = SelectedRoute!.EndPoint,
                        ["date"] = _selectedDay!.Value.ToString("yyyy-MM-dd")
                    }
                });
                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<TripSummary>>(json);
                    Trips.Clear();
                    if (data != null) foreach (var t in data) Trips.Add(t);
                    ShowTrips();
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SelectTrip(TripSummary? trip)
        {
            if (trip == null) return;
            _selectedTripId = trip.TripId;
            await LoadSchemeAsync(SchemeLevel.Trip);
        }

        [RelayCommand]
        private async Task OpenDayDetail(DaySummaryViewModel? day)
        {
            if (day == null) return;
            _selectedDay = DateOnly.ParseExact(day.Date, "dd.MM.yyyy");
            await LoadTripDetailsAsync();
        }

        [RelayCommand]
        private void HighlightVehicle(DayVehicleViewModel? vehicle)
        {
            if (HighlightedVehicle != null)
                HighlightedVehicle.IsHighlighted = false;

            HighlightedVehicle = vehicle;

            if (vehicle != null)
            {
                vehicle.IsHighlighted = true;
                foreach (var trip in TripDetails)
                    trip.IsHighlighted = trip.VehicleName == vehicle.VehicleName;
            }
            else
            {
                foreach (var trip in TripDetails)
                    trip.IsHighlighted = false;
            }
        }

        private async Task LoadTripDetailsAsync()
        {
            IsBusy = true;
            SetStatus("Загрузка рейсов...");

            try
            {
                var response = await _backend.SendAsync(new IpcRequest
                {
                    Command = "route_trips_detailed",
                    Parameters = new()
                    {
                        ["start"] = SelectedRoute!.StartPoint,
                        ["end"] = SelectedRoute!.EndPoint,
                        ["date"] = _selectedDay!.Value.ToString("yyyy-MM-dd")
                    }
                });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<TripDetailSummary>>(json);

                    TripDetails.Clear();
                    if (data != null && data.Count > 0)
                    {
                        StopNames.Clear();
                        if (data != null && data.Count > 0)
                        {
                            foreach (var name in data[0].Stops.Select(s => s.StopName))
                                StopNames.Add(name);
                        }

                        foreach (var t in data)
                            TripDetails.Add(new TripDetailViewModel(t));
                    }

                    var vehicleDict = new Dictionary<int, DayVehicleViewModel>();
                    foreach (var trip in data!)
                    {
                        if (!vehicleDict.ContainsKey(trip.VehicleName.GetHashCode()))
                        {
                            vehicleDict[trip.VehicleName.GetHashCode()] = new DayVehicleViewModel
                            {
                                VehicleId = trip.TripId, 
                                VehicleName = trip.VehicleName,
                                ModelName = trip.ModelName,
                                Seats = trip.Seats,
                                Capacity = trip.Capacity,
                                TripCount = 1
                            };
                        }
                        else
                        {
                            vehicleDict[trip.VehicleName.GetHashCode()].TripCount++;
                        }
                    }

                    VehiclesForDay.Clear();
                    foreach (var v in vehicleDict.Values)
                        VehiclesForDay.Add(v);

                    await LoadAllVehiclesAsync();

                    ShowTripsDetail();
                }
            }
            finally { IsBusy = false; }
        }

        private async Task LoadAllVehiclesAsync()
        {
            try
            {
                var response = await _backend.SendAsync(new IpcRequest { Command = "vehicles" });

                if (response.Success && response.Data != null)
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    var data = JsonSerializer.Deserialize<List<VehicleInfo>>(json);

                    AllVehicles.Clear();
                    if (data != null)
                        foreach (var v in data)
                            AllVehicles.Add(v);

                    AppLogger.Info($"[{LogContext}] Загружено машин: {AllVehicles.Count}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка загрузки машин: {ex.Message}");
            }
        }

        private void ShowTripsDetail()
        {
            IsShowingScheme = false;
            IsShowingDays = false;
            IsShowingTrips = false;
            IsShowingTripsDetail = true;
        }

        [RelayCommand]
        private async Task ReplaceVehicle(DayVehicleViewModel? vehicle)
        {
            if (vehicle?.SelectedReplacement == null) return;

            if (!vehicle.IsReplaced)
            {
                vehicle.OriginalVehicleName = vehicle.VehicleName;
                vehicle.OriginalModelName = vehicle.ModelName;
                vehicle.OriginalCapacity = vehicle.Capacity;
            }

            vehicle.IsReplaced = true;
            vehicle.VehicleName = vehicle.SelectedReplacement.UnitName;
            vehicle.ModelName = vehicle.SelectedReplacement.ModelName;
            vehicle.Capacity = vehicle.SelectedReplacement.Capacity;

            _vehicleReplacements[vehicle.VehicleId] = vehicle.SelectedReplacement.Id;

            UpdateTripDisplayForVehicle(vehicle);

            OnPropertyChanged(nameof(HasReplacements));

            await ReloadSchemesWithReplacements();
        }

        [RelayCommand]
        private async Task ResetVehicle(DayVehicleViewModel? vehicle)
        {
            if (vehicle == null || !vehicle.IsReplaced) return;

            vehicle.IsReplaced = false;
            vehicle.VehicleName = vehicle.OriginalVehicleName;
            vehicle.ModelName = vehicle.OriginalModelName;
            vehicle.Capacity = vehicle.OriginalCapacity;
            vehicle.SelectedReplacement = null;

            _vehicleReplacements.Remove(vehicle.VehicleId);

            UpdateTripDisplayForVehicle(vehicle);
            await ReloadSchemesWithReplacements();
            OnPropertyChanged(nameof(HasReplacements));
        }

        [RelayCommand]
        private async Task ResetAllVehicles()
        {
            foreach (var vehicle in VehiclesForDay)
            {
                if (vehicle.IsReplaced)
                {
                    vehicle.IsReplaced = false;
                    vehicle.VehicleName = vehicle.OriginalVehicleName;
                    vehicle.ModelName = vehicle.OriginalModelName;
                    vehicle.Capacity = vehicle.OriginalCapacity;
                    vehicle.SelectedReplacement = null;
                    UpdateTripDisplayForVehicle(vehicle);
                }
            }

            _vehicleReplacements.Clear();
            OnPropertyChanged(nameof(HasReplacements));
            await ReloadSchemesWithReplacements();
        }

        private void UpdateTripDisplayForVehicle(DayVehicleViewModel vehicle)
        {
            foreach (var trip in TripDetails)
            {
                if (trip.VehicleName == vehicle.OriginalVehicleName ||
                    trip.VehicleName == vehicle.VehicleName)
                {
                    trip.VehicleName = vehicle.VehicleName;
                    trip.ModelName = vehicle.ModelName;
                    trip.Capacity = vehicle.Capacity;
                    trip.IsReplaced = vehicle.IsReplaced;
                }
            }
        }

        private async Task ReloadSchemesWithReplacements()
        {
            foreach (var trip in TripDetails)
            {
                var replacementVehicleId = _vehicleReplacements
                    .FirstOrDefault(r => r.Key == trip.TripId || r.Value.HasValue).Value;

                if (replacementVehicleId.HasValue)
                {
                    var response = await _backend.SendAsync(new IpcRequest
                    {
                        Command = "route_scheme_trip",
                        Parameters = new()
                        {
                            ["trip_id"] = trip.TripId.ToString(),
                            ["vehicle_id"] = replacementVehicleId.Value.ToString()
                        }
                    });

                    if (response.Success && response.Data != null)
                    {
                        var json = JsonSerializer.Serialize(response.Data);
                        var data = JsonSerializer.Deserialize<List<RouteSchemeData>>(json);
                        UpdateTripStops(trip, data!);
                    }
                }
                else
                {
                    var response = await _backend.SendAsync(new IpcRequest
                    {
                        Command = "route_scheme_trip",
                        Parameters = new() { ["trip_id"] = trip.TripId.ToString() }
                    });

                    if (response.Success && response.Data != null)
                    {
                        var json = JsonSerializer.Serialize(response.Data);
                        var data = JsonSerializer.Deserialize<List<RouteSchemeData>>(json);
                        UpdateTripStops(trip, data!);
                    }
                }
            }
        }

        private void UpdateTripStops(TripDetailViewModel trip, List<RouteSchemeData> data)
        {
            var oldTimes = trip.Stops.Select(s => new { s.TimeFrom, s.TimeTo }).ToList();

            trip.Stops.Clear();
            for (int i = 0; i < data.Count; i++)
            {
                var s = data[i];
                var timeFrom = i < oldTimes.Count ? oldTimes[i].TimeFrom : "";
                var timeTo = i < oldTimes.Count ? oldTimes[i].TimeTo : "";

                trip.Stops.Add(new TripStopDetailViewModel
                {
                    StopNumber = s.StopNumber,
                    StopName = s.StopName,
                    TimeFrom = timeFrom,
                    TimeTo = timeTo,
                    Entered = s.Entered,
                    Exited = s.Exited,
                    Transported = s.Transported,
                    FillPercent = s.FillPercent,
                    LineColor = LineColorHelper.GetLineColor(s.FillPercent),
                    PreviousLineColor = i > 0
                        ? LineColorHelper.GetLineColor(trip.Stops[i - 1].FillPercent)
                        : "#CBD5E1",
                    IsFirst = i == 0,
                    IsLast = i == data.Count - 1
                });
            }
        }
    }
}