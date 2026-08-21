using extracTOR.Helper;
using extracTOR.Helper.Constants;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace extracTOR
{
    public partial class MainWindow : Window
    {
        private IConfiguration _config;
        private HashFileHelper _hashFileHelper;
        private FileExtractor _extractor;
        private Stopwatch _stopwatch;
        private FolderPicker _folderPicker;
        private ProgressTrackers progressTrackers;

        public bool LiveAssets { get; set; }
        public bool BetaAssets { get; set; }
        public bool PTSAssets { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        #region Init
        private void InitializeApplication()
        {
            LoadConfiguration();
            InitializeUiState();
            _hashFileHelper = new HashFileHelper(this);
            _extractor = new FileExtractor();
            progressTrackers = CreateProgressTrackers();
            InitializeHashCheck();
        }
        private void InitializeUiState()
        {
            chkStats.IsChecked = true;
            rbLive.IsChecked = true;
        }
        private async void InitializeHashCheck()
        {
            if (!File.Exists(Constants.HashesFilenamePath))
            {
                ShowWarningMessage("No Hash List found!\nClick the Check Hash List or Download Hash List button!");
            }
            await Task.Run(() => LoadHashList());
        }
        #endregion

        #region config
        private void LoadConfiguration()
        {
            if (!File.Exists(Constants.AppSettingsPath))
            {
                CreateDefaultConfiguration();
            }

            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Constants.AppSettingsFileName)
                .Build();

            tbxAssets.Text = _config[Constants.ConfigKeys.AssetsPath];
            tbxOutput.Text = _config[Constants.ConfigKeys.OutputPath];
        }
        private void CreateDefaultConfiguration()
        {
            var defaultConfig = new JObject(
                new JProperty("Paths",
                    new JObject(
                        new JProperty("Assets", ""),
                        new JProperty("Output", "")
                    )
                )
            );

            File.WriteAllText(Constants.AppSettingsPath, defaultConfig.ToString());
        }

        private void UpdateConfiguration<T>(string key, T value)
        {
            var json = File.ReadAllText(Constants.AppSettingsPath);
            var config = JObject.Parse(json);
            config["Paths"][key] = value.ToString();
            File.WriteAllText(Constants.AppSettingsPath, config.ToString());
        }

        private void Assets_TextChanged(object sender, TextChangedEventArgs e) =>
            UpdateConfiguration("Assets", tbxAssets.Text);

        private void Output_TextChanged(object sender, TextChangedEventArgs e) =>
            UpdateConfiguration("Output", tbxOutput.Text);
        #endregion

        #region UI and Controls
        private void ToggleUiState(bool enable)
        {
            var controls = new Control[] {
                BtnCheckHash, BtnDLHash, BtnExtract,
                BtnOpenFolderAssets, BtnOpenFolderOutput,
                tbxAssets, tbxOutput, rbLive, rbPTS, rbBeta
            };

            foreach (var control in controls)
            {
                control.IsEnabled = enable;
            }
        }
        private async void HandleButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            try
            {
                switch (button.Name)
                {
                    case "Exit":
                        Application.Current.Shutdown();
                        break;
                    case "BtnOpenFolderAssets":
                        HandleFolderSelection(button, tbxAssets, "Select Assets Folder");
                        break;
                    case "BtnOpenFolderOutput":
                        HandleFolderSelection(button, tbxOutput, "Select Extraction Output Folder");
                        break;
                    case "BtnCheckHash":
                        await HandleHashCheck(button);
                        break;
                    case "BtnDLHash":
                        await HandleHashDownload(button);
                        break;
                    case "BtnExtract":
                        await HandleFileExtraction(button);
                        break;
                    case "Jedipedia":
                    case "Blender":
                    case "Discord":
                    case "Github":
                    case "TORCom":
                        OpenWebLink(button.Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void OpenWebLink(string buttonName)
        {
            var url = buttonName switch
            {
                "Jedipedia" => Constants.URLs.Jedipedia,
                "Blender" => Constants.URLs.Blender,
                "Discord" => Constants.URLs.Discord,
                "Github" => Constants.URLs.Github,
                "TORCom" => Constants.URLs.TorCommunity,
                _ => throw new ArgumentException("Unknown button")
            };

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton radio)) return;

            LiveAssets = radio == rbLive;
            BetaAssets = radio == rbBeta;
            PTSAssets = radio == rbPTS;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void HandleFolderSelection(Button button, TextBox targetTextBox, string dialogTitle)
        {
            _folderPicker = new FolderPicker { Title = dialogTitle };
            if (_folderPicker.ShowDialog() == true)
            {
                targetTextBox.Text = _folderPicker.ResultPath;
            }
        }
        #endregion

        #region HashList
        private async Task LoadHashList()
        {

            if (File.Exists(Constants.HashesFilenamePath))
            {
                await Task.Run(() => _extractor.LoadHashDictionary(Constants.HashesFilenamePath));
            }

        }

        private async Task<bool> PrepareHashFile()
        {

            if (!File.Exists(Constants.HashesFilenamePath))
            {
                ShowErrorMessage("Hash List not found!\nCheck Hash List!");
                return false;
            }

            return true;
        }

        private async Task HandleHashCheck(Button button)
        {
            ToggleUiState(false);
            button.Width = 120;
            button.Content = "Checking Hash Lists...";

            prgBarExt.Value = 0;
            var progress = new Progress<double>(v => prgBarExt.Value = v);
            prgBarExt.Visibility = Visibility.Visible;

            try
            {

                if (!await Task.Run(() => _hashFileHelper.CheckInternetConnection()))
                {
                    ShowWarningMessage("Please check your internet connection!");
                    return;
                }

                bool success = await _hashFileHelper.CheckHashList(progress);
                ShowInfoMessage(success ? "Hash list up to date!" : "Hash list updated!");
            }
            finally
            {
                button.Width = 117;
                button.Content = "Check Hash List";
                prgBarExt.Value = 0;
                ToggleUiState(true);
            }
        }

        private async Task HandleHashDownload(Button button)
        {
            ToggleUiState(false);

            prgBarExt.Value = 0;
            button.Width = 140;
            button.Margin = new Thickness(3.0, 5.0, 3.0, 5.0);
            button.Content = "Downloading Hash List...";
            //Content="Downloading Hash List..." Margin="3,5,3,5" Width="140"
            var progress = new Progress<double>(v => prgBarExt.Value = v);
            prgBarExt.Visibility = Visibility.Visible;

            try
            {
                if (await _hashFileHelper.DownloadHashListAsync(progress))
                    ShowInfoMessage($"Hash list downloaded successfully!");
                else
                    ShowErrorMessage("Download failed!");
            }
            finally
            {
                button.Width = 117;
                button.Margin = new Thickness(3.0, 5.0, 3.0, 5.0);
                button.Content = "Download Hash List";
                prgBarExt.Value = 0;
                ToggleUiState(true);
            }
        }
        #endregion

        #region Extraction
        private async Task HandleFileExtraction(Button button)
        {
            _stopwatch = new Stopwatch();
            ToggleUiState(false);
            ((Storyboard)Resources["GlowWaveAnimation"]).Begin();

            if (!ValidatePaths())
            {
                ToggleUiState(true);
                return;
            }

            button.Content = "Checking Hashes...";
            if (!await Task.Run(() => PrepareHashFile()))
            {
                ToggleUiState(true);
                return;
            }


            button.Content = "Loading Hashes...";
            await Task.Run(() => LoadHashList());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            progressTrackers.ArcsProgress.Report(0);
            progressTrackers.ExtractedFilesProgress.Report("0");
            progressTrackers.TotalFilesProgress.Report("0");
            progressTrackers.ExtractionProgress.Report(0);
            progressTrackers.MemoryProgress.Report(0);
            progressTrackers.TimeProgress.Report("0:00:00");

            _stopwatch.Reset();
            _stopwatch.Start();

            button.Content = "Extracting...";
            try
            {
                var success = await _extractor.ExtractFilesAsync(
                    progressTrackers,
                    tbxAssets.Text,
                    tbxOutput.Text,
                    PTSAssets,
                    BetaAssets,
                    _stopwatch
                );

                HandleExtractionResult(success);
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                ToggleUiState(true);
                ResetExtractionState(button);
            }
        }
        private void HandleExtractionResult(bool success)
        {
            _stopwatch.Stop();
            var outputPath = tbxOutput.Text.TrimEnd('\\');

            if (!success)
            {
                ShowErrorMessage("Error during extraction!");
                return;
            }

            var subPath = PTSAssets ? "pts\\resources" : BetaAssets ? "beta\\resources" : "resources";
            ShowInfoMessage($"Extraction complete!\n\nFiles located at: {Path.Combine(outputPath, subPath)}\nPeak Memory Usage: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024.0 / 1024.0 / 1024.0:F2} GB\nTime: {_stopwatch.Elapsed:hh\\:mm\\:ss}");
            _stopwatch.Reset();
        }

        private bool ValidatePaths()
        {
            if (string.IsNullOrEmpty(tbxAssets.Text))
            {
                ShowErrorMessage("No Assets directory selected!");
                return false;
            }

            if (string.IsNullOrEmpty(tbxOutput.Text))
            {
                ShowErrorMessage("Enter a path for your output folder!");
                return false;
            }

            return true;
        }

        private void ResetExtractionState(Button button)
        {
            _stopwatch.Reset();
            progressTrackers.ArcsProgress.Report(0);
            progressTrackers.ExtractedFilesProgress.Report("0");
            progressTrackers.TotalFilesProgress.Report("0");
            progressTrackers.ExtractionProgress.Report(0);
            progressTrackers.MemoryProgress.Report(0);
            progressTrackers.TimeProgress.Report("0:00:00");
            prgBarExt.Value = 0;
            button.Content = "Extract";
            ToggleUiState(true);
            ((Storyboard)Resources["GlowWaveAnimation"]).Stop();
        }
        #endregion

        #region Error Handling
        private void HandleException(Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            ShowErrorMessage($"An error occurred: {ex.Message}");
        }

        private void ShowErrorMessage(string message) =>
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        private void ShowWarningMessage(string message) =>
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void ShowInfoMessage(string message) =>
            MessageBox.Show(message, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        #endregion


        #region Progress Trackers
        private ProgressTrackers CreateProgressTrackers() => new ProgressTrackers
        {
            ExtractionProgress = new Progress<double>(v => prgBarExt.Value = v),
            TimeProgress = new Progress<string>(t => prgTime.Text = t),
            MemoryProgress = new Progress<long>(m => statMem.Text = $"{m / 1024.0 / 1024.0 / 1024.0:F2}"),
            TotalFilesProgress = new Progress<string>(t => prgExTotalFiles.Text = $"{int.Parse(t):N0}"),
            ExtractedFilesProgress = new Progress<string>(e => prgExFiles.Text = $"{int.Parse(e):N0}"),
            ArcsProgress = new Progress<int>(a => arcsComplete.Text = $"{a}")
        };
        #endregion
    }

    public class ProgressTrackers
    {
        public IProgress<double> ExtractionProgress { get; set; }
        public IProgress<string> TimeProgress { get; set; }
        public IProgress<long> MemoryProgress { get; set; }
        public IProgress<string> TotalFilesProgress { get; set; }
        public IProgress<string> ExtractedFilesProgress { get; set; }
        public IProgress<int> ArcsProgress { get; set; }
    }

}