using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Runtime.InteropServices;

namespace E3PC
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static E3Config Config = new E3Config();

        App()
        {
            if (E3PC.Properties.Settings.Default.UpgradeRequired)
            {
                E3PC.Properties.Settings.Default.Upgrade();
                E3PC.Properties.Settings.Default.UpgradeRequired = false;
                E3PC.Properties.Settings.Default.Save();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (SingleInstance.AlreadyRunning())
            {
                Helper.ShowErrorMessage("Only one instance of this application is allowed.", "Application Starup Warning");
                App.Current.Shutdown(); // Just shutdown the current application,if any instance found.  
            }

            base.OnStartup(e);
        }

        public static string CheckForSoftwareUpdate(Window o = null, bool updatePrompt = true)
        {
            if (!string.IsNullOrEmpty(App.Config.UpdateUrl) && !string.IsNullOrEmpty(App.Config.ZipToolExe))
            {
                return CheckForSoftwareUpdateX(App.Config.UpdateUrl, App.Config.ZipToolExe, updatePrompt);
            }
            return string.Empty;
        }

        public static void DownloadAndUpdateSoftware(WebClient webClient, string zipFileName, string updateUrl, string zipToolCmd)
        {
            string zipFileFromServer = updateUrl + zipFileName;
            string sourcePath = System.AppDomain.CurrentDomain.BaseDirectory;
            string updatedZipFile = Path.Combine(sourcePath, zipFileName);

            webClient.DownloadFile(zipFileFromServer, updatedZipFile);

            if (File.Exists(updatedZipFile))
            {
                Application.Current.Shutdown();
                Process.Start(Path.Combine(sourcePath, "tools", "update.bat"), zipFileName + " " + zipToolCmd);
            }
            else
            {
                Helper.ShowErrorMessage("Error - Failed to download file " + zipFileName + "\nFrom" + zipFileFromServer, "Software Update Check Error");
            }
        }

        public static string CheckForSoftwareUpdateX(string updateUrl, string zipToolCmd, bool updatePrompt = true)
        {
            string msg = string.Empty;
            try
            {
                if (!updateUrl.EndsWith("/")) updateUrl += "/";

                using (var webClient = new System.Net.WebClient())
                {
                    var jsonString = webClient.DownloadString(updateUrl + "e3pc.json");
                    JObject updateVersion = JObject.Parse(jsonString);
                    if (null == updateVersion || null == updateVersion["version"] || null == updateVersion["zip"])
                    {
                        if (updatePrompt)
                        {
                            msg = "Error - Failed to pull update info.\nCheck settings for Software Update URL:\n" + updateUrl;
                            Helper.ShowErrorMessage(msg, "Software Update Check Error");
                        }
                        return msg;
                    }

                    string version = updateVersion["version"].ToString();
                    Assembly a = Assembly.GetExecutingAssembly();
                    string myVersion = FileVersionInfo.GetVersionInfo(a.Location).FileVersion;
                    Version newVersion = new Version(version);
                    Version currVersion = new Version(myVersion);
                    if (newVersion.CompareTo(currVersion) > 0)
                    {
                        if (updatePrompt)
                        {
                            if (MessageBox.Show("New Update is available!\nProceed?", "Software Update", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                string zipFileName = updateVersion["zip"].ToString();
                                DownloadAndUpdateSoftware(webClient, zipFileName, updateUrl, zipToolCmd);
                            }
                        }
                        else
                        {
                            msg = "New Update is available!";
                        }
                    }
                    else if (updatePrompt)
                    {
                        MessageBox.Show("Current version " + myVersion + " is the latest!", "Software Update");
                    }
                }
            }
            catch (Exception ex)
            {
                if (updatePrompt)
                {
                    msg = ex.Message + "\n\nCheck settings for Software Update URL:\n" + updateUrl;
                    Helper.ShowErrorMessage(msg, "Software Update Check Error");
                }
            }

            return msg;
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Helper.ShowErrorMessage(e.Exception, "Application Error");
            this.Shutdown(-1);
        }

    }  // class App : Application

    public class User32API
    {
        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;

    } // class User32API

    public sealed class SingleInstance
    {
        public static bool AlreadyRunning()
        {
            bool running = false;
            try
            {
                // Getting collection of process  
                Process currentProcess = Process.GetCurrentProcess();

                // Check with other process already running   
                foreach (var p in Process.GetProcesses())
                {
                    if (p.Id != currentProcess.Id) // Check running process   
                    {
                        if (p.ProcessName.Equals(currentProcess.ProcessName) == true)
                        {
                            running = true;
                            IntPtr hFound = p.MainWindowHandle;
                            if (User32API.IsIconic(hFound)) // If application is in ICONIC mode then  
                                User32API.ShowWindow(hFound, User32API.SW_RESTORE);
                            User32API.SetForegroundWindow(hFound); // Activate the window, if process is already running  
                            break;
                        }
                    }
                }
            }
            catch { }
            return running;
        }
    } // class SingleInstance

}
