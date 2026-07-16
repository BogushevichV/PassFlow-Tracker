using CommunityToolkit.Mvvm.ComponentModel;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class VehicleModelViewModel : ViewModelBase
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _seats;
        [ObservableProperty] private int _capacity;
        [ObservableProperty] private string _description = "";
    }
}
