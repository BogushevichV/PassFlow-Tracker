using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class DailyRecordRowViewModel : TrackedViewModel
    {
        public int DailyRecordId { get; set; }

        private string _originalUnitName = string.Empty;
        private string _originalRecordDate = string.Empty;
        private int _originalEntered;
        private int _originalExited;
        private int _originalTransported;

        [ObservableProperty]
        private string _unitName = string.Empty;
        partial void OnUnitNameChanged(string value) { if (value != _originalUnitName) MarkDirty(nameof(UnitName)); }

        [ObservableProperty]
        private string _recordDate = string.Empty;
        partial void OnRecordDateChanged(string value) { if (value != _originalRecordDate) MarkDirty(nameof(RecordDate)); }

        [ObservableProperty]
        private int _entered;
        partial void OnEnteredChanged(int value) { if (value != _originalEntered) MarkDirty(nameof(Entered)); }

        [ObservableProperty]
        private int _exited;
        partial void OnExitedChanged(int value) { if (value != _originalExited) MarkDirty(nameof(Exited)); }

        [ObservableProperty]
        private int _transported;
        partial void OnTransportedChanged(int value) { if (value != _originalTransported) MarkDirty(nameof(Transported)); }

        public void SetOriginalValues(int id, string unitName, string recordDate,
                                       int entered, int exited, int transported)
        {
            DailyRecordId = id;
            _originalUnitName = UnitName = unitName;
            _originalRecordDate = RecordDate = recordDate;
            _originalEntered = Entered = entered;
            _originalExited = Exited = exited;
            _originalTransported = Transported = transported;

            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void AcceptChanges()
        {
            _originalUnitName = UnitName;
            _originalRecordDate = RecordDate;
            _originalEntered = Entered;
            _originalExited = Exited;
            _originalTransported = Transported;

            IsDirty = false;
            ChangedProperties.Clear();
        }

        public override void RejectChanges()
        {
            UnitName = _originalUnitName;
            RecordDate = _originalRecordDate;
            Entered = _originalEntered;
            Exited = _originalExited;
            Transported = _originalTransported;

            IsDirty = false;
            ChangedProperties.Clear();
        }
    }
}
