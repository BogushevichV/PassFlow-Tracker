namespace PassFlow_Tracker.UI.ViewModels.Formatting
{
    public sealed class ColumnItem
    {
        public required string Header { get; init; }
        public required string PropertyName { get; init; }
        public bool IsNumeric { get; init; }

        public override string ToString() => Header;
    }
}
