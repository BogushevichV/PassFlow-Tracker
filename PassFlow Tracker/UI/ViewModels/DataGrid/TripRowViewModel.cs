using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class TripRowViewModel : TrackedViewModel
    {
        public int TripId { get; set; }

        private string _originalUnitName = "";
        private string _originalStartPoint = "";
        private string _originalEndPoint = "";
        private string _originalTimeFrom = "";
        private string _originalTimeTo = "";
        private int _originalEntered;
        private int _originalExited;
        private int _originalTransported;

        [ObservableProperty] private string _unitName = string.Empty;
        partial void OnUnitNameChanged(string value) { if (value != _originalUnitName) MarkDirty(nameof(UnitName)); }

        [ObservableProperty] private string _startPoint = string.Empty;
        partial void OnStartPointChanged(string value) { if (value != _originalStartPoint) MarkDirty(nameof(StartPoint)); }

        [ObservableProperty] private string _endPoint = string.Empty;
        partial void OnEndPointChanged(string value) { if (value != _originalEndPoint) MarkDirty(nameof(EndPoint)); }

        [ObservableProperty] private string _timeFrom = string.Empty;
        partial void OnTimeFromChanged(string value) { if (value != _originalTimeFrom) MarkDirty(nameof(TimeFrom)); }

        [ObservableProperty] private string _timeTo = string.Empty;
        partial void OnTimeToChanged(string value) { if (value != _originalTimeTo) MarkDirty(nameof(TimeTo)); }

        [ObservableProperty] private int _entered;
        partial void OnEnteredChanged(int value) { if (value != _originalEntered) MarkDirty(nameof(Entered)); }

        [ObservableProperty] private int _exited;
        partial void OnExitedChanged(int value) { if (value != _originalExited) MarkDirty(nameof(Exited)); }

        [ObservableProperty] private int _transported;
        partial void OnTransportedChanged(int value) { if (value != _originalTransported) MarkDirty(nameof(Transported)); }

        public void SetOriginalValues(int id, string unitName, string startPoint, string endPoint,
                                       string timeFrom, string timeTo,
                                       int entered, int exited, int transported)
        {
            TripId = id;
            _originalUnitName = UnitName = unitName;
            _originalStartPoint = StartPoint = startPoint;
            _originalEndPoint = EndPoint = endPoint;
            _originalTimeFrom = TimeFrom = timeFrom;
            _originalTimeTo = TimeTo = timeTo;
            _originalEntered = Entered = entered;
            _originalExited = Exited = exited;
            _originalTransported = Transported = transported;
            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void AcceptChanges()
        {
            _originalUnitName = UnitName;
            _originalStartPoint = StartPoint;
            _originalEndPoint = EndPoint;
            _originalTimeFrom = TimeFrom;
            _originalTimeTo = TimeTo;
            _originalEntered = Entered;
            _originalExited = Exited;
            _originalTransported = Transported;
            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void RejectChanges()
        {
            UnitName = _originalUnitName;
            StartPoint = _originalStartPoint;
            EndPoint = _originalEndPoint;
            TimeFrom = _originalTimeFrom;
            TimeTo = _originalTimeTo;
            Entered = _originalEntered;
            Exited = _originalExited;
            Transported = _originalTransported;
            IsDirty = false;
            ChangedProperties.Clear();
        }
    }
}

