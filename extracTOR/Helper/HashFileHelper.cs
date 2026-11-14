using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using extracTOR.Helper.Constants;

namespace extracTOR.Helper
{
    public class HashFileHelper
    {
        // UI-supplied Yes/No prompt (message, title) -> true if the user chose Yes.
        // Keeps this class free of any WPF/Avalonia dependency so both front-ends can share it.
        private readonly Func<string, string, Task<bool>> _confirm;

        public HashFileHelper(Func<string, string, Task<bool>> confirm)
        {
            _confirm = confirm;
        }

        private string remoteURL = "http://swtor.jedipedia.net/ajax/getFileNames.php?env=all&format=txt";
        private string localFile = Constants.Constants.HashesFilenamePath;

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
                var local = localFile;
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
            var result = await _confirm(
                "Hash List Update required. Download now?",
                "Update Available");
            if (!result) return false;
            var success = await DownloadHashListAsync(progress);
            if (success)
            {
                progress.Report(100);
                return true;
                //return await BinarizeHashListAsync(tmp, progress);
            }
            return false;
        }

        public async Task<bool> DownloadHashListAsync(IProgress<double> progress)
        {
            string dlPath = Constants.Constants.HashesFilenamePath;
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

        public DateTime GetLocalLastMod(string filePath)
        {
            return File.GetLastWriteTimeUtc(filePath);
        }


    }
}
