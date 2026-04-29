using Avalonia.Controls;
using PassFlow_Tracker.UI.ViewModels;
using System;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.Views
{
    public partial class MainWindow : Window
    {
        private bool _isInitialized;

        public MainWindow()
        {
            InitializeComponent();

            var vm = new MainWindowViewModel();
            vm.MainWindow = this;

            DataContext = vm;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (!_isInitialized)
            {
                _isInitialized = true;
                _ = LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            if (DataContext is MainWindowViewModel vm)
            {
                try
                {
                    await vm.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки данных: {ex}");
                }
            }
        }
    }
}