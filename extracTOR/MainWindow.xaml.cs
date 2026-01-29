using extracTOR.Helper;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using p = System.IO.Path;
using System.Windows.Media.Animation;

namespace extracTOR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IConfiguration _config;
        private string assetsFolder = "";
        private string outputFolder = "";
        private bool _ptsAssets;
        private bool _betaAssets;
        private bool _liveAssets;
        public bool LiveAssets
        {
            get => _liveAssets;
            set
            {
                _liveAssets = value;
            }
        }
        public bool BetaAssets
        {
            get => _betaAssets;
            set
            {
                _betaAssets = value;
            }
        }
        public bool PTSAssets
        {
            get => _ptsAssets;
            set
            {
                _ptsAssets = value;
                //Debug.WriteLine($"PTS Assets: {value}");
            }
        }

        private void PTS_Checked(object sender, RoutedEventArgs e)
        {
            PTSAssets = true;
            BetaAssets = false;
            LiveAssets = false;
        }
        private void Live_Checked(object sender, RoutedEventArgs e)
        {
            LiveAssets = true;
            PTSAssets = false;
            BetaAssets = false;
        }
        private void Beta_Checked(object sender, RoutedEventArgs e)
        {
            BetaAssets = true;
            PTSAssets = false;
            LiveAssets = false;
        }

        HashFileHelper hfh;
        FolderPicker dlg;
        FileExtractor _extractor;

        private TextBox assets
        {
            get { return tbxAssets; }
        }

        private TextBox output
        {
            get { return tbxOutput; }
        }

        private ProgressBar prgExtract
        {
            get { return prgBarExt; }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            chkStats.IsChecked = false;
            rbLive.IsEnabled = true;
            rbLive.IsChecked = true;
            rbPTS.IsEnabled = true;
            rbBeta.IsEnabled = true;
            hfh = new HashFileHelper(this);
            _extractor = new FileExtractor();
            InitHashCheck();
        }

        private async void InitHashCheck()
        {
            if (File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt")) && !File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes.bin")))
            {
                bool? binarize = await hfh.BinarizeHashListAsync(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt"));
                if (binarize.Value != true)
                    MessageBox.Show($"Error converting hash list! Contact dev!", "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (!File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt")) && !File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes.bin")))
            {
                MessageBox.Show($"No Hash List found!\nClick the Check Hash List or Download Hash List button!", "Hash List not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadConfig()
        {
            if (!File.Exists($"{Directory.GetCurrentDirectory()}\\appSettings.json"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"Paths\": {");
                sb.AppendLine("    \"Assets\": \"\",");
                sb.AppendLine("    \"Output\": \"\"");
                sb.AppendLine("  }");
                sb.AppendLine("}");
                File.WriteAllText("appSettings.json", sb.ToString());
            }
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true);
            _config = builder.Build();
            assets.Text = _config["Paths:Assets"];
            output.Text = _config["Paths:Output"];
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                switch (btn.Name)
                {
                    #region Exit
                    case "Exit":
                        Environment.Exit(0);
                        break;
                    #endregion
                    #region Assets Folder
                    case "BtnOpenFolderAssets":
                        dlg = new FolderPicker();
                        dlg.Title = "Select Assets Folder";
                        if (dlg.ShowDialog() == true)
                        {
                            tbxAssets.Text = dlg.ResultPath;
                        }
                        break;
                    #endregion
                    #region Output Folder
                    case "BtnOpenFolderOutput":
                        dlg = new FolderPicker();
                        dlg.Title = "Select Extraction Output Folder";
                        if (dlg.ShowDialog() == true)
                        {
                            tbxOutput.Text = dlg.ResultPath;
                        }
                        break;
                    #endregion
                    #region Check Hash
                    case "BtnCheckHash":
                        btn.IsEnabled = false;
                        BtnExtract.IsEnabled = false;
                        BtnDLHash.IsEnabled = false;
                        BtnOpenFolderAssets.IsEnabled = false;
                        BtnOpenFolderOutput.IsEnabled = false;
                        tbxAssets.IsEnabled = false;
                        tbxOutput.IsEnabled = false;
                        rbBeta.IsEnabled = false;
                        rbLive.IsEnabled = false;
                        rbPTS.IsEnabled = false;
                        chkStats.IsEnabled = false;
                        try
                        {
                            bool isConnected = await hfh.CheckInternetConnection();
                            if (isConnected)
                            {
                                btn.Content = "Checking Hash Lists...";
                                if (File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt")))
                                {
                                    bool? binarize = await hfh.BinarizeHashListAsync(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt"));
                                    if (binarize != true)
                                    {
                                        btn.Content = "Check Hash List";
                                        MessageBox.Show($"Error converting hash list! Contact dev!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                else
                                {
                                    bool? cHL = await hfh.CheckHashList();
                                    if (cHL.Value == true)
                                    {
                                        btn.Content = "Check Hash List";
                                        MessageBox.Show("Hash list check complete!", "", MessageBoxButton.OK, MessageBoxImage.Information);
                                        GC.Collect();
                                        GC.WaitForPendingFinalizers();
                                    }
                                }
                            }
                            else
                            {
                                btn.Content = "Check Hash List";
                                MessageBox.Show("Couldn't check hash list.\nPlease check your internet connection and try again!", "No Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        finally
                        {
                            btn.IsEnabled = true;
                            BtnExtract.IsEnabled = true;
                            BtnDLHash.IsEnabled = true;
                            BtnOpenFolderAssets.IsEnabled = true;
                            BtnOpenFolderOutput.IsEnabled = true;
                            tbxAssets.IsEnabled = true;
                            tbxOutput.IsEnabled = true;
                            rbBeta.IsEnabled = true;
                            rbLive.IsEnabled = true;
                            rbPTS.IsEnabled = true;
                            chkStats.IsEnabled = true;
                        }
                        break;
                    #endregion
                    #region Download Hash
                    case "BtnDLHash":
                        btn.IsEnabled = false;
                        BtnExtract.IsEnabled = false;
                        BtnCheckHash.IsEnabled = false;
                        BtnOpenFolderAssets.IsEnabled = false;
                        BtnOpenFolderOutput.IsEnabled = false;
                        tbxAssets.IsEnabled = false;
                        tbxOutput.IsEnabled = false;
                        rbBeta.IsEnabled = false;
                        rbLive.IsEnabled = false;
                        rbPTS.IsEnabled = false;
                        chkStats.IsEnabled = false;
                        try
                        {
                            bool isConnected = await hfh.CheckInternetConnection();
                            if (isConnected)
                            {
                                btn.Width = 145;
                                btn.Content = "Downloading Hash List...";
                                bool? dLH = await hfh.DLHashList();
                                if (dLH.Value == true)
                                {
                                    btn.Width = 117;
                                    btn.Content = "Download Hash List";
                                    MessageBox.Show("Hash list download complete!", "", MessageBoxButton.OK, MessageBoxImage.Information);
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                }
                                else
                                {
                                    btn.Width = 117;
                                    btn.Content = "Donwload Hash List";
                                    MessageBox.Show("Error downloading hash list!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                btn.Width = 117;
                                btn.Content = "Download Hash List";
                                MessageBox.Show("Couln't download Hash List.\nPlease check your internet connection and try again!", "No Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        finally
                        {
                            btn.IsEnabled = true;
                            BtnExtract.IsEnabled = true;
                            BtnCheckHash.IsEnabled = true;
                            BtnOpenFolderAssets.IsEnabled = true;
                            BtnOpenFolderOutput.IsEnabled = true;
                            tbxAssets.IsEnabled = true;
                            tbxOutput.IsEnabled = true;
                            rbBeta.IsEnabled = true;
                            rbLive.IsEnabled = true;
                            rbPTS.IsEnabled = true;
                            chkStats.IsEnabled = true;
                        }
                        break;
                    #endregion
                    #region Extract
                    case "BtnExtract":
                        GC.Collect();
                        GC.WaitForFullGCComplete();
                        btn.Content = "Extracting...";
                        if (File.Exists($"{p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt")}") && !File.Exists(p.Combine(Directory.GetCurrentDirectory(), "hashes.bin")))
                        {
                            bool? bHL = await hfh.BinarizeHashListAsync(p.Combine(Directory.GetCurrentDirectory(), "hashes_filename.txt"));
                            if (bHL.Value != true)
                            {
                                MessageBox.Show("Error Converting hash list! Contact dev!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                            }
                        }
                        if (!File.Exists($"{p.Combine(Directory.GetCurrentDirectory(), "hashes.bin")}"))
                        {
                            MessageBox.Show($"Hash List not found!\nCheck Hash List!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }

                        btn.IsEnabled = false;
                        BtnCheckHash.IsEnabled = false;
                        BtnDLHash.IsEnabled = false;
                        tbxAssets.IsEnabled = false;
                        tbxOutput.IsEnabled = false;
                        BtnOpenFolderAssets.IsEnabled = false;
                        BtnOpenFolderOutput.IsEnabled = false;
                        rbLive.IsEnabled = false;
                        rbPTS.IsEnabled = false;
                        rbBeta.IsEnabled = false;
                        prgBarExt.Value = 0;

                        string outPath = tbxOutput.Text;
                        if (outPath.Length == 3)
                            outPath = outPath.Substring(0, outPath.Length - 1);

                        if (String.IsNullOrEmpty(tbxAssets.Text))
                        {
                            MessageBox.Show($"No Assets directory selected!");
                            btn.IsEnabled = true;
                            BtnCheckHash.IsEnabled = true;
                            BtnDLHash.IsEnabled = true;
                            tbxAssets.IsEnabled = true;
                            tbxOutput.IsEnabled = true;
                            rbLive.IsEnabled = true;
                            rbPTS.IsEnabled = true;
                            rbBeta.IsEnabled = true;
                            BtnOpenFolderAssets.IsEnabled = true;
                            BtnOpenFolderOutput.IsEnabled = true;
                            break;
                        }
                        if (String.IsNullOrEmpty(tbxOutput.Text))
                        {
                            MessageBox.Show($"Enter a path for your output folder!");
                            btn.IsEnabled = true;
                            BtnCheckHash.IsEnabled = true;
                            BtnDLHash.IsEnabled = true;
                            tbxAssets.IsEnabled = true;
                            tbxOutput.IsEnabled = true;
                            rbLive.IsEnabled = true;
                            rbPTS.IsEnabled = true;
                            rbBeta.IsEnabled = true;
                            BtnOpenFolderAssets.IsEnabled = true;
                            BtnOpenFolderOutput.IsEnabled = true;
                            break;
                        }
                        else
                        {
                            prgExFiles.Text = "";
                            prgExTotalFiles.Text = "";
                            prgTime.Text = String.Empty;
                            prgExPrg.Text = "";
                            statMem.Text = "0";
                            prgBarExt.Value = 0;
                            prgExSep.Visibility = Visibility.Collapsed;
                            prgExPrg.Visibility = Visibility.Collapsed;
                            sw = new();
                            var anim = (Storyboard)Resources["GlowWaveAnimation"];
                            anim.Begin();
                            string assets = tbxAssets.Text;
                            string output = tbxOutput.Text;
                            int estimatedTotal = 0;
                            int extracted = 0;
                            prgExSep.Visibility = Visibility.Collapsed;
                            var prgProgTime = new Progress<string>(time =>
                            {
                                prgTime.Text = $"{time}";
                            });
                            var prgExtr = new Progress<double>(value =>
                            {
                                prgExPrg.Text = $"{value:0.00}";
                                prgBarExt.Value = value;
                            });
                            var mem = new Progress<long>(pMem =>
                            {
                                statMem.Text = $"{pMem / (1024.0 * 1024.0 * 1024.0):F2}";
                            });
                            var totalFile = new Progress<string>(fOut =>
                            {
                                if (int.Parse(fOut) > 0)
                                    prgExSep.Visibility = Visibility.Visible;
                                estimatedTotal = int.Parse(fOut);
                                prgExTotalFiles.Text = $"{estimatedTotal:N0}";
                            });
                            var extractedFiles = new Progress<string>(fExt =>
                            {
                                extracted = int.Parse(fExt);
                                prgExFiles.Text = $"{extracted:N0}";
                            });
                            var arcsFinished = new Progress<int>(fArcs =>
                            {
                                arcsComplete.Text = $"{fArcs}";
                            });
                            prgExPrg.Visibility = Visibility.Visible;
                            prgTime.Visibility = Visibility.Visible;
                            bool? extractionSuccess = false;
                            try
                            {
                                extractionSuccess = await Task.Run(async () => { return await _extractor.ExtractFilesAsync(prgExtr, prgProgTime, mem, totalFile, extractedFiles, arcsFinished, assets, output, _ptsAssets, _betaAssets, sw, true); });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Unhandled Exception in BtnExtract.Click: {ex.Message}\n{ex.StackTrace}");
                                MessageBox.Show($"Unhandled Exception in BtnExtract.Click: {ex.Message}\n{ex.StackTrace}", "", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            finally
                            {
                                if (BetaAssets)
                                {
                                    anim.Stop();
                                    sw.Stop();
                                    if (extractionSuccess.Value == true)
                                    {
                                        MessageBox.Show($"Extraction complete!\nYour files are located at:\n{p.Combine(outPath, "resources")}\nTime Taken: {sw.Elapsed.Hours} Hours, {sw.Elapsed.Minutes} Minutes, {sw.Elapsed.Seconds} Seconds", "Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        // Not sure how to handle failed beta extraction...
                                    }
                                    sw.Reset();
                                    btn.IsEnabled = true;
                                    btn.Content = "Extract";
                                    BtnCheckHash.IsEnabled = true;
                                    BtnDLHash.IsEnabled = true;
                                    tbxAssets.IsEnabled = true;
                                    tbxOutput.IsEnabled = true;
                                    rbLive.IsEnabled = true;
                                    rbPTS.IsEnabled = true;
                                    rbBeta.IsEnabled = true;
                                    BtnOpenFolderAssets.IsEnabled = true;
                                    BtnOpenFolderOutput.IsEnabled = true;
                                }
                                else if (PTSAssets)
                                {
                                    anim.Stop();
                                    sw.Stop();
                                    if (extractionSuccess.Value == true)
                                    {
                                        MessageBox.Show($"Extraction complete!\nYour files are located at:\n{p.Combine(outPath, "pts\\resources")}\nTime Taken: {sw.Elapsed.Hours} Hours, {sw.Elapsed.Minutes} Minutes, {sw.Elapsed.Seconds} Seconds", "Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Error during extraction.\nMake sure you have files with \"_test_\" in the folder:\n {assets} then try again!");
                                    }
                                    sw.Reset();
                                    //_timer.Reset();
                                    btn.IsEnabled = true;
                                    btn.Content = "Extract";
                                    BtnCheckHash.IsEnabled = true;
                                    BtnDLHash.IsEnabled = true;
                                    tbxAssets.IsEnabled = true;
                                    tbxOutput.IsEnabled = true;
                                    rbLive.IsEnabled = true;
                                    rbPTS.IsEnabled = true;
                                    rbBeta.IsEnabled = true;
                                    BtnOpenFolderAssets.IsEnabled = true;
                                    BtnOpenFolderOutput.IsEnabled = true;
                                }
                                else
                                {
                                    anim.Stop();
                                    sw.Stop();
                                    if (extractionSuccess.Value == true)
                                    {
                                        MessageBox.Show($"Extraction complete!\nYour files are located at:\n{p.Combine(outPath, "resources")}\nTime Taken: {sw.Elapsed.Hours:00} Hours, {sw.Elapsed.Minutes:00} Minutes, {sw.Elapsed.Seconds:00} Seconds", "Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Error during extraction.\nMake sure you have files with \"_main_\" in the folder:\n {assets} then try again!");
                                    }
                                    sw.Reset();
                                    btn.IsEnabled = true;
                                    btn.Content = "Extract";
                                    BtnCheckHash.IsEnabled = true;
                                    BtnDLHash.IsEnabled = true;
                                    tbxAssets.IsEnabled = true;
                                    tbxOutput.IsEnabled = true;
                                    rbLive.IsEnabled = true;
                                    rbPTS.IsEnabled = true;
                                    rbBeta.IsEnabled = true;
                                    BtnOpenFolderAssets.IsEnabled = true;
                                    BtnOpenFolderOutput.IsEnabled = true;
                                }
                            }
                            break;
                        }
                    #endregion
                    #region Jedipedia Link
                    case "Jedipedia":
                        var jedipediaURL = "https://swtor.jedipedia.net";
                        var jpInfo = new ProcessStartInfo(jedipediaURL)
                        {
                            UseShellExecute = true,
                        };
                        Process.Start(jpInfo);
                        e.Handled = true;
                        break;
                    #endregion
                    #region Blender Link
                    case "Blender":
                        var blenderURL = "https://blender.org";
                        var blenderInfo = new ProcessStartInfo(blenderURL)
                        {
                            UseShellExecute = true,
                        };
                        Process.Start(blenderInfo);
                        e.Handled = true;
                        break;
                    #endregion
                    #region Discord Link
                    case "Discord":
                        var discordURL = "https://discord.gg/XfHFjSN";
                        var discordInfo = new ProcessStartInfo(discordURL)
                        {
                            UseShellExecute = true,
                        };
                        Process.Start(discordInfo);
                        e.Handled = true;
                        break;
                    #endregion
                    #region Github Link
                    case "Github":
                        var githubURL = "https://github.com/SWTOR-Slicers";
                        var githubInfo = new ProcessStartInfo(githubURL)
                        {
                            UseShellExecute = true,
                        };
                        Process.Start(githubInfo);
                        e.Handled = true;
                        break;
                    #endregion
                    #region TORCommunity Link
                    case "TORCom":
                        var torcomURL = "https://torcommunity.com";
                        var torcomInfo = new ProcessStartInfo(torcomURL)
                        {
                            UseShellExecute = true,
                        };
                        Process.Start(torcomInfo);
                        e.Handled = true;
                        break;
                        #endregion
                }
            }
        }

        private Stopwatch sw;

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void tbxAssetsTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(assets.Text))
            {
                _config["Paths:Assets"] = assets.Text;
                var json = File.ReadAllText("appSettings.json");
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                jsonObj["Paths"]["Assets"] = assets.Text;
                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText("appSettings.json", output);
            }
        }

        private void tbxOutputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(output.Text))
            {
                _config["Paths:Output"] = output.Text;
                var json = File.ReadAllText("appSettings.json");
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                jsonObj["Paths"]["Output"] = output.Text;
                string outText = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText("appSettings.json", outText);
            }
        }
    }
}