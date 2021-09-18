﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.IO;
using Newtonsoft.Json;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public static AppSettings AppConfigData = new AppSettings();
        public void LoadAppConfig() => AppConfigData = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(@"appconfig.json"));

        public async void ApplyAppConfig() => await Task.Run(() =>
        {
            LoadAppConfig();
            Dispatcher.Invoke(() =>
            {
                ConfigEnableConsole.IsChecked = AppConfigData.ShowConsole;
            });

            if (AppConfigData.ShowConsole)
                ShowConsoleWindow();
            else
                HideConsoleWindow();
        });

        public void SaveAppConfig()
        {
            Dispatcher.Invoke(() =>
            {
                AppConfigData.ShowConsole = ConfigEnableConsole.IsChecked ?? false;
            });
            File.WriteAllText(@"appconfig.json", JsonConvert.SerializeObject(AppConfigData, Formatting.Indented).ToString());
        }
    }
}
