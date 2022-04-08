using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace E3PC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        LocalHttpServer _myServer = null;

        public MainWindow()
        {
            InitializeCef();

            InitializeComponent();

            InitializeConfig();

            InitInstalledVersions(App.Config.SelectedE3Version);

            this.DataContext = App.Config;
        }

        private void InitializeCef()
        {
            // done once per process
            CefSettings settings = new CefSettings();
            settings.CefCommandLineArgs["disable-features"] += ",SameSiteByDefaultCookies,CookiesWithoutSameSiteMustBeSecure";
            Cef.Initialize(settings);
        }

        private void InitializeConfig()
        {
            try
            {
                E3Config cfg = JsonConvert.DeserializeObject(Properties.Settings.Default.Config, typeof(E3Config)) as E3Config;
                App.Config.Update(cfg);
            }
            catch (Exception ex) { }
        }

        private string ReadE3VersionFromDir(string s)
        {
            string e3_ver = string.Empty;
            string cfg_file = Path.Combine(s, "config/config.json");
            if (File.Exists(cfg_file))
            {
                using (var sr = new StreamReader(cfg_file))
                {
                    var reader = new JsonTextReader(sr);
                    var jObject = JObject.Load(reader);
                    //Get property from JObject
                    e3_ver = jObject.GetValue("version").Value<string>();
                }
            }
            return e3_ver;
        }

        private void InitInstalledVersions(string selected_e3_ver = "")
        {
            App.Config.InstalledVersions.Clear();
            string[] dirs = Directory.GetDirectories(App.Config.LocalFolder, "e3*");
            foreach (string s in dirs)
            {
                string cfg_file = Path.Combine(s, "config/config.json");
                string e3_ver = ReadE3VersionFromDir(s);
                if (!string.IsNullOrEmpty(e3_ver))
                {
                    App.Config.InstalledVersions.Add(new E3InstalledVersion() { Version = e3_ver, LocalFilePath = s });
                }
            }

            CboInstalledVersion.Items.Refresh();
            if (App.Config.InstalledVersions.Count > 0)
            {
                E3InstalledVersion v = null;
                if (!string.IsNullOrEmpty(selected_e3_ver))
                {
                    v = App.Config.InstalledVersions.Find(x => x.Version == selected_e3_ver);
                }
                if (v == null) v = App.Config.InstalledVersions[0];
                CboInstalledVersion.SelectedItem = v;
            }
        }

        private void BtnStartE3_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(App.Config.ClientId))
            {
                SetStatusText("Error: Please set a client Id");
                return;
            }

            if (null == _myServer)
            {
                _myServer = new LocalHttpServer(App.Config.LocalFolder, 8051);
                _myServer.OnError += _myServer_OnError;
            }
            else if (App.Config.LocalFolder != _myServer.RootDirectory)
            {
                _myServer.Stop();
                _myServer.RootDirectory = App.Config.LocalFolder;
                _myServer.Start();
            }

            string e3_path = "e3";
            string e3_installed_path = App.Config.GetInstalledE3Path();
            if (!string.IsNullOrEmpty(e3_installed_path))
            {
                e3_path = e3_installed_path;
            }
            string url = "http://127.0.0.1:8051/" + e3_path + "/index_pcc.html?demoId=" + App.Config.ClientId;

            if (!string.IsNullOrEmpty(App.Config.LauncherUrl))
            {
                url += "&launcherUrl=" + App.Config.LauncherUrl;
            }

            if (!string.IsNullOrEmpty(App.Config.BackendUrl))
            {
                url += "&be=" + App.Config.BackendUrl;
                if (!App.Config.BackendUrls.Contains(App.Config.BackendUrl))
                {
                    App.Config.BackendUrls.Add(App.Config.BackendUrl);
                }
            }

            if (!App.Config.ClientIds.Contains(App.Config.ClientId))
            {
                App.Config.ClientIds.Add(App.Config.ClientId);
            }

            //if (!string.IsNullOrEmpty(App.Config.BrowserExe) && File.Exists(App.Config.BrowserExe))
            //{
            //    if (!App.Config.BrowserExes.Contains(App.Config.BrowserExe))
            //    {
            //        App.Config.BrowserExes.Add(App.Config.BrowserExe);
            //    }
            //}
            //else
            //{
            //    SetStatusText("Error: Invalid Browser Exe Path - " + App.Config.BrowserExe);
            //    return;
            //}

            Browser browser = new Browser(url);
            browser.Show();

            // Process.Start(App.Config.BrowserExe, App.Config.BrowserArgs + " " + url);
        }

        private void _myServer_OnError(object sender, string e)
        {
            MessageBox.Show(e, "Local Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Config = JsonConvert.SerializeObject(App.Config);
            Properties.Settings.Default.Save();

            if (_myServer != null)
            {
                _myServer.Stop();
            }
        }

        private void BtnInstallE3_Click(object sender, RoutedEventArgs e)
        {
            string e3a_zip = Path.Combine(App.Config.LocalFolder, "e3a.zip");
            if (File.Exists(e3a_zip)) File.Delete(e3a_zip);
            BtnInstallE3.IsEnabled = false;
            // TODO: verify version first
            using (WebClient wc = new WebClient())
            {
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                wc.DownloadFileAsync(new Uri(App.Config.LauncherUrl + "/e3a.zip"), e3a_zip);
            }
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                SetStatusText("Installing...");
                string e3a_zip = Path.Combine(App.Config.LocalFolder, "e3a.zip");
                string e3a_unzip = Path.Combine(App.Config.LocalFolder, "e3a_unzip");
                string e3_ver = string.Empty;
                if (File.Exists(e3a_zip))
                {
                    if (Directory.Exists(e3a_unzip))
                    {
                        Directory.Delete(e3a_unzip, true);
                    }

                    ProcessStartInfo psi = new ProcessStartInfo(App.Config.ZipToolExe, " x -o\"" + e3a_unzip + "\" \"" + e3a_zip + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process p = Process.Start(psi);
                    p.WaitForExit();

                    if (!App.Config.LauncherUrls.Contains(App.Config.LauncherUrl))
                    {
                        App.Config.LauncherUrls.Add(App.Config.LauncherUrl);
                    }

                    e3_ver = ReadE3VersionFromDir(e3a_unzip);
                    string e3_installed_path = Path.Combine(App.Config.LocalFolder, "e3_v" + e3_ver);
                    E3InstalledVersion has_same_ver = App.Config.InstalledVersions.Find(x => x.Version == e3_ver);
                    if (has_same_ver != null)
                    {
                        if (MessageBox.Show("E3 v" + has_same_ver.Version + " is already installed.\nUpgrade?", "Upgrade E3", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            e3_installed_path = has_same_ver.LocalFilePath;
                            Directory.Delete(has_same_ver.LocalFilePath, true);
                        }
                        else
                        {
                            Directory.Delete(e3a_unzip, true);
                            File.Delete(e3a_zip);
                            BtnInstallE3.IsEnabled = true;
                            SetStatusText("Install E3 cancelled.");
                            return;
                        }
                    }
                    Directory.Move(e3a_unzip, e3_installed_path);
                    File.Delete(e3a_zip);
                }
                BtnInstallE3.IsEnabled = true;
                InitInstalledVersions(e3_ver);
                SetStatusText("E3 v" + e3_ver + " installed successfully.");
            }
            catch (Exception ex)
            {
                SetStatusText("Install E3 Error: " + ex.Message);
                BtnInstallE3.IsEnabled = true;
            }
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            SetStatusText("Downloading... " + e.ProgressPercentage + "%");
        }

        private void BtnUninstallE3_Click(object sender, RoutedEventArgs e)
        {
            E3InstalledVersion v = CboInstalledVersion.SelectedItem as E3InstalledVersion;
            if (null != v)
            {
                if (MessageBox.Show("Uninstall E3 v" + v.Version + "?", "Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Directory.Delete(v.LocalFilePath, true);
                    InitInstalledVersions();
                }
            }
        }

        System.Windows.Media.SolidColorBrush _errorColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        private void SetStatusText(string txt, bool error = false)
        {
            BtnInstallE3.Dispatcher.BeginInvoke((Action)(() => {
                TxtStatus.Text = txt;
                if (error)
                {
                    TxtStatus.Foreground = _errorColor;
                }
                else
                {
                    TxtStatus.Foreground = this.Foreground;
                }
            }));
        }

        private void BtnVerifyBE_Click(object sender, RoutedEventArgs e)
        {
            SetStatusText("Verifying Backend...");
            if (string.IsNullOrEmpty(App.Config.BackendUrl))
            {
                SetStatusText("Error: Backend Url is empty!");
                return;
            }
            BtnVerifyBE.IsEnabled = false;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadStringCompleted += Wc_BtnVerifyBE_Completed;
                    wc.DownloadStringAsync(new Uri(App.Config.BackendUrl + "/ws/"));
                }
            }
            catch (Exception ex)
            {
                SetStatusText("Error verifying Backend: " + ex.Message);
                BtnVerifyBE.IsEnabled = true;
            }
        }

        private void Wc_BtnVerifyBE_Completed(object sender, DownloadStringCompletedEventArgs e)
        {
            if (null != e.Error)
            {
                SetStatusText("Error verifying Backend: " + e.Error.Message);
            }
            else
            {
                SetStatusText("Backend is valid!");
            }
            BtnVerifyBE.IsEnabled = true;
        }

        private async void BtnVerifySTB_Click(object sender, RoutedEventArgs e)
        {
            SetStatusText("Verifying Backend...");
            string be = App.Config.BackendUrl;
            if (string.IsNullOrEmpty(be))
            {
                be = "https://vpn-portal.allentek.net";
            }
            if (string.IsNullOrEmpty(App.Config.ClientId))
            {
                SetStatusText("Error: ClientId is empty!");
                return;
            }

            BtnVerifySTB.IsEnabled = false;
            StbCheckInResponse ret = await StbCheckInAsync(be);
            string txt = "";
            if (null != ret)
            {
                if (string.IsNullOrEmpty(ret.error) && null != ret.stb)
                {
                    txt = "OK: [" + App.Config.ClientId + "] is registered on server [" + be + "]\n" + ret.stb.ToString();
                }
                else
                {
                    txt = ret.error;
                }
            }
            else
            {
                txt = "Internal Error";
            }

            SetStatusText(txt);
            BtnVerifySTB.IsEnabled = true;
        }

        public Task<StbCheckInResponse> StbCheckInAsync(string be)
        {
            var tcs = new TaskCompletionSource<StbCheckInResponse>();
            StbCheckInResponse stbr = new StbCheckInResponse();

            try
            {
                RestRequest request = new RestRequest(Method.POST);
                request.Resource = "/ws/json/stbClient/checkIn";
                request.AddParameter("macAddress", App.Config.ClientId);
                request.AddParameter("stbType", "SmartTV");
                if (!string.IsNullOrEmpty(App.Config.SelectedE3Version))
                {
                    request.AddParameter("clientVersion", App.Config.SelectedE3Version);
                }

                string username = "vigo_client";
                string password = "7pLuPnrx_hi;af" + "---" + App.Config.ClientId.ToLower();

                RestClient rc = new RestClient(be);
                rc.Authenticator = new HttpBasicAuthenticator(username, password);
                rc.ExecuteAsync(request, (r) => {
                    if (r.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        stbr.error = "Error: " + r.StatusCode;
                    }
                    else
                    {
                        stbr.stb = (Stb)(JsonConvert.DeserializeObject(r.Content, typeof(Stb)));
                    }
                    tcs.TrySetResult(stbr);
                });
            }
            catch (Exception ex)
            {
                stbr.error = "Exception: " + ex.Message;
                tcs.TrySetResult(stbr);
            }

            return tcs.Task;
        }

        private void CboInstalledVersion_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //E3InstalledVersion v = (CboInstalledVersion.SelectedItem as E3InstalledVersion);
            //if (null != v)
            //{
            //    CboLauncherUrl.Text = v.InstalledPath;
            //}
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            string updateUrl = App.Config.UpdateUrl;
            if (string.IsNullOrEmpty(updateUrl))
            {
                SetStatusText("Error: Please enter Software Update URL", true);
                CboUpdateUrl.Focus();
                return;
            }

            string zipToolCmd = App.Config.ZipToolExe;
            if (!File.Exists(zipToolCmd))
            {
                SetStatusText("Error: Please enter correct Zip Tool exe location!");
                CboZipToolExe.Focus();
                return;
            }

            App.CheckForSoftwareUpdateX(updateUrl, zipToolCmd);
        }

        private void BtnBrowseLocalFolder_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO: Browse for Local Folder Path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnBrowseZipToolExe_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO: Browse for Zipp Tool EXE Path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(a.Location);
            Title += " v." + fvi.FileVersion;

            string s = App.CheckForSoftwareUpdate(this, false);
            SetStatusText(s);
        }
    }

    public class StbCheckInResponse
    {
        public Stb stb { get; set; }
        public string error { get; set; }
    }

    public class Stb
    {
        public string room { get; set; }
        public string bed { get; set; }
        public string hospital { get; set; }

        public override string ToString()
        {
            return room + "/" + bed + "/" + hospital;
        }
    }

    public class E3InstalledVersion
    {
        public string Version { get; set; }
        public string LocalFilePath { get; set; }
        public string LauncherUrl { get; set; }

        public string InstalledPath
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(this.LocalFilePath);
                return di.Name;
            }
        }

        public override string ToString()
        {
            return this.Version; // + " [" + this.LocalFilePath + "]";
        }
    }

    public class E3Config
    {
        public string LauncherUrl { get; set; }
        public List<string> LauncherUrls { get; }

        public string ClientId { get; set; }
        public List<string> ClientIds { get; }

        public string UpdateUrl { get; set; }
        public string LocalFolder { get; set; }
        public string ZipToolExe { get; set; }
        public string SelectedE3Version { get; set; }
        public List<E3InstalledVersion> InstalledVersions { get; }

        public string BackendUrl { get; set; }
        public List<string> BackendUrls { get; }

        public E3Config()
        {
            this.LauncherUrl = "http://nextgen.corp.allentek.com/e3";
            this.LauncherUrls = new List<string>();

            this.ClientId = "e3tv:demo";
            this.ClientIds = new List<string>();

            this.UpdateUrl = "http://nextgen.corp.allentek.com/e3tools";

            this.LocalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "E3PC"); // AppDomain.CurrentDomain.BaseDirectory; // @"c:\www";

            this.ZipToolExe = @"c:\tools\7-zip\7z.exe";

            this.InstalledVersions = new List<E3InstalledVersion>();
            this.BackendUrls = new List<string>();

            if (!Directory.Exists(this.LocalFolder))
            {
                Directory.CreateDirectory(this.LocalFolder);
            }
        }

        public E3InstalledVersion GetSelectedE3Version()
        {
            return this.GetInstalledE3Version(this.SelectedE3Version);
        }

        public E3InstalledVersion GetInstalledE3Version(string e3_ver)
        {
            if (string.IsNullOrEmpty(e3_ver)) return null;
            return this.InstalledVersions.Find(x => x.Version == e3_ver);
        }

        public string GetInstalledE3Path(string e3_ver = "")
        {
            string ret = string.Empty;
            if (string.IsNullOrEmpty(e3_ver)) e3_ver = this.SelectedE3Version;
            E3InstalledVersion e3 = this.GetInstalledE3Version(e3_ver);
            if (null != e3)
            {
                ret = e3.InstalledPath;
                // ret = e3.LocalFilePath.Replace(this.LocalFolder, "");
                // ret = ret.Replace("\\", "");
            }
            return ret;
        }

        public void Update(E3Config cfg)
        {
            if (null == cfg) return;

            if (!string.IsNullOrEmpty(cfg.LauncherUrl)) this.LauncherUrl = cfg.LauncherUrl;
            if (null != cfg.LauncherUrls)
            {
                this.LauncherUrls.Clear();
                this.LauncherUrls.AddRange(cfg.LauncherUrls);
            }

            if (!string.IsNullOrEmpty(cfg.ClientId)) this.ClientId = cfg.ClientId;
            if (null != cfg.ClientIds)
            {
                this.ClientIds.Clear();
                this.ClientIds.AddRange(cfg.ClientIds);
            }

            if (!string.IsNullOrEmpty(cfg.UpdateUrl)) this.UpdateUrl = cfg.UpdateUrl;

            if (!string.IsNullOrEmpty(cfg.LocalFolder)) this.LocalFolder = cfg.LocalFolder;

            if (!string.IsNullOrEmpty(cfg.ZipToolExe)) this.ZipToolExe = cfg.ZipToolExe;

            if (!string.IsNullOrEmpty(cfg.SelectedE3Version)) this.SelectedE3Version = cfg.SelectedE3Version;
            if (null != cfg.InstalledVersions)
            {
                this.InstalledVersions.Clear();
                this.InstalledVersions.AddRange(cfg.InstalledVersions);
            }

            if (!string.IsNullOrEmpty(cfg.BackendUrl)) this.BackendUrl = cfg.BackendUrl;
            if (null != cfg.BackendUrls)
            {
                this.BackendUrls.Clear();
                this.BackendUrls.AddRange(cfg.BackendUrls);
            }
        }
    }
}
