using CommunityToolkit.Mvvm.ComponentModel;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class VehicleViewModel : ViewModelBase
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _unitName = "";
        [ObservableProperty] private int _modelId;
        [ObservableProperty] private string _description = "";


        private VehicleModelViewModel? _selectedModel;
        public VehicleModelViewModel? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (value != null)
                {
                    _selectedModel = value;
                    ModelId = value.Id;
                    OnPropertyChanged();
                }
            }
        }
    }
}
