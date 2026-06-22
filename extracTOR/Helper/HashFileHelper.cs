using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace extracTOR.Helper
{
    public class HashFileHelper
    {
        private MainWindow _m;

        public HashFileHelper(MainWindow mainWindow)
        {
            _m = mainWindow;
        }

        private string remoteURL = "http://swtor.jedipedia.net/ajax/getFileNames.php?env=all&format=easymyp";
        private string localFile = $"{Environment.CurrentDirectory}\\hashes.bin";

        public async Task<DateTime?> GetHashModifiedTimeAsync(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Head, fileUrl);

                HttpResponseMessage resp = await client.SendAsync(req);

                if (resp.Content.Headers.TryGetValues("Last-Modified", out var values))
                {
                    string lastModified = values.FirstOrDefault();
                    if (DateTime.TryParse(lastModified, out DateTime lastMod))
                    {
                        return lastMod.ToUniversalTime();
                    }
                }

                return null;
            }
        }

        public async Task<bool> CheckInternetConnection()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var resp = await client.GetAsync("https://www.google.com");
                    return resp.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> CompareFilesAsync(string local, string remote)
        {
            string localPath = local;
            string remotePath = remote;

            DateTime? localMod = GetLocalLastMod(local);
            DateTime? remoteMod = await GetHashModifiedTimeAsync(remotePath);
            if (!File.Exists(local))
            {
                return "REMOTE";
            }
            if (remoteMod.HasValue)
            {
                if (localMod >= remoteMod.Value)
                {
                    return $"LOCAL {local}: {localMod}";
                }
                else
                {
                    return $"REMOTE {remote}: {remoteMod}";
                }
            }
            else
            {
                return $"Error";
            }
        }

        public async Task<bool> CheckHashList(IProgress<double> progress)
        {
            try
            {
                progress.Report(10);
                var local = Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin");
                var comparisonResult = await CompareFilesAsync(local, remoteURL);
                if (comparisonResult.Contains("LOCAL"))
                {
                    progress.Report(100);
                    return true;
                }
                if (comparisonResult.Contains("REMOTE"))
                {
                    progress.Report(30);
                    var downloadResult = await ShowDownloadPromptAndExecute(progress);
                    progress.Report(100);
                    return downloadResult;
                }
                progress.Report(100);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Excpetion in CheckHashList(progress): {ex.Message}");
                progress.Report(0);
                return false;
            }
        }

        private async Task<bool> ShowDownloadPromptAndExecute(IProgress<double> progress)
        {
            var result = MessageBox.Show(
                "Hash List Update required. Download now?",
                "Update Available",
                MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return false;
            var tmp = Path.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt");
            var success = await DownloadHashListAsync(progress);
            if (success)
            {
                progress.Report(90);
                return await BinarizeHashListAsync(tmp, progress);
            }
            return false;
        }

        public async Task<bool> DLHashList()
        {
            bool result = false;
            await Task.Run(async () =>
            {
                string dlPath = Path.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt");
                if (File.Exists(Path.GetFileName(Path.GetFileNameWithoutExtension(dlPath) + ".bak")))
                {
                    File.Delete(Path.GetFileName(Path.GetFileNameWithoutExtension(dlPath) + ".bak"));
                }
                if (File.Exists(dlPath))
                {
                    File.Copy(dlPath, Path.GetFileNameWithoutExtension(dlPath) + ".bak");
                    File.Delete(dlPath);
                }
                try
                {
                    bool? dlSuccess = await DownloadHashList(dlPath, remoteURL);
                    if (dlSuccess.Value == true)
                    {
                        if (File.Exists(dlPath))
                        {
                            await BinarizeHashListAsync(dlPath);
                            result = true;
                        }
                    }
                    else
                        result = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"HashFileHelper::DLHashList() {ex.Message}\n{ex.StackTrace}");
                }
            });
            return result;
        }

        public async Task<bool> DownloadHashListAsync(IProgress<double> progress)
        {
            string dlPath = Path.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt");
            return await DownloadHashListAsync(dlPath, remoteURL, progress);
        }

        public async Task<bool> DownloadHashListAsync(string path, string url, IProgress<double> progress)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var buffer = new byte[4096];
                        var totalRead = 0L;
                        var bytesRead = 0;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            progress.Report(totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0);
                            totalRead += bytesRead;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Excpetion in DownloadHashListAsync(progress): {ex.Message}");
                progress.Report(0);
                return false;
            }
        }

        public async Task<bool> DownloadHashList(string path, string url)
        {
            bool result = false;
            await Task.Run(async () =>
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        HttpResponseMessage resp = await client.GetAsync(url);
                        resp.EnsureSuccessStatusCode();  // This will throw if the response code is not 2xx
                        byte[] fileBuffer = await resp.Content.ReadAsByteArrayAsync();

                        if (!File.Exists(path))
                            File.Create(path).Dispose(); // Properly dispose the file stream
                        else
                            File.Delete(path);

                        File.WriteAllBytes(path, fileBuffer);
                        result = true;
                    }
                    catch (HttpRequestException ex)
                    {
                        result = false;
                    }
                    catch (Exception ex)
                    {
                        result = false;
                    }
                }
            });
            return result;

        }

        public DateTime GetLocalLastMod(string filePath)
        {
            return File.GetLastWriteTimeUtc(filePath);
        }

        public async Task<bool> BinarizeHashListAsync(string txtPath, IProgress<double> progress)
        {
            const int bufferSize = 4096;
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin")))
                    File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin"));
                using (var fs = new FileStream(txtPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
                using (var reader = new StreamReader(fs))
                using (var bw = new BinaryWriter(File.OpenWrite(Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin"))))
                {
                    var totalBytes = fs.Length;
                    var bytesProcessed = 0L;
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        bytesProcessed += line.Length + Environment.NewLine.Length;
                        progress.Report((double)bytesProcessed / totalBytes * 100);
                        string[] parts = line.Split('#');
                        if (parts.Length == 4)
                        {
                            uint ph = Convert.ToUInt32(parts[0], 16);
                            uint sh = Convert.ToUInt32(parts[1], 16);
                            ulong hash = ((ulong)ph << 32) | sh;
                            byte[] bytes = BitConverter.GetBytes(hash);
                            bw.Write(hash);
                            string filename = parts[2] + "\0";
                            byte[] filenameBytes = Encoding.UTF8.GetBytes(filename);
                            bw.Write((int)filenameBytes.Length);
                            bw.Write(filenameBytes);
                            bw.Flush();
                        }
                    }
                }
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin")))
                {
                    File.SetLastWriteTime(Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin"), DateTime.Now);
                }
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task<bool> BinarizeHashListAsync(string txtPath)
        {
            bool res = false;
            await Task.Run(() =>
            {
                string binOut = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin");
                if (File.Exists(binOut))
                {
                    File.Delete(binOut);
                }
                using (var bw = new BinaryWriter(File.Open(binOut, FileMode.Create)))
                {
                    foreach (var line in File.ReadLines(txtPath))
                    {
                        string[] parts = line.Split('#');
                        if (parts.Length == 4)
                        {
                            uint pHash = Convert.ToUInt32(parts[0], 16);
                            uint sHash = Convert.ToUInt32(parts[1], 16);
                            ulong hash64 = ((ulong)pHash << 32) | sHash;
                            byte[] bytes = BitConverter.GetBytes(hash64);
                            bw.Write(hash64);
                            string filename = parts[2] + "\0";
                            byte[] filenameBytes = Encoding.UTF8.GetBytes(filename);
                            bw.Write((int)filenameBytes.Length);
                            bw.Write(filenameBytes);
                            res = true;
                        }
                        else
                        {
                            res = false;
                        }
                    }
                }
                if (File.Exists(binOut))
                {
                    File.SetLastWriteTime(binOut, DateTime.Now);
                    res = true;
                }
                else
                {
                    res = false;
                }
                File.Delete(txtPath);
            });
            return res;
        }
    }
}
