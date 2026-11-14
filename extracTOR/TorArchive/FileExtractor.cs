using extracTOR.Helper;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using extracTOR.Helper.Constants;
using Microsoft.Win32.SafeHandles;
using ZstdSharp;

namespace extracTOR
{
    /// <summary>
    /// Extracts files from .tor archives.
    ///
    ///   1 scheduler thread : goes through the archives one at a time, reads each archive's index and queues
    ///                        its files sorted by position in the archive.
    ///   N worker threads   : take files from the queue in that order, read the file's data, decompress it
    ///                        (zstd/zlib) and write it out.
    ///
    /// Because every worker takes the next file in archive order, the source drive sees (almost) one ascending
    /// sequential read per archive instead of several archives being read at once - which is what spinning
    /// disks need. Each worker reuses its own buffers, so memory use is fixed (about 32MB per worker at most)
    /// no matter how slow the output drive is.
    /// </summary>
    public class FileExtractor
    {
        public int _archivesFinished;
        private readonly HashDictionary _hashDictionary = new();
        private ExtractionCounters counters = new ExtractionCounters();

        /// <summary>Number of read/decompress/write threads. Defaults to one per logical CPU.</summary>
        public int WorkerCount { get; set; } = Environment.ProcessorCount;

        // Files up to this size (compressed and decompressed) are extracted in memory using the worker's
        // reusable buffers. The ~100 larger files (audio banks of up to ~200MB) are streamed in 1MB chunks.
        private const int MaxInMemoryFile = 16 * 1024 * 1024;

        public async Task LoadHashDictionary(string hashFilePath)
        {
            try
            {
                if (File.Exists(Constants.HashesFilenamePath))
                {
                    foreach (string line in File.ReadLines(Constants.HashesFilenamePath))
                    {
                        ulong fileHash = HashLittle2.GetHash(line);
                        _hashDictionary.Add(fileHash, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async Task<bool> ExtractFilesAsync(
            ProgressTrackers progress,
            string assetsPath,
            string outPath,
            bool pts,
            bool beta,
            Stopwatch stopwatch)
        {
            var success = false;
            var isRunning = true;
            Task monitor = Task.CompletedTask;

            // Fresh counters for every run (previously they carried over, so a second extraction
            // in the same session always reported failure).
            _archivesFinished = 0;
            counters = new ExtractionCounters();

            try
            {
                stopwatch.Restart();
                var archiveFiles = GetArchiveFiles(assetsPath, pts);

                int totalFiles = await Task.Run(() => ProcessArchiveHeaders(archiveFiles));
                progress.TotalFilesProgress.Report(totalFiles.ToString());

                monitor = StartProgressMonitoring(progress, stopwatch, () => Volatile.Read(ref isRunning), totalFiles);

                await PerformPipelinedExtraction(archiveFiles, pts, beta, outPath);

                success = _archivesFinished == archiveFiles.Count && counters.FailedFiles == 0;
            }
            catch (Exception ex)
            {
                HandleException(ex);
                success = false;
            }
            finally
            {
                Volatile.Write(ref isRunning, false);
                await monitor; // let the last tick finish so it can't overwrite the final values below
                StopProgressMonitoring(progress, stopwatch, counters);
            }

            return success;
        }

        private List<string> GetArchiveFiles(string assetsPath, bool pts)
        {
            var archiveFiles = new List<string>();
            // Match "*.tor" case-insensitively: Windows does this by default, Linux file systems do not.
            var allTors = Directory.EnumerateFiles(assetsPath, "*.tor", new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            });

            archiveFiles.AddRange(pts
                ? allTors.Where(f => Path.GetFileName(f).Contains("swtor_test_")).OrderBy(f => new FileInfo(f).Length)
                : allTors.Where(f => !Path.GetFileName(f).Contains("swtor_test_")).OrderBy(f => new FileInfo(f).Length));

            AddSpecialGraphicsArchive(archiveFiles, assetsPath, pts);
            return archiveFiles;
        }

        private void AddSpecialGraphicsArchive(List<string> archiveFiles, string assetsPath, bool pts)
        {
            var parentDir = Directory.GetParent(assetsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToString();
            if (string.IsNullOrEmpty(parentDir)) return;

            var clientPath = pts
                ? Path.Combine(parentDir, Constants.Paths.PublicTestClient)
                : Path.Combine(parentDir, Constants.Paths.LiveClient);

            var gfxTor = ResolvePathIgnoreCase(Path.Combine(clientPath, Constants.Files.MainGfx));
            if (gfxTor != null) archiveFiles.Add(gfxTor);
        }

        // Returns the existing path whose components match 'path' ignoring case, or null if none exists.
        // On Windows this is the same as File.Exists; on Linux it copes with folder casing that differs
        // from what the Windows installer used (e.g. "RetailClient" vs "retailclient").
        private static string? ResolvePathIgnoreCase(string path)
        {
            if (File.Exists(path)) return path;
            if (OperatingSystem.IsWindows()) return null;

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return null;

            var current = root;
            var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!Directory.Exists(current)) return null;
                var match = Directory.EnumerateFileSystemEntries(current)
                    .FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
                if (match == null) return null;
                current = match;
            }

            return File.Exists(current) ? current : null;
        }

        private Task StartProgressMonitoring(
            ProgressTrackers progress,
            Stopwatch stopwatch,
            Func<bool> isRunning,
            int totalFiles)
        {
            // Progress is pushed to the UI ~10x per second rather than once per file
            // (800k+ UI updates per extraction otherwise).
            return Task.Run(async () =>
            {
                using var process = Process.GetCurrentProcess();
                while (isRunning())
                {
                    process.Refresh();
                    progress.MemoryProgress.Report(process.PeakWorkingSet64);
                    progress.TimeProgress.Report(stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
                    ReportCounts(progress, counters, totalFiles);
                    await Task.Delay(100);
                }
            });
        }

        private static void ReportCounts(ProgressTrackers progress, ExtractionCounters c, int totalFiles)
        {
            progress.ExtractedFilesProgress.Report($"{c.ProcessedFiles}");
            progress.ArcsProgress.Report(c.ArchivesFinished);
            if (totalFiles > 0)
                progress.ExtractionProgress.Report(Math.Min(100.0, c.ProcessedFiles / (double)totalFiles * 100.0));
        }

        private async Task<int> ProcessArchiveHeaders(
            List<string> archiveFiles)
        {
            int totalFiles = 0;
            foreach (var archiveFile in archiveFiles)
            {
                using var handle = File.OpenHandle(archiveFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                totalFiles += TORHeader.ReadHeader(handle).FileCount;
            }

            return totalFiles;
        }

        /// <summary>
        /// An open archive shared by the workers. The handle is closed once the scheduler has queued all of
        /// the archive's files and the workers have finished every one of them (simple reference count).
        /// </summary>
        private sealed class OpenArchive
        {
            private int _references = 1; // the scheduler's own reference, released when it has queued everything
            private int _failed;

            public OpenArchive(SafeFileHandle handle, int version) { Handle = handle; Version = version; }

            public SafeFileHandle Handle { get; }
            public int Version { get; }
            public bool Failed => Volatile.Read(ref _failed) != 0;

            public void AddReference() => Interlocked.Increment(ref _references);
            public void MarkFailed() => Volatile.Write(ref _failed, 1);

            /// <returns>true when this was the last reference (the archive is fully processed).</returns>
            public bool Release()
            {
                if (Interlocked.Decrement(ref _references) != 0) return false;
                Handle.Dispose();
                return true;
            }
        }

        // One unit of work: a single file to extract.
        private readonly record struct WorkItem(OpenArchive Archive, FileEntry Entry, string OutputPath);

        private async Task PerformPipelinedExtraction(
            List<string> archiveFiles,
            bool pts,
            bool beta,
            string outPath)
        {
            // Small queue: items are just file positions, and keeping it short keeps the workers' reads close
            // together in the archive.
            var queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(256)
            {
                SingleWriter = true,
                SingleReader = false,
            });
            var dirCache = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            var basePath = pts ? Path.Combine(outPath, "pts") :
                           beta ? Path.Combine(outPath, "beta") :
                                  outPath;

            var workers = Enumerable.Range(0, Math.Max(1, WorkerCount))
                .Select(_ => Task.Factory.StartNew(
                    () => RunWorker(queue.Reader, dirCache),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                .ToArray();

            var scheduler = Task.Factory.StartNew(() =>
            {
                try
                {
                    foreach (var archiveFile in archiveFiles)
                        QueueArchive(archiveFile, basePath, queue.Writer);
                }
                finally
                {
                    queue.Writer.Complete();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            await scheduler;
            await Task.WhenAll(workers);
        }

        // Scheduler stage: open archive -> read index -> queue its files in archive order.
        private void QueueArchive(string archiveFile, string basePath, ChannelWriter<WorkItem> queue)
        {
            OpenArchive? open = null;
            try
            {
                var archiveName = Path.GetFileName(archiveFile);
                var handle = File.OpenHandle(archiveFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    FileOptions.SequentialScan);

                var archive = new TORArchive(handle);
                open = new OpenArchive(handle, archive.Header.Version);
                var entries = archive.ReadEntries(handle, _hashDictionary, archiveName, ShouldProcessFile);

                foreach (var entry in entries)
                {
                    open.AddReference();
                    var item = new WorkItem(open, entry, ConstructOutputPath(basePath, entry.RelativePath));

                    // WriteAsync only waits when the queue is full; block this dedicated thread on it.
                    if (!queue.TryWrite(item))
                        queue.WriteAsync(item).AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                open?.MarkFailed();
                HandleException(ex);
            }
            finally
            {
                if (open == null) { /* couldn't even open/parse it: counts as not finished */ }
                else if (open.Release()) ArchiveCompleted(open);
            }
        }

        private void ArchiveCompleted(OpenArchive archive)
        {
            if (archive.Failed) return;
            Interlocked.Increment(ref _archivesFinished);
            counters.IncrementArchivesFinished();
        }

        // Worker stage: read + decompress + write, with buffers reused across files.
        private void RunWorker(ChannelReader<WorkItem> queue, ConcurrentDictionary<string, byte> dirCache)
        {
            using var zstd = new Decompressor();
            byte[] source = Array.Empty<byte>();
            byte[] scratch = Array.Empty<byte>();

            while (true)
            {
                if (!queue.TryRead(out var item))
                {
                    if (!queue.WaitToReadAsync().AsTask().GetAwaiter().GetResult()) return;
                    continue;
                }

                var entry = item.Entry;
                var archive = item.Archive;
                try
                {
                    EnsureDirectory(item.OutputPath, dirCache);

                    if (entry.CompressedSize > MaxInMemoryFile || entry.DecompressedSize > MaxInMemoryFile)
                    {
                        TORArchive.StreamEntry(archive.Handle, entry, archive.Version, item.OutputPath);
                    }
                    else
                    {
                        EnsureCapacity(ref source, entry.CompressedSize);
                        var data = source.AsSpan(0, entry.CompressedSize);
                        TORArchive.ReadExactly(archive.Handle, data, entry.DataOffset);

                        var output = Span<byte>.Empty;
                        if (entry.IsCompressed && entry.DecompressedSize > 0)
                        {
                            EnsureCapacity(ref scratch, entry.DecompressedSize);
                            output = scratch.AsSpan(0, entry.DecompressedSize);
                        }

                        TORArchive.WriteEntry(entry, archive.Version, data, output, zstd, item.OutputPath);
                    }

                    counters.IncrementFilesExtracted();
                }
                catch (Exception ex)
                {
                    counters.IncrementFailed();
                    Debug.WriteLine($"Failed to extract {entry.RelativePath}: {ex.Message}");
                }
                finally
                {
                    if (archive.Release()) ArchiveCompleted(archive);
                }
            }
        }

        // Grows a reusable buffer (doubling, capped at MaxInMemoryFile) so it settles at a steady size quickly.
        private static void EnsureCapacity(ref byte[] buffer, int size)
        {
            if (buffer.Length >= size) return;
            buffer = GC.AllocateUninitializedArray<byte>(Math.Min(MaxInMemoryFile, Math.Max(size, buffer.Length * 2)));
        }

        private static void EnsureDirectory(string filePath, ConcurrentDictionary<string, byte> dirCache)
        {
            var dir = Path.GetDirectoryName(filePath)!;
            if (dirCache.ContainsKey(dir)) return;
            Directory.CreateDirectory(dir);
            dirCache.TryAdd(dir, 0);
        }

        private static bool ShouldProcessFile(string relativePath, string archiveFileName)
        {
            if (string.Equals(relativePath, archiveFileName, StringComparison.OrdinalIgnoreCase))
                return false;

            return !relativePath.Contains("metadata.bin") &&
                   !relativePath.Contains("ft.sig") &&
                   !relativePath.Contains("groupmanifest.bin");
        }

        private static string ConstructOutputPath(string basePath, string relativePath)
        {
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return $"{basePath}{relativePath}";
        }

        private void StopProgressMonitoring(
            ProgressTrackers progress,
            Stopwatch stopwatch,
            ExtractionCounters finalCounters)
        {
            stopwatch.Stop();
            progress.ExtractedFilesProgress.Report($"{finalCounters.ProcessedFiles}");
            progress.ArcsProgress.Report(finalCounters.ArchivesFinished);
            progress.ExtractionProgress.Report(100);
            progress.TimeProgress.Report(stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
            progress.MemoryProgress.Report(Process.GetCurrentProcess().PeakWorkingSet64);
        }

        private void HandleException(Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
        }

        public int FailedFiles => counters.FailedFiles;
        public int ExtractedFiles => counters.ProcessedFiles;

        private class ExtractionCounters
        {
            private int _archivesFinished;
            private int _processedFiles;
            private int _failedFiles;
            public int ArchivesFinished => Volatile.Read(ref _archivesFinished);
            public int ProcessedFiles => Volatile.Read(ref _processedFiles);
            public int FailedFiles => Volatile.Read(ref _failedFiles);
            public void IncrementArchivesFinished() => Interlocked.Increment(ref _archivesFinished);
            public void IncrementFilesExtracted() => Interlocked.Increment(ref _processedFiles);
            public void IncrementFailed() => Interlocked.Increment(ref _failedFiles);
        }
    }
}
