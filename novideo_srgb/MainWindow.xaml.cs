﻿using Microsoft.Win32;
using novideo_srgb.PowerBroadcast;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace novideo_srgb
{
    public partial class MainWindow
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;
            SystemEvents.DisplaySettingsChanged += _viewModel.OnDisplaySettingsChanged;
            
            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0);
            
            if (args.Contains("-minimize"))
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
            
            InitializeTrayIcon();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs o)
        {
            var window = new AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.Cast<Window>().Any(x => x is AdvancedWindow)) return;
            var monitor = ((FrameworkElement)sender).DataContext as MonitorData;
            var window = new AdvancedWindow(monitor)
            {
                Owner = this
            };

            void CloseWindow(object o, EventArgs e2) => window.Close();

            SystemEvents.DisplaySettingsChanged += CloseWindow;
            if (window.ShowDialog() == false) return;
            SystemEvents.DisplaySettingsChanged -= CloseWindow;

            if (window.ChangedCalibration)
            {
                _viewModel.SaveConfig();
                monitor?.ReapplyClamp();
            }

            if (window.ChangedDither)
            {
                monitor?.ApplyDither(window.DitherState.SelectedIndex, Math.Max(window.DitherBits.SelectedIndex, 0),
                    Math.Max(window.DitherMode.SelectedIndex, 0));
            }
        }

        private void ReapplyButton_Click(object sender, RoutedEventArgs e)
        {
            ReapplyMonitorSettings();
        }

        private async Task DeferAction(Action action, int seconds)
        {
            await Task.Delay(seconds);
            action();
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            PowerBroadcastNotificationHelpers.HandleBroadcastNotification(msg, lParam, (message) =>
            {
                if (((char)message.Data) == (char)ConsoleDisplayState.TurnedOn)
                {
                    _ = DeferAction(ReapplyMonitorSettings, 150);
                }
            });

        private void InitializeTrayIcon()
        {
            var notifyIcon = new NotifyIcon
            {
                Text = "Novideo sRGB",
                Icon = Properties.Resources.icon,
                Visible = true
            };

            notifyIcon.MouseDoubleClick +=
                delegate
                {
                    Show();
                    WindowState = WindowState.Normal;
                };

            var contextMenu = new ContextMenu();

            var reapplyItem = new MenuItem();
            contextMenu.MenuItems.Add(reapplyItem);
            reapplyItem.Text = "Reapply";
            reapplyItem.Click += delegate { ReapplyMonitorSettings(); };

            contextMenu.MenuItems.Add("-");

            var exitItem = new MenuItem();
            contextMenu.MenuItems.Add(exitItem);
            exitItem.Text = "Exit";
            exitItem.Click += delegate { Close(); };

            notifyIcon.ContextMenu = contextMenu;

            Closed += delegate { notifyIcon.Dispose(); };
        }

        /*
        protected override void OnSourceInitialized(EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged += new EventHandler(OnDisplaySettingsChanged);
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            PowerBroadcastNotificationHelpers.RegisterPowerBroadcastNotification(handle, PowerSettingGuids.CONSOLE_DISPLAY_STATE);
            HwndSource.FromHwnd(handle)?.AddHook(HandleMessages);
        }
        */

        private void ReapplyMonitorSettings()
        {
            foreach (var monitor in _viewModel.Monitors)
            {
                monitor.ReapplyClamp();
            }
        }
    }
}