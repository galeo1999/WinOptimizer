using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class StorageCleaner
{
    private static readonly TimeSpan
        MinimumTempFileAge =
            TimeSpan.FromHours(24);

    public async Task<StorageCleanupResult>
        CleanAsync(
            StorageLocationInfo location,
            CancellationToken cancellationToken =
                default)
    {
        if (!location.IsCleanupCandidate)
        {
            throw new InvalidOperationException(
                "This storage location is not a cleanup candidate."
            );
        }

        return await Task.Run(
            () =>
            {
                return location.Type switch
                {
                    StorageLocationType.UserTemp =>
                        CleanTempDirectory(
                            location.Path,
                            cancellationToken
                        ),

                    StorageLocationType.WindowsTemp =>
                        CleanWindowsTemp(
                            location,
                            cancellationToken
                        ),

                    StorageLocationType.RecycleBin =>
                        EmptyRecycleBin(
                            location
                        ),

                    _ =>
                        throw new NotSupportedException(
                            "This storage location cannot be cleaned."
                        )
                };
            },
            cancellationToken
        );
    }


    private static StorageCleanupResult
        CleanWindowsTemp(
            StorageLocationInfo location,
            CancellationToken cancellationToken)
    {
        if (!AdminService
                .IsRunningAsAdministrator())
        {
            throw new UnauthorizedAccessException(
                "Administrator privileges are required to clean Windows Temp."
            );
        }

        return CleanTempDirectory(
            location.Path,
            cancellationToken
        );
    }


    private static StorageCleanupResult
        CleanTempDirectory(
            string path,
            CancellationToken cancellationToken)
    {
        StorageCleanupResult result =
            new StorageCleanupResult();

        if (!Directory.Exists(path))
        {
            return result;
        }

        DateTime cutoff =
            DateTime.UtcNow -
            MinimumTempFileAge;

        Stack<string> directories =
            new Stack<string>();

        List<string> visitedDirectories =
            [];

        directories.Push(path);

        while (directories.Count > 0)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            string currentDirectory =
                directories.Pop();

            visitedDirectories.Add(
                currentDirectory
            );


            // Files
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

                        if ((info.Attributes &
                             FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedFiles++;
                            continue;
                        }

                        // Keep recently used temp files.
                        if (info.LastWriteTimeUtc >
                            cutoff)
                        {
                            result.SkippedFiles++;
                            continue;
                        }

                        long size =
                            info.Length;

                        File.Delete(file);

                        result.DeletedFiles++;

                        result.DeletedBytes +=
                            size;
                    }
                    catch (
                        UnauthorizedAccessException)
                    {
                        result.SkippedFiles++;
                    }
                    catch (
                        IOException)
                    {
                        result.SkippedFiles++;
                    }
                    catch
                    {
                        result.FailedFiles++;
                    }
                }
            }
            catch (
                UnauthorizedAccessException)
            {
                result.SkippedFiles++;
            }
            catch (
                IOException)
            {
                result.SkippedFiles++;
            }


            // Subdirectories
            try
            {
                foreach (
                    string directory
                    in Directory.EnumerateDirectories(
                        currentDirectory))
                {
                    try
                    {
                        DirectoryInfo info =
                            new DirectoryInfo(
                                directory
                            );

                        // Do not follow junctions/symlinks.
                        if ((info.Attributes &
                             FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedFiles++;
                            continue;
                        }

                        directories.Push(
                            directory
                        );
                    }
                    catch
                    {
                        result.SkippedFiles++;
                    }
                }
            }
            catch (
                UnauthorizedAccessException)
            {
                result.SkippedFiles++;
            }
            catch (
                IOException)
            {
                result.SkippedFiles++;
            }
        }


        // Remove empty directories from deepest to highest.
        foreach (
            string directory
            in visitedDirectories
                .OrderByDescending(
                    item => item.Length))
        {
            if (string.Equals(
                directory,
                path,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Directory.Delete(
                    directory,
                    recursive: false
                );
            }
            catch
            {
                // Directory may still contain skipped files.
            }
        }

        return result;
    }


    private static StorageCleanupResult
        EmptyRecycleBin(
            StorageLocationInfo location)
    {
        const uint SHERB_NOCONFIRMATION =
            0x00000001;

        const uint SHERB_NOPROGRESSUI =
            0x00000002;

        const uint SHERB_NOSOUND =
            0x00000004;

        int resultCode =
            SHEmptyRecycleBin(
                IntPtr.Zero,
                null,
                SHERB_NOCONFIRMATION |
                SHERB_NOPROGRESSUI |
                SHERB_NOSOUND
            );

        if (resultCode != 0)
        {
            Marshal.ThrowExceptionForHR(
                resultCode
            );
        }

        return new StorageCleanupResult
        {
            DeletedFiles =
                location.FileCount,

            DeletedBytes =
                location.SizeBytes
        };
    }


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int
        SHEmptyRecycleBin(
            IntPtr hwnd,
            string? pszRootPath,
            uint dwFlags
        );
}