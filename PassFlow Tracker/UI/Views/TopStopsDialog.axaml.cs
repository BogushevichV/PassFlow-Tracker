using Avalonia.Controls;
using Avalonia.Interactivity;
using PassFlow_Tracker.Domain.Models;

namespace PassFlow_Tracker.UI.Views
{
    public partial class TopStopsDialog : Window
    {
        public TopStopsMode? SelectedMode { get; private set; }

        public TopStopsDialog()
        {
            InitializeComponent();
        }

        private void OnOk(object? sender, RoutedEventArgs e)
        {
            if (RadioPerDay.IsChecked == true)
                SelectedMode = TopStopsMode.PerDay;
            else if (RadioAllTime.IsChecked == true)
                SelectedMode = TopStopsMode.AllTime;
            else
                SelectedMode = TopStopsMode.PerRecord;

            Close(SelectedMode);
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            SelectedMode = null;
            Close(null);
        }
    }
}
