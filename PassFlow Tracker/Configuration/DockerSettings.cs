using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Configuration
{
    public class DockerSettings
    {
        public string ImageName { get; set; }
        public string Tag { get; set; }
        public string ContainerName { get; set; }
        public string Password { get; set; }
        public string DbPort { get; set; }
    }
}
