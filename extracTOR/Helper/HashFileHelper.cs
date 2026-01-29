using System;
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
        public async Task<string?> CompareFilesAsync(string local, string remote)
        {
            string localPath = local;
            string remotePath = remote;

            DateTime? localMod = GetLocalLastMod(localPath);
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

        public async Task<bool?> CheckHashList()
        {
            bool? res = true;
            await Task.Run(async () =>
            {
                string local = Path.Combine(Directory.GetCurrentDirectory(), "hashes.bin");
                Task<string?> mostRecent = CompareFilesAsync(local, remoteURL);
                if (mostRecent.Result.Contains("LOCAL"))
                {
                    res = true;
                    return res;
                }
                else if (mostRecent.Result.Contains("REMOTE"))
                {
                    MessageBoxResult result = MessageBox.Show("Hash List update required. Download now?", "", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        res = await DLHashList();
                    }
                    return res;
                }
                else
                {
                    res = null;
                    return res;
                }
            });
            return res;
        }

        public async Task<bool?> DLHashList()
        {
            bool? result = false;
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

        public async Task<bool?> DownloadHashList(string path, string url)
        {
            bool? result = false;
            await Task.Run(async () =>
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        HttpResponseMessage resp = await client.GetAsync(url);
                        resp.EnsureSuccessStatusCode();
                        byte[] fileBuffer = await resp.Content.ReadAsByteArrayAsync();

                        if (!File.Exists(path))
                            File.Create(path).Dispose();
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

        public async void BinarizeHashList(string path)
        {
            await BinarizeHashListAsync(path);
        }

        public async Task<bool?> BinarizeHashListAsync(string txtPath)
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
