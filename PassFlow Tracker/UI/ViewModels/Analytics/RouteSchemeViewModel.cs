using CommunityToolkit.Mvvm.ComponentModel;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public partial class RouteSchemeViewModel : ViewModelBase
    {
        [ObservableProperty] private int _stopNumber;
        [ObservableProperty] private string _stopName = "";
        [ObservableProperty] private int _entered;
        [ObservableProperty] private int _exited;
        [ObservableProperty] private int _transported;
        [ObservableProperty] private double _fillPercent;    
        [ObservableProperty] private string _lineColor = "#94A3B8";
        [ObservableProperty] private string _previousLineColor = "#CBD5E1";
        [ObservableProperty] private bool _isFirst;
        [ObservableProperty] private bool _isLast;
        [ObservableProperty] private string _transportedLabel = "";
        [ObservableProperty] private string _enteredLabel = "";
        [ObservableProperty] private string _exitedLabel = "";
    }
}
