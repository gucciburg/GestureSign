﻿using GestureSign.Common.Applications;
using GestureSign.Common.Configuration;
using GestureSign.Common.Gestures;
using GestureSign.Common.InterProcessCommunication;
using GestureSign.Common.Log;
using GestureSign.ControlPanel.Localization;
using ManagedWinapi.Windows;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Windows.Management.Deployment;

namespace GestureSign.ControlPanel
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        Mutex mutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                Logging.OpenLogFile();
                LoadLanguageData();

                if (AppConfig.UiAccess && Environment.OSVersion.Version.Major == 10)
                {
                    using (var currentUser = WindowsIdentity.GetCurrent())
                    {
                        if (currentUser.User != null)
                        {
                            var sid = currentUser.User.ToString();
                            PackageManager packageManager = new PackageManager();
                            var storeVersion = packageManager.FindPackagesForUserWithPackageTypes(sid, "41908Transpy.GestureSign", "CN=AF41F066-0041-4D13-9D95-9DAB66112B0A", PackageTypes.Main).FirstOrDefault();
                            if (storeVersion != null)
                            {
                                using (Process explorer = new Process
                                {
                                    StartInfo =
                                    {
                                        FileName = "explorer.exe", Arguments = @"shell:AppsFolder\" + "41908Transpy.GestureSign_f441wk0cxr8zc!GestureSign"
                                    }
                                })
                                {
                                    explorer.Start();
                                }
                                Current.Shutdown();
                                return;
                            }
                        }
                    }
                }

                bool createdNew;
                mutex = new Mutex(true, "GestureSignControlPanel", out createdNew);
                if (createdNew)
                {
                    GestureManager.Instance.Load(null);
                    GestureSign.Common.Plugins.PluginManager.Instance.Load(null);
                    ApplicationManager.Instance.Load(null);

                    NamedPipe.Instance.RunNamedPipeServer("GestureSignControlPanel", new MessageProcessor());

                    ApplicationManager.ApplicationSaved += (o, ea) => NamedPipe.SendMessageAsync("LoadApplications", "GestureSignDaemon");
                    GestureManager.GestureSaved += (o, ea) => NamedPipe.SendMessageAsync("LoadGestures", "GestureSignDaemon");

                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                else
                {
                    ShowControlPanel();
                    // use Dispatcher to resolve exception 0xc0020001
                    Current.Dispatcher.InvokeAsync(() => Current.Shutdown(), DispatcherPriority.ApplicationIdle);
                }

            }
            catch (Exception exception)
            {
                Logging.LogException(exception);
                MessageBox.Show(exception.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                Current.Shutdown();
            }

        }

        private void LoadLanguageData()
        {
            if ("Built-in".Equals(AppConfig.CultureName) || !LocalizationProviderEx.Instance.LoadFromFile("ControlPanel", ControlPanel.Properties.Resources.en))
            {
                LocalizationProviderEx.Instance.LoadFromResource(ControlPanel.Properties.Resources.en);
            }

            Current.Resources["DefaultFlowDirection"] = LocalizationProviderEx.Instance.FlowDirection;
            var font = LocalizationProviderEx.Instance.Font;
            var headerFontFamily = LocalizationProviderEx.Instance.HeaderFontFamily;
            if (font != null)
                Current.Resources["DefaultFont"] =
                    Current.Resources["ContentFontFamily"] =
                    Current.Resources["ToggleSwitchFontFamily"] =
                    Current.Resources["ToggleSwitchHeaderFontFamily"] =
                    Current.Resources["ToggleSwitchFontFamily.Win10"] =
                    Current.Resources["ToggleSwitchHeaderFontFamily.Win10"] = font;
            if (headerFontFamily != null)
                Current.Resources["HeaderFontFamily"] = headerFontFamily;
        }

        private bool ShowControlPanel()
        {
            Process current = Process.GetCurrentProcess();
            var controlPanelProcesses = Process.GetProcessesByName(current.ProcessName);

            if (controlPanelProcesses.Length > 1)
            {
                foreach (Process process in controlPanelProcesses)
                {
                    if (process.Id != current.Id)
                    {
                        var window = new SystemWindow(process.MainWindowHandle);

                        if (window.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                        {
                            ShowWindowAsync(process.MainWindowHandle, SW_RESTORE);
                        }
                        SystemWindow.ForegroundWindow = window;
                        break;
                    }
                }
                return true;
            }
            return false;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (mutex != null)
            {
                NamedPipe.Instance.Dispose();
                mutex.Dispose();
            }
        }
    }
}
