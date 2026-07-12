using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class TripStopRowViewModel : TrackedViewModel
    {
        public int TripStopId { get; set; }

        private int _originalStopNumber;
        private string _originalStopName = string.Empty;
        private string _originalTimeFrom = string.Empty;
        private string _originalTimeTo = string.Empty;
        private int _originalEntered;
        private int _originalExited;
        private int _originalTransported;

        [ObservableProperty]
        private int _stopNumber;
        partial void OnStopNumberChanged(int value) { if (value != _originalStopNumber) MarkDirty(nameof(StopNumber)); }

        [ObservableProperty]
        private string _stopName = string.Empty;
        partial void OnStopNameChanged(string value) { if (value != _originalStopName) { MarkDirty(nameof(StopName)); } }

        [ObservableProperty]
        private string _period = string.Empty;

        [ObservableProperty]
        private string _routeName = string.Empty;

        [ObservableProperty]
        private string _timeFrom = string.Empty;
        partial void OnTimeFromChanged(string value) { if (value != _originalTimeFrom) MarkDirty(nameof(TimeFrom)); }

        [ObservableProperty]
        private string _timeTo = string.Empty;
        partial void OnTimeToChanged(string value) { if (value != _originalTimeTo) MarkDirty(nameof(TimeTo)); }

        [ObservableProperty]
        private int _entered;
        partial void OnEnteredChanged(int value) { if (value != _originalEntered) MarkDirty(nameof(Entered)); }

        [ObservableProperty]
        private int _exited;
        partial void OnExitedChanged(int value) { if (value != _originalExited) MarkDirty(nameof(Exited)); }

        [ObservableProperty]
        private int _transported;
        partial void OnTransportedChanged(int value) { if (value != _originalTransported) MarkDirty(nameof(Transported)); }

        public void SetOriginalValues(int id, int stopNumber, string stopName, string period, string routeName, 
                                       int entered, int exited, int transported)
        {
            TripStopId = id;
            Period = period;
            RouteName = period;
            _originalStopNumber = StopNumber = stopNumber;
            _originalStopName = StopName = stopName;
            _originalEntered = Entered = entered;
            _originalExited = Exited = exited;
            _originalTransported = Transported = transported;

            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void AcceptChanges()
        {
            _originalStopNumber = StopNumber;
            _originalStopName = StopName;
            _originalEntered = Entered;
            _originalExited = Exited;
            _originalTransported = Transported;

            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void RejectChanges()
        {
            StopNumber = _originalStopNumber;
            StopName = _originalStopName;
            Entered = _originalEntered;
            Exited = _originalExited;
            Transported = _originalTransported;

            IsDirty = false;
            ChangedProperties.Clear();
        }
    }
}
