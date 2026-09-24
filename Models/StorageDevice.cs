using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WinOptimizer.Models
{
    public class StorageDevice
    {
         public string Model { get; set; } = "Unknown";
        public string Firmware { get; set; } = "Unknown";

        public string InterfaceType { get; set; } = "Unknown";
        public string MediaType { get; set; } = "Unknown";

        public string SerialNumber { get; set; } = "";
        public string DeviceId { get; set; } = "";

        public ulong SizeBytes { get; set; }

        public double SizeGigabytes =>
            SizeBytes / 1024.0 / 1024.0 / 1024.0;
    }
}
