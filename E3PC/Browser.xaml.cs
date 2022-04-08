using CefSharp;
using CefSharp.Wpf;
using System.Windows;

namespace E3PC
{
    /// <summary>
    /// Interaction logic for Browser.xaml
    /// </summary>
    public partial class Browser : Window
    {
        public Browser(string url = "")
        {
            // done once per process
            //CefSettings settings = new CefSettings();
            //settings.CefCommandLineArgs["disable-features"] += ",SameSiteByDefaultCookies,CookiesWithoutSameSiteMustBeSecure";
            //Cef.Initialize(settings);

            InitializeComponent();

            WebBrowser.Address = url;

            // disable context menu
            WebBrowser.MenuHandler = new CustomContextHandler();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void MnuSettings_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MnuReload_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.Reload(true);
        }

        private void MnuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

    // use to disable browser context menu
    public class CustomContextHandler : IContextMenuHandler
    {
        public void OnBeforeContextMenu(IWebBrowser browserControl, CefSharp.IBrowser browser, IFrame frame, IContextMenuParams parameters,
            IMenuModel model)
        {
            model.Clear();
        }

        public bool OnContextMenuCommand(IWebBrowser browserControl, CefSharp.IBrowser browser, IFrame frame, IContextMenuParams parameters,
            CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser browserControl, CefSharp.IBrowser browser, IFrame frame)
        {
        }

        public bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }
    }
}
