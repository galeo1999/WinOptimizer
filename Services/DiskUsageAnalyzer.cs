using System.Diagnostics;
using System.IO;
using System.Security;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public sealed class DiskUsageAnalyzer
{
    public Task<DiskUsageScanResult> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default,
        IProgress<DiskUsageProgress>? progress = null,
        int largestFileLimit = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Choose an absolute folder or drive path.", nameof(path));
        if (largestFileLimit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(largestFileLimit));

        string fullPath = Path.GetFullPath(path);
        return Task.Run(() => Analyze(fullPath, largestFileLimit, progress, cancellationToken),
            cancellationToken);
    }

    private static DiskUsageScanResult Analyze(string path, int limit,
        IProgress<DiskUsageProgress>? progress, CancellationToken token)
    {
        var root = CreateNode(new DirectoryInfo(path));
        var largest = new PriorityQueue<LargeFileInfo, long>();
        var pending = new Stack<(StorageTreeNode Node, StorageTreeNode? Parent, bool Complete)>();
        pending.Push((root, null, false));
        long bytes = 0, files = 0, skipped = 0;
        var timer = Stopwatch.StartNew();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = false,
            AttributesToSkip = 0,
            ReturnSpecialDirectories = false
        };

        void Report(string currentPath, bool force = false)
        {
            if (!force && timer.ElapsedMilliseconds < 200)
                return;
            progress?.Report(new DiskUsageProgress(currentPath, bytes, files, skipped));
            timer.Restart();
        }

        void Skip(StorageTreeNode node)
        {
            node.SkippedItems++;
            skipped++;
        }

        Report(path, true);
        while (pending.TryPop(out var work))
        {
            token.ThrowIfCancellationRequested();
            StorageTreeNode node = work.Node;
            if (work.Complete)
            {
                node.Children.Sort((left, right) =>
                {
                    int sizeOrder = right.SizeBytes.CompareTo(left.SizeBytes);
                    return sizeOrder != 0 ? sizeOrder : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                });
                if (work.Parent != null)
                {
                    work.Parent.SizeBytes += node.SizeBytes;
                    work.Parent.FileCount += node.FileCount;
                    work.Parent.SkippedItems += node.SkippedItems;
                }
                continue;
            }

            pending.Push((node, work.Parent, true));
            Report(node.Path);
            try
            {
                // Check ancestors too: a selected folder can itself be inside a junction.
                // Recheck on entry, rather than trusting attributes cached when it was queued.
                if (HasReparseAncestor(node.Path, token))
                {
                    Skip(node);
                    continue;
                }

                foreach (FileSystemInfo entry in new DirectoryInfo(node.Path).EnumerateFileSystemInfos("*", options))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            Skip(node);
                        }
                        else if (entry is DirectoryInfo directory)
                        {
                            StorageTreeNode child = CreateNode(directory);
                            node.Children.Add(child);
                            pending.Push((child, node, false));
                        }
                        else if (entry is FileInfo file)
                        {
                            // Read metadata only; never open file contents or hydrate cloud placeholders.
                            long size = file.Length;
                            node.SizeBytes += size;
                            node.FileCount++;
                            bytes += size;
                            files++;
                            if (largest.Count < limit ||
                                (largest.TryPeek(out _, out long smallest) && size > smallest))
                            {
                                largest.Enqueue(new LargeFileInfo
                                {
                                    Name = file.Name, Path = file.FullName, SizeBytes = size
                                }, size);
                                if (largest.Count > limit)
                                    largest.Dequeue();
                            }
                        }
                    }
                    catch (Exception ex) when (IsAccessFailure(ex))
                    {
                        Skip(node);
                    }
                    Report(node.Path);
                }
            }
            catch (Exception ex) when (IsAccessFailure(ex))
            {
                // Keep any entries already measured if enumeration fails partway through.
                Skip(node);
            }
        }

        token.ThrowIfCancellationRequested();
        Report(path, true);
        return new DiskUsageScanResult(root, largest.UnorderedItems
            .Select(item => item.Element)
            .OrderByDescending(file => file.SizeBytes)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private static StorageTreeNode CreateNode(DirectoryInfo directory) => new()
    {
        Name = string.IsNullOrEmpty(directory.Name) ? directory.FullName : directory.Name,
        Path = directory.FullName
    };

    private static bool HasReparseAncestor(string path, CancellationToken token)
    {
        var ancestors = new Stack<DirectoryInfo>();
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
        {
            token.ThrowIfCancellationRequested();
            ancestors.Push(directory);
        }
        // Inspect from the drive root down, before accessing anything beneath a link.
        foreach (DirectoryInfo directory in ancestors)
        {
            token.ThrowIfCancellationRequested();
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static bool IsAccessFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;
}
