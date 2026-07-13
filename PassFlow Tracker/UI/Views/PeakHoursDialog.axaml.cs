using Avalonia.Controls;
using Avalonia.Interactivity;
using PassFlow_Tracker.Domain.Models;
using System.Collections.Generic;

namespace PassFlow_Tracker.UI.Views
{
    public partial class PeakHoursDialog : Window
    {
        // null = вся сеть, иначе unit_name выбранного маршрута
        public string? SelectedUnit { get; private set; }
        public bool Confirmed { get; private set; }

        public PeakHoursDialog()
        {
            InitializeComponent();
        }

        /// <summary>Заполнить выпадающий список маршрутов.</summary>
        public void SetRoutes(IEnumerable<RouteItem> routes)
        {
            RouteCombo.ItemsSource = routes;
        }

        private void OnScopeChanged(object? sender, RoutedEventArgs e)
        {
            RouteCombo.IsEnabled = RadioRoute.IsChecked == true;
        }

        private void OnOk(object? sender, RoutedEventArgs e)
        {
            Confirmed = true;
            if (RadioRoute.IsChecked == true && RouteCombo.SelectedItem is RouteItem r)
                SelectedUnit = r.UnitName;
            else
                SelectedUnit = null;

            Close((Confirmed, SelectedUnit));
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close((false, (string?)null));
        }
    }
}
