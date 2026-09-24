using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinOptimizer.Models;

namespace WinOptimizer.Services
{
    public class SystemAnalyzer
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MemoryStatus
        {
            public uint dwLength;
            public uint dwMemoryLoad;

            public ulong ullTotalPhys;
            public ulong ullAvailPhys;

            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;

            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;

            public ulong ullAvailExtendedVirtual;

            public MemoryStatus()
            {
                dwLength = (uint)Marshal.SizeOf(this);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(
            out FileTime lpIdleTime,
            out FileTime lpKernelTime,
            out FileTime lpUserTime
        );

        private ulong? _previousIdleTime;
        private ulong? _previousKernelTime;
        private ulong? _previousUserTime;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(
            [In, Out] MemoryStatus lpBuffer
        );

        public string GetWindowsVersion()
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
            );

            if (key == null)
            {
                return "Unknown Windows Version";
            }


            string? editionString = key.GetValue("EditionID")?.ToString();
            string? buildString = key.GetValue("CurrentBuildNumber")?.ToString();
            string? displayVersion = key.GetValue("DisplayVersion")?.ToString();

            if (!int.TryParse(buildString, out int build))
            {
                return "Unknown Windows Version";
            }

            string windowsName = build >= 22000
                ? "Windows 11"
                : "Windows 10";

            return $"{windowsName} {editionString} {displayVersion} (Build {build})";
        }

        public string GetWindowsEdition()
        {
            using RegistryKey? key =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
                );

            if (key == null)
            {
                return "Unknown";
            }

            return key.GetValue("EditionID")
                ?.ToString()
                ?.Trim()
                ?? "Unknown";
        }

        public int GetProcessorCount()
        {
            return Environment.ProcessorCount;
        }
        public string GetProcessorName()
        {
            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Processor"
                );

            foreach (ManagementObject cpu in searcher.Get())
            {
                return cpu["Name"]?.ToString()?.Trim()
                    ?? "Unknown CPU";
            }

            return "Unknown CPU";
        }

        public DriveInfo[] GetDrives()
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady)
                .ToArray();
        }

        public DriveInfo GetSystemDrive()
        {
            string systemDrive =
                Path.GetPathRoot(Environment.SystemDirectory)!;

            return new DriveInfo(systemDrive);
        }

        private MemoryStatus GetMemoryStatus()
        {
            MemoryStatus memory = new MemoryStatus();

            if (!GlobalMemoryStatusEx(memory))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error()
                );
            }

            return memory;
        }
        public List<GpuInfo> GetGpus()
        {
            List<GpuInfo> gpus = new List<GpuInfo>();

            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion FROM Win32_VideoController"
                );

            foreach (ManagementObject gpu in searcher.Get())
            {
                gpus.Add(new GpuInfo
                {
                    Name =
                        gpu["Name"]?.ToString()?.Trim()
                        ?? "Unknown GPU",

                    DriverVersion =
                        gpu["DriverVersion"]?.ToString()?.Trim()
                        ?? "Unknown"
                });
            }

            return gpus;
        }
        public string GetMotherboardName()
        {
            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Manufacturer, Product FROM Win32_BaseBoard"
                );

            foreach (ManagementObject board in searcher.Get())
            {
                string manufacturer =
                    board["Manufacturer"]?.ToString()?.Trim()
                    ?? "Unknown";

                string product =
                    board["Product"]?.ToString()?.Trim()
                    ?? "Unknown";

                return $"{manufacturer} {product}";
            }

            return "Unknown Motherboard";
        }
        public string GetBiosVersion()
        {
            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT SMBIOSBIOSVersion FROM Win32_BIOS"
                );

            foreach (ManagementObject bios in searcher.Get())
            {
                return bios["SMBIOSBIOSVersion"]
                    ?.ToString()
                    ?.Trim()
                    ?? "Unknown";
            }

            return "Unknown";
        }
        public List<PhysicalDriveInfo> GetPhysicalDrives()
        {
            List<PhysicalDriveInfo> drives =
                new List<PhysicalDriveInfo>();

            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Model, Size, FirmwareRevision, InterfaceType " +
                    "FROM Win32_DiskDrive"
                );

            foreach (ManagementObject drive in searcher.Get())
            {
                ulong size = 0;

                if (drive["Size"] != null)
                {
                    ulong.TryParse(
                        drive["Size"].ToString(),
                        out size
                    );
                }

                drives.Add(new PhysicalDriveInfo
                {
                    Model =
                        drive["Model"]?.ToString()?.Trim()
                        ?? "Unknown Drive",


                    Size = size,

                    Firmware =
                        drive["FirmwareRevision"]?.ToString()?.Trim()
                        ?? "Unknown",

                    InterfaceType =
                        drive["InterfaceType"]?.ToString()?.Trim()
                        ?? "Unknown"
                });
            }

            return drives;
        }
        public double GetCpuUsagePercentage()
        {
            if (!GetSystemTimes(
                out FileTime idleTime,
                out FileTime kernelTime,
                out FileTime userTime))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error()
                );
            }

            ulong idle = FileTimeToUInt64(idleTime);
            ulong kernel = FileTimeToUInt64(kernelTime);
            ulong user = FileTimeToUInt64(userTime);

            // Beim ersten Aufruf haben wir noch keinen Vergleichswert.
            if (_previousIdleTime == null ||
                _previousKernelTime == null ||
                _previousUserTime == null)
            {
                _previousIdleTime = idle;
                _previousKernelTime = kernel;
                _previousUserTime = user;

                return 0;
            }

            ulong idleDelta =
                idle - _previousIdleTime.Value;

            ulong kernelDelta =
                kernel - _previousKernelTime.Value;

            ulong userDelta =
                user - _previousUserTime.Value;

            ulong totalDelta =
                kernelDelta + userDelta;

            _previousIdleTime = idle;
            _previousKernelTime = kernel;
            _previousUserTime = user;

            if (totalDelta == 0)
            {
                return 0;
            }

            ulong busyDelta =
                totalDelta - idleDelta;

            return busyDelta * 100.0 / totalDelta;
        }

        private static ulong FileTimeToUInt64(
            FileTime fileTime)
        {
            return ((ulong)fileTime.dwHighDateTime << 32)
                   | fileTime.dwLowDateTime;
        }
        public MemorySnapshot GetMemorySnapshot()
        {
            MemoryStatus memory = GetMemoryStatus();

            return new MemorySnapshot
            {
                TotalBytes = memory.ullTotalPhys,
                AvailableBytes = memory.ullAvailPhys,
                UsedBytes =
                    memory.ullTotalPhys - memory.ullAvailPhys,
                UsagePercentage = memory.dwMemoryLoad
            };
        }
        public bool SupportsAdvancedPolicies()
        {
            string edition =
                GetWindowsEdition();

            return edition switch
            {
                "Professional" => true,
                "ProfessionalN" => true,
                "ProfessionalWorkstation" => true,
                "ProfessionalWorkstationN" => true,
                "Enterprise" => true,
                "EnterpriseN" => true,
                "Education" => true,
                "EducationN" => true,
                "IoTEnterprise" => true,

                _ => false
            };
        }
    }
}