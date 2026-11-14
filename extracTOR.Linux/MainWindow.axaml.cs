using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using extracTOR.Helper;
using extracTOR.Helper.Constants;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace extracTOR
{
    // Avalonia (Linux) port of ..\extracTOR\MainWindow.xaml.cs.
    // The flow and method names intentionally mirror the WPF version so the two stay easy to compare.
    public partial class MainWindow : Window
    {
        private HashFileHelper _hashFileHelper;
        private FileExtractor _extractor;
        private Stopwatch _stopwatch;
        private ProgressTrackers progressTrackers;
        private readonly DispatcherTimer _glowTimer;
        private readonly Stopwatch _glowClock = new();

        public bool LiveAssets { get; set; }
        public bool BetaAssets { get; set; }
        public bool PTSAssets { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            _glowTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => UpdateGlow());
            InitializeApplication();
        }

        #region Init
        private void InitializeApplication()
        {
            LoadConfiguration();
            InitializeUiState();
            _hashFileHelper = new HashFileHelper((message, title) => MessageDialog.ShowYesNo(this, message, title));
            _extractor = new FileExtractor();
            progressTrackers = CreateProgressTrackers();
            // Dialogs need a visible owner window, so the hash check waits until the window is open.
            Opened += (_, _) => InitializeHashCheck();
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
                await ShowWarningMessage("No Hash List found!\nClick the Check Hash List or Download Hash List button!");
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

            try
            {
                var config = JObject.Parse(File.ReadAllText(Constants.AppSettingsPath));
                tbxAssets.Text = (string?)config["Paths"]?["Assets"] ?? "";
                tbxOutput.Text = (string?)config["Paths"]?["Output"] ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not read {Constants.AppSettingsPath}: {ex.Message}");
                CreateDefaultConfiguration();
            }
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
            if (!File.Exists(Constants.AppSettingsPath)) CreateDefaultConfiguration();
            var json = File.ReadAllText(Constants.AppSettingsPath);
            var config = JObject.Parse(json);
            config["Paths"][key] = value?.ToString() ?? "";
            File.WriteAllText(Constants.AppSettingsPath, config.ToString());
        }

        private void Assets_TextChanged(object? sender, TextChangedEventArgs e) =>
            UpdateConfiguration("Assets", tbxAssets.Text);

        private void Output_TextChanged(object? sender, TextChangedEventArgs e) =>
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
        private async void HandleButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            try
            {
                switch (button.Name)
                {
                    case "Exit":
                        Close();
                        break;
                    case "BtnOpenFolderAssets":
                        await HandleFolderSelection(tbxAssets, "Select Assets Folder");
                        break;
                    case "BtnOpenFolderOutput":
                        await HandleFolderSelection(tbxOutput, "Select Extraction Output Folder");
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
                        await OpenWebLink(button.Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                await HandleException(ex);
            }
        }

        private async Task OpenWebLink(string buttonName)
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

            // Opens in the user's default browser (xdg-open on Linux).
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private void RadioButton_Checked(object? sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radio || radio.IsChecked != true) return;

            LiveAssets = radio == rbLive;
            BetaAssets = radio == rbBeta;
            PTSAssets = radio == rbPTS;
        }

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private async Task HandleFolderSelection(TextBox targetTextBox, string dialogTitle)
        {
            var options = new FolderPickerOpenOptions { Title = dialogTitle, AllowMultiple = false };
            if (!string.IsNullOrWhiteSpace(targetTextBox.Text) && Directory.Exists(targetTextBox.Text))
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(targetTextBox.Text);
            }

            var result = await StorageProvider.OpenFolderPickerAsync(options);
            var path = result.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                targetTextBox.Text = path;
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
                await ShowErrorMessage("Hash List not found!\nCheck Hash List!");
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
            prgBarExt.IsVisible = true;

            try
            {
                if (!await Task.Run(() => _hashFileHelper.CheckInternetConnection()))
                {
                    await ShowWarningMessage("Please check your internet connection!");
                    return;
                }

                bool success = await _hashFileHelper.CheckHashList(progress);
                await ShowInfoMessage(success ? "Hash list up to date!" : "Hash list updated!");
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
            var progress = new Progress<double>(v => prgBarExt.Value = v);
            prgBarExt.IsVisible = true;

            try
            {
                if (await _hashFileHelper.DownloadHashListAsync(progress))
                    await ShowInfoMessage("Hash list downloaded successfully!");
                else
                    await ShowErrorMessage("Download failed!");
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
            StartGlow();

            if (!await ValidatePaths())
            {
                StopGlow();
                ToggleUiState(true);
                return;
            }

            button.Content = "Checking Hashes...";
            if (!await PrepareHashFile())
            {
                StopGlow();
                ToggleUiState(true);
                button.Content = "Extract";
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
                    tbxAssets.Text ?? "",
                    tbxOutput.Text ?? "",
                    PTSAssets,
                    BetaAssets,
                    _stopwatch
                );

                await HandleExtractionResult(success);
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                ToggleUiState(true);
                ResetExtractionState(button);
            }
        }
        private async Task HandleExtractionResult(bool success)
        {
            _stopwatch.Stop();
            var outputPath = (tbxOutput.Text ?? "").TrimEnd(Path.DirectorySeparatorChar);

            if (!success)
            {
                await ShowErrorMessage("Error during extraction!");
                return;
            }

            var subPath = PTSAssets ? Path.Combine("pts", "resources") : BetaAssets ? Path.Combine("beta", "resources") : "resources";
            await ShowInfoMessage($"Extraction complete!\n\nFiles located at: {Path.Combine(outputPath, subPath)}\nPeak Memory Usage: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024.0 / 1024.0 / 1024.0:F2} GB\nTime: {_stopwatch.Elapsed:hh\\:mm\\:ss}");
            _stopwatch.Reset();
        }

        private async Task<bool> ValidatePaths()
        {
            if (string.IsNullOrEmpty(tbxAssets.Text))
            {
                await ShowErrorMessage("No Assets directory selected!");
                return false;
            }

            if (string.IsNullOrEmpty(tbxOutput.Text))
            {
                await ShowErrorMessage("Enter a path for your output folder!");
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
            StopGlow();
        }
        #endregion

        #region Progress bar glow
        // Replaces the WPF "GlowWaveAnimation" storyboard: StartPoint.X sweeps -2 -> 1 over 3s,
        // EndPoint.X sweeps 1 -> 2 over 4s, both repeating forever.
        private LinearGradientBrush prgBarGlow => (LinearGradientBrush)prgBarExt.Foreground!;

        private void StartGlow()
        {
            _glowClock.Restart();
            _glowTimer.Start();
        }

        private void StopGlow()
        {
            _glowTimer.Stop();
            _glowClock.Reset();
            prgBarGlow.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
            prgBarGlow.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
        }

        private void UpdateGlow()
        {
            var t = _glowClock.Elapsed.TotalSeconds;
            var startX = -2.0 + 3.0 * (t % 3.0 / 3.0);
            var endX = 1.0 + 1.0 * (t % 4.0 / 4.0);
            prgBarGlow.StartPoint = new RelativePoint(startX, 0, RelativeUnit.Relative);
            prgBarGlow.EndPoint = new RelativePoint(endX, 0, RelativeUnit.Relative);
        }
        #endregion

        #region Error Handling
        private async Task HandleException(Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorMessage($"An error occurred: {ex.Message}");
        }

        private Task ShowErrorMessage(string message) => MessageDialog.Show(this, message, "Error");

        private Task ShowWarningMessage(string message) => MessageDialog.Show(this, message, "Warning");

        private Task ShowInfoMessage(string message) => MessageDialog.Show(this, message, "Info");
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
}
