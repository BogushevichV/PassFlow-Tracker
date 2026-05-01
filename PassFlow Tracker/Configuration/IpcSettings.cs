using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Configuration
{
    public class IpcSettings
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 5000;
    }
}
