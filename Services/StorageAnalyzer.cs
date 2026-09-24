using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class StorageAnalyzer
{
    public async Task<List<StorageLocationInfo>>
        AnalyzeAsync(
            CancellationToken cancellationToken =
                default)
    {
        return await Task.Run(
            () =>
            {
                List<StorageLocationInfo> locations =
                    [];

                // User Temp
                string userTemp =
                    Path.GetTempPath();

locations.Add(
    AnalyzeDirectory(
        "User Temp",
        userTemp,
        "Temporary files for the current Windows user.",
        type: StorageLocationType.UserTemp,
        isCleanupCandidate: true,
        requiresAdmin: false,
        requiresConfirmation: true,
        cancellationToken
    )
);

                // Windows Temp
                string windowsDirectory =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows
                    );

                string windowsTemp =
                    Path.Combine(
                        windowsDirectory,
                        "Temp"
                    );

    locations.Add(
    AnalyzeDirectory(
        "Windows Temp",
        windowsTemp,
        "Temporary files created by Windows and system applications.",
        type: StorageLocationType.WindowsTemp,
        isCleanupCandidate: true,
        requiresAdmin: true,
        requiresConfirmation: true,
        cancellationToken
    )
);


                // Downloads
                string userProfile =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    );

                string downloads =
                    Path.Combine(
                        userProfile,
                        "Downloads"
                    );

    locations.Add(
    AnalyzeDirectory(
        "Downloads",
        downloads,
        "Files stored in the current user's Downloads folder.",
        type: StorageLocationType.Downloads,
        isCleanupCandidate: false,
        requiresAdmin: false,
        requiresConfirmation: false,
        cancellationToken
    )
);

                // Recycle Bin
                locations.Add(
                    AnalyzeRecycleBin()
                );


                return locations;
            },
            cancellationToken
        );
    }


private static StorageLocationInfo AnalyzeDirectory(
    string name,
    string path,
    string description,
    StorageLocationType type,
    bool isCleanupCandidate,
    bool requiresAdmin,
    bool requiresConfirmation,
    CancellationToken cancellationToken)
    {
       StorageLocationInfo result =
    new StorageLocationInfo
    {
        Name = name,
        Path = path,
        Description = description,

        Type = type,

        IsCleanupCandidate =
            isCleanupCandidate,

        RequiresAdmin =
            requiresAdmin,

        RequiresConfirmation =
            requiresConfirmation
    };

        if (!Directory.Exists(path))
        {
            return result;
        }

        Stack<string> directories =
            new Stack<string>();

        directories.Push(path);

        while (directories.Count > 0)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            string currentDirectory =
                directories.Pop();

            try
            {
                foreach (
                    string file
                    in Directory.EnumerateFiles(
                        currentDirectory))
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    try
                    {
                        FileInfo info =
                            new FileInfo(file);

                        result.SizeBytes +=
                            info.Length;

                        result.FileCount++;
                    }
                    catch (
                        UnauthorizedAccessException)
                    {
                        result.SkippedItems++;
                    }
                    catch (
                        IOException)
                    {
                        result.SkippedItems++;
                    }
                }
            }
            catch (
                UnauthorizedAccessException)
            {
                result.SkippedItems++;
            }
            catch (
                IOException)
            {
                result.SkippedItems++;
            }

            try
            {
                foreach (
                    string directory
                    in Directory.EnumerateDirectories(
                        currentDirectory))
                {
                    directories.Push(
                        directory
                    );
                }
            }
            catch (
                UnauthorizedAccessException)
            {
                result.SkippedItems++;
            }
            catch (
                IOException)
            {
                result.SkippedItems++;
            }
        }

        return result;
    }


    private static StorageLocationInfo
        AnalyzeRecycleBin()
    {
        StorageLocationInfo result =
            new StorageLocationInfo
            {
         Name =
            "Recycle Bin",

        Path =
            "Windows Recycle Bin",

        Description =
            "Files currently stored in the Windows Recycle Bin.",

        IsCleanupCandidate =
            true,

        RequiresAdmin =
            false,

        RequiresConfirmation =
            true,
        Type =
            StorageLocationType.RecycleBin
            };

        SHQUERYRBINFO info =
            new SHQUERYRBINFO
            {
                cbSize =
                    Marshal.SizeOf<
                        SHQUERYRBINFO>()
            };

        int returnValue =
            SHQueryRecycleBin(
                null,
                ref info
            );

        if (returnValue != 0)
        {
            result.SkippedItems++;

            return result;
        }

        result.SizeBytes =
            info.i64Size;

        result.FileCount =
            info.i64NumItems;

        return result;
    }


    [StructLayout(
        LayoutKind.Sequential,
        Pack = 8)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;

        public long i64Size;

        public long i64NumItems;
    }


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int
        SHQueryRecycleBin(
            string? pszRootPath,
            ref SHQUERYRBINFO pSHQueryRBInfo
        );
}