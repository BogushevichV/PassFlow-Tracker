using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.ViewModels
{
    public abstract partial class TrackedViewModel : ViewModelBase
    {
        public bool IsDirty { get; protected set; }

        public HashSet<string> ChangedProperties { get; } = new();

        public abstract void AcceptChanges();

        public abstract void RejectChanges();

        protected void MarkDirty(string propertyName)
        {
            IsDirty = true;
            ChangedProperties.Add(propertyName);
        }
    }
}
