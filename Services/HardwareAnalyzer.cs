using System.Collections.Generic;
using System.Management;
using WinOptimizer.Models;

namespace WinOptimizer.Services
{
    public class HardwareAnalyzer
    {
        public string GetCpuName()
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

        public string GetGpuName()
        {
            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController"
                );

            List<string> gpus = new List<string>();

            foreach (ManagementObject gpu in searcher.Get())
            {
                string? name = gpu["Name"]?.ToString()?.Trim();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    gpus.Add(name);
                }
            }

            if (gpus.Count == 0)
            {
                return "Unknown GPU";
            }

            return string.Join("\n", gpus);
        }

        public string GetMotherboard()
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
                return bios["SMBIOSBIOSVersion"]?.ToString()?.Trim()
                    ?? "Unknown BIOS";
            }

            return "Unknown BIOS";
        }
        public string GetGpuDriverVersion()
        {
            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion FROM Win32_VideoController"
                );

            foreach (ManagementObject gpu in searcher.Get())
            {
                string? name = gpu["Name"]?.ToString();

                if (!string.IsNullOrWhiteSpace(name) &&
                    name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    return gpu["DriverVersion"]?.ToString()
                        ?? "Unknown";
                }
            }

            return "Unknown";
        }
        public List<StorageDevice> GetStorageDevices()
        {
            List<StorageDevice> storageDevices = new List<StorageDevice>();

            using ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    @"SELECT Model,
                     FirmwareRevision,
                     InterfaceType,
                     MediaType,
                     SerialNumber,
                     DeviceID,
                     Size
              FROM Win32_DiskDrive"
                );

            foreach (ManagementObject drive in searcher.Get())
            {
                ulong sizeBytes = 0;

                if (drive["Size"] != null)
                {
                    ulong.TryParse(
                        drive["Size"].ToString(),
                        out sizeBytes
                    );
                }

                StorageDevice device = new StorageDevice
                {
                    Model =
                        drive["Model"]?.ToString()?.Trim()
                        ?? "Unknown",

                    Firmware =
                        drive["FirmwareRevision"]?.ToString()?.Trim()
                        ?? "Unknown",

                    InterfaceType =
                        drive["InterfaceType"]?.ToString()?.Trim()
                        ?? "Unknown",

                    MediaType =
                        drive["MediaType"]?.ToString()?.Trim()
                        ?? "Unknown",

                    SerialNumber =
                        drive["SerialNumber"]?.ToString()?.Trim()
                        ?? "",

                    DeviceId =
                        drive["DeviceID"]?.ToString()?.Trim()
                        ?? "",

                    SizeBytes = sizeBytes
                };

                storageDevices.Add(device);
            }

            return storageDevices;
        }
    }
}