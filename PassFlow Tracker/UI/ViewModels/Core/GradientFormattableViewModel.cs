using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace PassFlow_Tracker.UI.ViewModels.Core
{
    public partial class GradientFormattableViewModel : ViewModelBase, IGradientFormattable
    {
        private readonly Dictionary<string, string?> _cellBackgrounds = new();

        [ObservableProperty]
        private bool _showGradient = true;

        [ObservableProperty]
        private int _formattingVersion;

        public string? GetCellBackground(string column) =>
            ShowGradient && _cellBackgrounds.TryGetValue(column, out var bg) ? bg : null;

        public void SetCellBackground(string column, string? color)
        {
            if (color == null)
                _cellBackgrounds.Remove(column);
            else
                _cellBackgrounds[column] = color;
        }

        public void ClearAllCellBackgrounds()
        {
            _cellBackgrounds.Clear();
            NotifyFormattingChanged();
        }

        public void NotifyFormattingChanged() => FormattingVersion++;
    }
}
