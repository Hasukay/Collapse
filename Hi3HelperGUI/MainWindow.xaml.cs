﻿using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
//using System.IO;
//using Microsoft.Win32;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            Logger.DisableConsole = true;
        }
        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
            Logger.DisableConsole = false;
        }

        public MainWindow()
        {
            LoadAppConfig();
            ApplyAppConfig();
            InitializeConsole();
            InitializeComponent();
            //CheckVersionAvailability();
            Title = "Hi3HelperGUI InDev v" + GetRunningVersion().ToString();
            CheckConfigSettings();
        }

        private Version GetRunningVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        internal void DisableAllFunction() =>
            Dispatcher.Invoke(() =>
            {
                UpdateSection.IsEnabled = false;
                BlockSection.IsEnabled = false;
                CutsceneSection.IsEnabled = false;
                SettingsSection.IsEnabled = false;
                MirrorSelector.IsEnabled = false;
            });

        static void InitializeConsole()
        {
            //AllocConsole();

            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                LogWriteLine("failed to get output console mode", LogType.Error);
                Console.ReadKey();
                return;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                LogWriteLine($"failed to set output console mode, error code: {GetLastError()}", LogType.Error);
                Console.ReadKey();
                return;
            }

            InitLog();
        }

        private void EnableConsole(object sender, RoutedEventArgs e) => ShowConsoleWindow();
        private void DisableConsole(object sender, RoutedEventArgs e) => HideConsoleWindow();

        private void ApplySettings(object sender, RoutedEventArgs e)
        {
            SaveAppConfig();
            ApplyAppConfig();
        }
    }
}
