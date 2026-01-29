using extracTOR;
using System.Diagnostics;
using System.IO;
using System.Windows;
using p = System.Diagnostics.Process;

public class FileExtractor
{
    private Dictionary<ulong, string> LoadHashDictionary()
    {
        try
        {
            var dictionary = new Dictionary<ulong, string>();
            using (BinaryReader hashReader = new(File.Open($"{Directory.GetCurrentDirectory()}\\hashes.bin", FileMode.Open)))
            {
                while (hashReader.BaseStream.Position < hashReader.BaseStream.Length)
                {
                    dictionary.Add(hashReader.ReadUInt64(), hashReader.ReadString(true));
                }
            }
            return dictionary;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
            Debugger.Break();
            return null;
        }
    }
    double pct = 0;
    public async Task<bool?> ExtractFilesAsync(IProgress<double> prg, IProgress<string>? time, IProgress<long>? mem, IProgress<string>? totalFile, IProgress<string>? extr, IProgress<int>? arcsPrg, string assetsPath, string outPath, bool pts, bool beta, Stopwatch sw, bool isRunning)
    {
        int archivesFinished = 0;
        bool success = false;
        try
        {
            if (mem != null)
            {
                async Task UpdatePeakMemAsync()
                {
                    while (isRunning)
                    {
                        mem?.Report(p.GetCurrentProcess().PeakWorkingSet64);
                        await Task.Delay(100);
                    }
                }
                var memTask = UpdatePeakMemAsync();
            }
            if (time != null)
            {
                async Task UpdateTimerAsync()
                {
                    while (isRunning)
                    {
                        time?.Report(sw.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
                        await Task.Delay(10);
                    }
                }

                var timerTask = UpdateTimerAsync();
            }

            List<string> archiveFiles = new();

            var allTors = Directory.EnumerateFiles(assetsPath, "*.tor", SearchOption.TopDirectoryOnly);

            if (pts)
            {
                archiveFiles.AddRange(allTors.Where(file => Path.GetFileName(file).Contains("_test_")));
                string ptsClient = Path.Combine(Directory.GetParent(assetsPath).ToString(), "publictest\\retailclient");
                string gfxTor = Path.Combine(ptsClient, "main_gfx_1.tor");
                if (File.Exists(gfxTor))
                {
                    archiveFiles.Add(gfxTor);
                }
            }
            else
            {
                archiveFiles.AddRange(allTors.Where(file => !Path.GetFileName(file).Contains("_test_")));
                string liveClient = Path.Combine(Directory.GetParent(assetsPath).ToString(), "swtor\\retailclient");
                string gfxTor = Path.Combine(liveClient, "main_gfx_1.tor");
                if (File.Exists(gfxTor))
                {
                    archiveFiles.Add(gfxTor);
                }
            }
            if (pts && archiveFiles.Count < allTors.Count())
            {
                isRunning = false;
                sw.Stop();
                time?.Report(sw.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
                mem?.Report(p.GetCurrentProcess().PeakWorkingSet64);
                MessageBox.Show($"An error occurred during extraction. Check your extract settings and try again.", "Extraction Setting error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!pts && archiveFiles.Count < allTors.Count())
            {
                isRunning = false;
                sw.Stop();
                time?.Report(sw.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
                mem?.Report(p.GetCurrentProcess().PeakWorkingSet64);
                MessageBox.Show($"An error occurred during extraction. Check your extract settings and try again.", "Extraction Setting error", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;
            }
            allTors = null;
            var numFilesTotal = 0;
            var filesProcessed = 0;
            Dictionary<ulong, string> hashDictionary = LoadHashDictionary();

            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            await Parallel.ForEachAsync(archiveFiles, options, async (archiveFile, _) =>
            {
                using (FileStream fs = new(archiveFile, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    try
                    {
                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        string arc = Path.GetFileNameWithoutExtension(archiveFile);
                        var archive = new TORArchive(br, arc);
                        Interlocked.Add(ref numFilesTotal, archive.Header.FileCount);
                        if (totalFile != null)
                            totalFile?.Report($"{numFilesTotal}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
                        Debugger.Break();
                    }
                }
            });


            Action reportProgress = () =>
            {
                pct = ((Interlocked.Increment(ref filesProcessed) / (double)numFilesTotal) * 100);
                if (extr != null)
                    extr?.Report($"{filesProcessed}");
                prg?.Report(pct);
            };

            ParallelOptions optionsExtract = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            sw.Start();
            List<string> hashes = new();
            await Parallel.ForEachAsync(archiveFiles, optionsExtract, async (archiveFile, _) =>
            {
                try
                {
                    using (FileStream fs = new(archiveFile, FileMode.Open, FileAccess.Read))
                    using (BinaryReader br = new(fs))
                    {
                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        string arc = Path.GetFileNameWithoutExtension(archiveFile);
                        var archive = new TORArchive(br, arc);
                        await archive.ExtractEntriesAsync(br, arc, archiveFile, pts, beta, hashDictionary, outPath, reportProgress, hashes);
                    }
                    if (arcsPrg != null)
                    {
                        Interlocked.Increment(ref archivesFinished);
                        arcsPrg?.Report(archivesFinished);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
                    Debugger.Break();
                }
            });
            prg?.Report(100);

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
            Debugger.Break();
        }
        finally
        {
            isRunning = false;
            sw.Stop();
            time?.Report(sw.Elapsed.ToString(@"hh\:mm\:ss\.ff"));
            mem?.Report(p.GetCurrentProcess().PeakWorkingSet64);
        }
        if (archivesFinished == 0)
            success = false;
        else if (archivesFinished > 0)
            success = true;

        return success;
    }
}