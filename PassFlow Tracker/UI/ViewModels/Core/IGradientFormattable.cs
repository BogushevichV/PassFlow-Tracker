namespace PassFlow_Tracker.UI.ViewModels.Core
{
    public interface IGradientFormattable
    {
        bool ShowGradient { get; set; }
        int FormattingVersion { get; }
        string? GetCellBackground(string column);
        void SetCellBackground(string column, string? color);
        void ClearAllCellBackgrounds();
        void NotifyFormattingChanged();
    }
}
