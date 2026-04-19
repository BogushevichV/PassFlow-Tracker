using Avalonia.Controls;
using PassFlow_Tracker.UI.ViewModels;

namespace PassFlow_Tracker.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var vm = new MainWindowViewModel();
            vm.MainWindow = this;

            DataContext = vm;
        }
    }
}