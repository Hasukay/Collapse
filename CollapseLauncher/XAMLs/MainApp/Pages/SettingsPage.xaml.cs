﻿using Hi3Helper;
using Hi3Helper.Data;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static CollapseLauncher.WindowSize.WindowSize;
using static CollapseLauncher.FileDialogCOM.FileDialogNative;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Dialogs.SimpleDialogs;

namespace CollapseLauncher.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            this.InitializeComponent();
            LoadAppConfig();
            this.DataContext = this;

            string Version = $" {AppCurrentVersion.VersionString}";
#if DEBUG
            Version = Version + "d";
#endif
            if (IsPreview)
                Version = Version + " Preview";
            else
                Version = Version + " Stable";

            AppVersionTextBlock.Text = Version;
            CurrentVersion.Text = Version;

            // TODO: Eventually, we should make this a button which copies on click, but this works for now
            GitVersionIndicator.Text = $"{ThisAssembly.Git.Branch} - {ThisAssembly.Git.Commit}";

            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (IsChangeRegionWarningNeedRestart)
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

            string SwitchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, SwitchToVer);
#if !DISABLEDISCORD
            AppDiscordPresence.SetActivity(ActivityType.AppSettings);
#else
            ToggleDiscordRPC.Visibility = Visibility.Collapsed;
#endif
        }

        private async void RelocateFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialog_RelocateFolder(Content))
            {
                case ContentDialogResult.Primary:
                    IsFirstInstall = true;
                    try
                    {
                        File.Delete(AppConfigFile);
                        File.Delete(AppNotifIgnoreFile);
                        Directory.Delete(AppGameConfigMetadataFolder, true);
                    }
                    catch { }
                    MainFrameChanger.ChangeWindowFrame(typeof(StartupPage));
                    break;
                default:
                    break;
            }
        }

        private async void ClearMetadataFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialog_ClearMetadata(Content))
            {
                case ContentDialogResult.Primary:
                    try
                    {
                        Directory.Delete(AppGameConfigMetadataFolder, true);
                        var collapsePath = Process.GetCurrentProcess().MainModule.FileName;
                        Process.Start(collapsePath);
                        App.Current.Exit();
                    }
                    catch (Exception ex)
                    {
                        string msg = $"An error occurred while attempting to clear metadata folder. Exception stacktrace below:\r\n{ex}";
                        ErrorSender.SendException(ex);
                        LogWriteLine(msg, LogType.Error, true);
                    }
                    break;
                default:
                    break;
            }
        }

        private void OpenAppDataFolder(object sender, RoutedEventArgs e)
        {
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = AppGameFolder
                }
            }.Start();
        }

        private void ClearImgFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(AppGameImgFolder))
                    Directory.Delete(AppGameImgFolder, true);

                Directory.CreateDirectory(AppGameImgFolder);
                (sender as Button).IsEnabled = false;
            }
            catch (Exception ex)
            {
                string msg = $"An error occurred while attempting to clear image folder. Exception stacktrace below:\r\n{ex}";
                ErrorSender.SendException(ex);
                LogWriteLine(msg, LogType.Error, true);
            }
        }

        private void ClearLogsFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(AppGameLogsFolder))
                {
                    _log.Dispose();
                    Directory.Delete(AppGameLogsFolder, true);
                    _log.SetFolderPathAndInitialize(AppGameLogsFolder, Encoding.UTF8);
                }

                Directory.CreateDirectory(AppGameLogsFolder);
                (sender as Button).IsEnabled = false;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }
        }

        private void ForceUpdate(object sender, RoutedEventArgs e)
        {
            string ChannelName = IsPreview ? "Preview" : "Stable";
            LaunchUpdater(ChannelName);
        }

        private async void ChangeRelease(object sender, RoutedEventArgs e)
        {
            string ChannelName = IsPreview ? "Stable" : "Preview";
            switch (await Dialog_ChangeReleaseChannel(ChannelName, this))
            {
                case ContentDialogResult.Primary:
                    LaunchUpdater(ChannelName);
                    break;
            }
        }

        private void LaunchUpdater(string ChannelName)
        {
            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string UpdateArgument = $"elevateupdate --input \"{ExecutableLocation.Replace('\\', '/')}\" --channel {ChannelName}";
            Console.WriteLine(UpdateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = Path.Combine(ExecutableLocation, "CollapseLauncher.exe"),
                        Arguments = UpdateArgument,
                        Verb = "runas"
                    }
                }.Start();
                App.Current.Exit();
            }
            catch
            {
                return;
            }
        }

        private async void CheckUpdate(object sender, RoutedEventArgs e)
        {
            var isMetered = true;
            NetworkCostType currentNetCostType = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost().NetworkCostType ?? NetworkCostType.Fixed;
            if (currentNetCostType == NetworkCostType.Unrestricted || currentNetCostType == NetworkCostType.Unknown)
                isMetered = false;

            if (isMetered)
            {
                switch (await Dialog_MeteredConnectionWarning(Content))
                {
                    case ContentDialogResult.Primary:
                        UpdateLoadingStatus.Visibility = Visibility.Visible;
                        UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                        UpToDateStatus.Visibility = Visibility.Collapsed;
                        CheckUpdateBtn.IsEnabled = false;
                        ForceInvokeUpdate = true;
                        LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
                        LauncherUpdateWatcher.StartCheckUpdate(true);
                        break;

                    case ContentDialogResult.Secondary:
                        return;
                }
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Visible;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.IsEnabled = false;
                ForceInvokeUpdate = true;
                LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
                LauncherUpdateWatcher.StartCheckUpdate(true);
            }
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            string ChannelName = IsPreview ? " Preview" : " Stable";

            CheckUpdateBtn.IsEnabled = true;
            if (e.IsUpdateAvailable)
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Visible;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableLabel.Text = e.NewVersionName.VersionString + (ChannelName);
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
                return;
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Visible;
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
            }
        }

        private void ClickTextLinkFromTag(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as TextBlock).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        private void ClickButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as Button).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        private async void SelectBackgroundImg(object sender, RoutedEventArgs e)
        {
            string file = await GetFilePicker(new Dictionary<string, string> { { "Supported formats", "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.tiff;*.tif;*.webp" } });
            if (file != null)
            {
                regionBackgroundProp.imgLocalPath = file;
                SetAndSaveConfigValue("CustomBGPath", file);
                BGPathDisplay.Text = file;
                BackgroundImgChanger.ChangeBackground(file);
            }
        }

        int EggsAttempt = 1;
        private void Egg(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (EggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
        }

        private bool IsBGCustom
        {
            get
            {
                bool IsEnabled = GetAppConfigValue("UseCustomBG").ToBool();
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                if (!string.IsNullOrEmpty(BGPath))
                    BGPathDisplay.Text = BGPath;
                else
                    BGPathDisplay.Text = Lang._Misc.NotSelected;

                if (IsEnabled)
                {
                    AppBGCustomizer.Visibility = Visibility.Visible;
                    AppBGCustomizerNote.Visibility = Visibility.Visible;
                }
                else
                {
                    AppBGCustomizer.Visibility = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility = Visibility.Collapsed;
                }

                BGSelector.IsEnabled = IsEnabled;
                return IsEnabled;
            }
            set
            {
                SetAndSaveConfigValue("UseCustomBG", new IniValue(value));
                if (!value)
                {
                    BGPathDisplay.Text = Lang._Misc.NotSelected;
                    regionBackgroundProp.imgLocalPath = GetAppConfigValue("CurrentBackground").ToString();
                    m_mainPage?.ChangeBackgroundImageAsRegionAsync();
                    AppBGCustomizer.Visibility = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility = Visibility.Collapsed;
                }
                else
                {
                    string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                    if (string.IsNullOrEmpty(BGPath))
                    {
                        regionBackgroundProp.imgLocalPath = AppDefaultBG;
                    }
                    else
                    {
                        if (!File.Exists(BGPath))
                        {
                            regionBackgroundProp.imgLocalPath = AppDefaultBG;
                        }
                        else
                        {
                            regionBackgroundProp.imgLocalPath = BGPath;
                        }
                    }
                    BGPathDisplay.Text = regionBackgroundProp.imgLocalPath;
                    BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath);
                    AppBGCustomizer.Visibility = Visibility.Visible;
                    AppBGCustomizerNote.Visibility = Visibility.Visible;
                }
                BGSelector.IsEnabled = value;
            }
        }

        private bool IsConsoleEnabled
        {
            get
            {
                bool isEnabled = GetAppConfigValue("EnableConsole").ToBool();
                if (isEnabled)
                {
                    ToggleIncludeGameLogs.Visibility = Visibility.Visible;
                }
                else
                {
                    ToggleIncludeGameLogs.Visibility = Visibility.Collapsed;
                }
                return isEnabled;
            }
            set
            {
                _log.Dispose();
                if (value)
                {
                    _log = new LoggerConsole(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Visible;
                }
                else
                {
                    _log = new LoggerNull(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableConsole", value);
            }
        }

        private bool IsMultipleInstanceEnabled
        {
            get => LauncherConfig.IsMultipleInstanceEnabled;
            set => LauncherConfig.IsMultipleInstanceEnabled = value;
        }

#if !DISABLEDISCORD
        private bool IsDiscordRPCEnabled
        {
            get
            {
                bool IsEnabled = GetAppConfigValue("EnableDiscordRPC").ToBool();
                ToggleDiscordGameStatus.IsEnabled = IsEnabled;
                if (IsEnabled)
                {
                    ToggleDiscordGameStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    ToggleDiscordGameStatus.Visibility = Visibility.Collapsed;
                }
                return IsEnabled;
            }
            set
            {
                if (value)
                {
                    AppDiscordPresence.SetupPresence();
                    ToggleDiscordGameStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    AppDiscordPresence.DisablePresence();
                    ToggleDiscordGameStatus.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableDiscordRPC", value);
                ToggleDiscordGameStatus.IsEnabled = value;
            }
        }

        private bool IsDiscordGameStatusEnabled
        {
            get => GetAppConfigValue("EnableDiscordGameStatus").ToBool();
            set
            {
                SetAndSaveConfigValue("EnableDiscordGameStatus", value);
                AppDiscordPresence.SetupPresence();
            }
        }

        private bool IsAcrylicEffectEnabled
        {
            get => GetAppConfigValue("EnableAcrylicEffect").ToBool();
            set
            {
                SetAndSaveConfigValue("EnableAcrylicEffect", value);
                App.ToggleBlurBackdrop(value);
            }
        }

#else
        private bool IsDiscordRPCEnabled
        {
            get => false;
            set => _ = value;
        }

        private bool IsDiscordGameStatusEnabled
        {
            get => false;
            set => _ = value;
        }
#endif

        private int CurrentThemeSelection
        {
            get
            {
                if (IsAppThemeNeedRestart)
                    AppThemeSelectionWarning.Visibility = Visibility.Visible;

                string AppTheme = GetAppConfigValue("ThemeMode").ToString();
                bool IsParseSuccess = Enum.TryParse(typeof(AppThemeMode), AppTheme, out object ThemeIndex);
                return IsParseSuccess ? (int)ThemeIndex : -1;
            }
            set
            {
                if (value < 0) return;
                SetAndSaveConfigValue("ThemeMode", Enum.GetName(typeof(AppThemeMode), value));
                AppThemeSelectionWarning.Visibility = Visibility.Visible;
                IsAppThemeNeedRestart = true;
            }
        }

        private int CurrentAppThreadDownloadValue
        {
            get => GetAppConfigValue("DownloadThread").ToInt();
            set => SetAndSaveConfigValue("DownloadThread", value);
        }

        private int CurrentAppThreadExtractValue
        {
            get => GetAppConfigValue("ExtractionThread").ToInt();
            set => SetAndSaveConfigValue("ExtractionThread", value);
        }

        private List<string> LanguageList => LanguageNames
            .Select(a => string.Format(Lang._SettingsPage.LanguageEntry, a.Value.LangName, a.Value.LangAuthor))
            .ToList();

        private int LanguageSelectedIndex
        {
            get
            {
                string key = GetAppConfigValue("AppLanguage").ToString().ToLower();

                return LanguageNames.ContainsKey(key) ? LanguageNames[key].LangIndex : -1;
            }
            set
            {
                IsAppLangNeedRestart = true;
                AppLangSelectionWarning.Visibility = Visibility.Visible;

                string key = LanguageIDIndex[value];

                SetAndSaveConfigValue("AppLanguage", key);
            }
        }

        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();
        private int SelectedWindowSizeProfile
        {
            get
            {
                string val = CurrentWindowSizeName;
                return WindowSizeProfilesKey.IndexOf(val);
            }
            set
            {
                if (value < 0) return;
                CurrentWindowSizeName = WindowSizeProfilesKey[value];
                var delayedDragAreaChange = async () =>
                {
                    await Task.Delay(250);
                    ChangeTitleDragArea.Change(DragAreaTemplate.Default);
                };
                delayedDragAreaChange();
                SaveAppConfig();
            }
        }

        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                SetAppConfigValue("CurrentCDN", value);
                SaveAppConfig();
            }
        }

        private bool IsIncludeGameLogs
        {
            get => GetAppConfigValue("IncludeGameLogs").ToBool();
            set => SetAndSaveConfigValue("IncludeGameLogs", value);
        }

        private bool IsShowRegionChangeWarning
        {
            get => LauncherConfig.IsShowRegionChangeWarning;
            set
            {
                IsChangeRegionWarningNeedRestart = true;
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

                LauncherConfig.IsShowRegionChangeWarning = value;
            }
        }

        private bool IsUseDownloadChunksMerging
        {
            get => GetAppConfigValue("UseDownloadChunksMerging").ToBool();
            set => SetAndSaveConfigValue("UseDownloadChunksMerging", value);
        }

        private bool IsLowerCollapsePriorityOnGameLaunch
        {
            get => GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool();
            set => SetAndSaveConfigValue("LowerCollapsePrioOnGameLaunch", value);
        }

        #region Keyboard Shortcuts
        private async void ShowKbScList_Click(Object sender, RoutedEventArgs e) => await Dialogs.KeyboardShortcuts.Dialog_ShowKbShortcuts(this);

        private async void ResetKeylist_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialogs.SimpleDialogs.Dialog_ResetKeyboardShortcuts(sender as UIElement) == ContentDialogResult.Primary)
            {
                Dialogs.KeyboardShortcuts.ResetKeyboardShortcuts();
                KeyboardShortcutsEvent(null, AreShortcutsEnabled ? 1 : 2);
            }
        }

        public static event EventHandler<int> KeyboardShortcutsEvent;
        private bool AreShortcutsEnabled
        {
            get
            {
                bool value = GetAppConfigValue("EnableShortcuts").ToBool();
                if (value)
                {
                    KbScBtns.Visibility = Visibility.Visible;
                }
                return value;
            }
            set
            {
                if (value)
                {
                    KbScBtns.Visibility = Visibility.Visible;
                }
                else
                {
                    KbScBtns.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableShortcuts", value);
                KeyboardShortcutsEvent(this, value ? 0 : 2);
            }
        }
        #endregion
    }
}
