using extracTOR;
using extracTOR.Helper;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using extracTOR.Helper.Constants;
using static extracTOR.TORArchive;

namespace extracTOR
{
    public class FileExtractor
    {
        public int _archivesFinished;
        private readonly HashDictionary _hashDictionary = new();
        private ExtractionCounters counters = new ExtractionCounters();
        public async Task LoadHashDictionary(string hashFilePath)
        {
            try
            {
                using var hashReader = new BinaryReader(File.Open(hashFilePath, FileMode.Open));
                while (hashReader.BaseStream.Position < hashReader.BaseStream.Length)
                {
                    ulong hash = hashReader.ReadUInt64();
                    string path = hashReader.ReadString(true);
                    _hashDictionary.Add(hash, path);
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
            var archivesFinished = 0;
            var isRunning = true;

            try
            {
                stopwatch.Restart();
                var archiveFiles = GetArchiveFiles(assetsPath, pts);
                ValidateArchiveCount(archiveFiles, pts);

                await StartProgressMonitoring(progress, stopwatch, () => isRunning);
                int totalFiles = 0;
                int processedFiles = 0;
                totalFiles = await Task.Run(() => ProcessArchiveHeaders(archiveFiles));
                progress.TotalFilesProgress.Report(totalFiles.ToString());
                var processorCount = 0;
                if (Environment.ProcessorCount < 16)
                {
                    processorCount = Environment.ProcessorCount / 2;
                }
                else if (Environment.ProcessorCount >= 16)
                {
                    processorCount = Environment.ProcessorCount - 2;
                }
                var extractionOptions = CreateParallelOptions(processorCount);

                await PerformParallelExtraction(
                    archiveFiles,
                    extractionOptions,
                    progress,
                    pts,
                    beta,
                    outPath,
                    stopwatch,
                    counters,
                    totalFiles);

                success = this._archivesFinished == archiveFiles.Count;
            }
            catch (Exception ex)
            {
                HandleException(ex);
                success = false;
            }
            finally
            {
                StopProgressMonitoring(progress, stopwatch, ref isRunning);
            }

            return success;
        }

        private List<string> GetArchiveFiles(string assetsPath, bool pts)
        {
            var archiveFiles = new List<string>();
            var allTors = Directory.EnumerateFiles(assetsPath, "*.tor", SearchOption.TopDirectoryOnly);

        archiveFiles.AddRange(pts
            ? allTors.Where(f => Path.GetFileName(f).Contains("swtor_test_")).OrderBy(f => new FileInfo(f).Length)
            : allTors.Where(f => !Path.GetFileName(f).Contains("swtor_test_")).OrderBy(f => new FileInfo(f).Length));

            AddSpecialGraphicsArchive(archiveFiles, assetsPath, pts);
            return archiveFiles;
        }

        private void AddSpecialGraphicsArchive(List<string> archiveFiles, string assetsPath, bool pts)
        {
            var parentDir = Directory.GetParent(assetsPath)?.ToString();
            if (string.IsNullOrEmpty(parentDir)) return;

            var clientPath = pts
                ? Path.Combine(parentDir, Constants.Paths.PublicTestClient)
            : Path.Combine(parentDir, Constants.Paths.LiveClient);

            var gfxTor = Path.Combine(clientPath, Constants.Files.MainGfx);
            if (File.Exists(gfxTor)) archiveFiles.Add(gfxTor);
        }

        private void ValidateArchiveCount(List<string> archiveFiles, bool pts)
        {
            var torCount = Directory.EnumerateFiles(Path.GetDirectoryName(archiveFiles[0]) ?? "", "*.tor").Count(f=>pts?f.Contains("swtor_test_"):!f.Contains("swtor_test_")) + 1;
            if ((pts && archiveFiles.Count < torCount) || (!pts && archiveFiles.Count < torCount))
            {
                throw new InvalidOperationException("Archive count mismatch - check extraction settings");
            }
        }

        private async Task StartProgressMonitoring(
            ProgressTrackers progress,
            Stopwatch stopwatch,
            Func<bool> isRunning)
        {
            _ = Task.Run(async () =>
            {
                while (isRunning())
                {
                    progress.MemoryProgress.Report(Process.GetCurrentProcess().PeakWorkingSet64);
                    progress.TimeProgress.Report(stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
                    await Task.Delay(100);
                }
            });
        }

        private async Task<int> ProcessArchiveHeaders(
            List<string> archiveFiles)
        {
            int totalFiles = 0;
            foreach (var archiveFile in archiveFiles)
            {
                using var fs = File.Open(archiveFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                var archive = new TORArchive(reader, Path.GetFileNameWithoutExtension(archiveFile));

                Interlocked.Add(ref totalFiles, archive.Header.FileCount);
            }

            return totalFiles;
        }

        private ParallelOptions CreateParallelOptions(int degreeOfParallelism) => new()
        {
            MaxDegreeOfParallelism = Math.Max(1, degreeOfParallelism)
        };

        private async Task PerformParallelExtraction(
            List<string> archiveFiles,
            ParallelOptions options,
            ProgressTrackers progress,
            bool pts,
            bool beta,
            string outPath,
            Stopwatch stopwatch,
            ExtractionCounters counters,
            int totalFiles)
        {
            await Parallel.ForEachAsync(archiveFiles, options, async (archiveFile, _) =>
            {
                try
                {
                    using var fs = new FileStream(archiveFile, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read,
                        4096,
                        FileOptions.Asynchronous | FileOptions.RandomAccess);
                    using var reader = new BinaryReader(fs);

                    var archive = new TORArchive(reader, Path.GetFileNameWithoutExtension(archiveFile));
                    await archive.ExtractEntriesAsync(
                        archiveFile,
                        pts,
                        beta,
                        _hashDictionary,
                        outPath,
                        () => UpdateProgress(progress, totalFiles, counters));

                    Interlocked.Increment(ref _archivesFinished);
                    counters.IncrementArchivesFinished();
                    progress.ArcsProgress.Report(counters.ArchivesFinished);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }
            });
            progress.ExtractionProgress.Report(100);
        }

        private void UpdateProgress(
            ProgressTrackers progress,
            int totalFiles,
            ExtractionCounters counters)
        {
            counters.IncrementFilesExtracted();
            progress.ExtractedFilesProgress.Report($"{counters.ProcessedFiles}");
            progress.ExtractionProgress.Report((counters.ProcessedFiles / (double)totalFiles) * 100.0);
        }

        private void StopProgressMonitoring(
            ProgressTrackers progress,
            Stopwatch stopwatch,
            ref bool isRunning)
        {
            isRunning = false;
            stopwatch.Stop();
            progress.TimeProgress.Report(stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
            progress.MemoryProgress.Report(Process.GetCurrentProcess().PeakWorkingSet64);
        }

        private void HandleException(Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
        }

        private class ExtractionCounters
        {
            private int _archivesFinished;
            private int _processedFiles;
            public int ArchivesFinished => _archivesFinished;
            public int ProcessedFiles => _processedFiles;
            public void IncrementArchivesFinished() => Interlocked.Increment(ref _archivesFinished);
            public void IncrementFilesExtracted() => Interlocked.Increment(ref _processedFiles);
        }
    }
}