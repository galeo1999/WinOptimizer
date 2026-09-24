using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinOptimizer.Models;

namespace WinOptimizer.Models
{
    public class PhysicalDriveInfo
    {
        public string Model { get; set; } = "Unknown Drive";
        public ulong Size { get; set; }
        public string Firmware { get; set; } = "Unknown";
        public string InterfaceType { get; set; } = "Unknown";
    }
}