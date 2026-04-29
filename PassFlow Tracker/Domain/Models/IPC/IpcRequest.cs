using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Domain.Models.IPC
{
    public class IpcRequest
    {
        public string Command { get; set; } = "";
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
