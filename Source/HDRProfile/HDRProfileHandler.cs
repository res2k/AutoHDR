﻿using CodectoryCore.UI.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace HDRProfile
{
    public class HDRProfileHandler : BaseViewModel
    {
        Dictionary<ApplicationItem, bool> _lastApplicationStates = new Dictionary<ApplicationItem, bool>();

        private string SettingsPath => $"{System.AppDomain.CurrentDomain.BaseDirectory}\\HDRProfile_Settings.xml";
        TaskbarIcon TrayMenu;
        readonly object _accessLock = new object();

        private bool started = false;
        public bool Started { get => started; private set { started = value; OnPropertyChanged(); } }
        ProcessWatcher ProcessWatcher;
        HDRController HDRSwitcherHandler;
        private bool _showView = false;
        private HDRProfileSettings settings;

        public bool ShowView { get => _showView;  set { _showView = value; OnPropertyChanged(); } }

        public RelayCommand ActivateHDRCommand { get; private set; }
        public RelayCommand DeactivateHDRCommand { get; private set; }
        public RelayCommand AddApplicationCommand { get; private set; }
        public RelayCommand<ApplicationItem> RemoveApplicationCommand { get; private set; }
        public RelayCommand LoadingCommand { get; private set; }
        public RelayCommand ClosingCommand { get; private set; }
        public RelayCommand ShutdownCommand { get; private set; }

        public RelayCommand<ApplicationItem> StartApplicationCommand { get; private set; }


        public HDRProfileSettings Settings { get => settings; set { settings = value; OnPropertyChanged(); } }

        public bool Initialized { get; private set; } = false;

        public HDRProfileHandler()
        {
           // ChangeLanguage( new System.Globalization.CultureInfo("en-US"));
            Initialize();
        }

        private void ChangeLanguage(CultureInfo culture)
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(culture.Name);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture(culture.Name);
        }

        #region Initialization

        private void Initialize()
        {

            lock (_accessLock)
            {
                if (Initialized)
                    return;

                ProcessWatcher = new ProcessWatcher();
                HDRSwitcherHandler = new HDRController();
                LoadSettings();
                InitializeTrayMenu();
                CreateRelayCommands();
                SwitchTrayIcon(Settings.StartMinimizedToTray);
                ShowView = !Settings.StartMinimizedToTray;
                Initialized = true;
            }
        }

        private void LoadSettings()
        {
            try
            {
                Settings = HDRProfileSettings.ReadSettings(SettingsPath);
            }
            catch (Exception)
            {
                Settings = new HDRProfileSettings();
                Settings.SaveSettings(SettingsPath);
            }
            Settings.ApplicationItems.CollectionChanged += ApplicationItems_CollectionChanged;
            settings.PropertyChanged += Settings_PropertyChanged;
            foreach (var application in Settings.ApplicationItems)
            {
                ProcessWatcher.AddProcess(application);
                application.PropertyChanged += ApplicationItem_PropertyChanged;
            }

        }

        private void InitializeTrayMenu()
        {
            TrayMenu = new TaskbarIcon();
            TrayMenu.Visibility = Visibility.Hidden;
            TrayMenu.ToolTipText = Locale_Texts.HDRProfile;
            TrayMenu.Icon = Locale_Texts.Logo;
            ContextMenu contextMenu = new ContextMenu();
            MenuItem close = new MenuItem()
            {
                Header = Locale_Texts.Shutdown
            };
            close.Click += (o, e) => Shutdown();

            MenuItem open = new MenuItem()
            {
                Header = Locale_Texts.Open
            };
            open.Click += (o, e) => SwitchTrayIcon(false);

            contextMenu.Items.Add(open);
            contextMenu.Items.Add(close);
            TrayMenu.ContextMenu = contextMenu;
            TrayMenu.TrayLeftMouseDown += TrayMenu_TrayLeftMouseDown;
        }

        private void CreateRelayCommands()
        {
            ActivateHDRCommand = new RelayCommand(HDRSwitcherHandler.ActivateHDR);
            DeactivateHDRCommand = new RelayCommand(HDRSwitcherHandler.DeactivateHDR);
            AddApplicationCommand = new RelayCommand(AddAplication);
            RemoveApplicationCommand = new RelayCommand<ApplicationItem>(RemoveApplication);
            LoadingCommand = new RelayCommand(Starting);
            ClosingCommand = new RelayCommand(Closing);
            ShutdownCommand = new RelayCommand(Shutdown);
            StartApplicationCommand = new RelayCommand<ApplicationItem>(StartApplication);
        }

        #endregion Initialization

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            lock (_accessLock)
            {
                Tools.SetAutoStart(Locale_Texts.HDRProfile, System.Reflection.Assembly.GetEntryAssembly().Location, settings.AutoStart);
                if (e.PropertyName.Equals(nameof(Settings.HDRMode)))
                    UpdateHDRMode();
                Settings.SaveSettings(SettingsPath);
            }
        } 



        private void TrayMenu_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            SwitchTrayIcon(false);
            ShowView = true;

        }


        private void StartApplication(ApplicationItem application)
        {
            HDRController.SetHDR(true);
            System.Threading.Thread.Sleep(3000);
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(application.ApplicationFilePath);
            process.Start();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            System.Threading.Thread.Sleep(3000);
        }


        private void Starting()
        {
            Start();
        }
        private void Closing()
        {
            if (Settings.CloseToTray)
            {
                SwitchTrayIcon(true);
            }
            else
            {
                Shutdown();
            }
        }

        private void Shutdown()
        {
            Stop();
            SwitchTrayIcon(false);
            Application.Current.Shutdown();
        }

        public void Start()
        {
            if (Started)
                return;
            lock (_accessLock)
            {
                ProcessWatcher.OneProcessIsRunningChanged += ProcessWatcher_RunningOrFocusedChanged;
                ProcessWatcher.OneProcessIsFocusedChanged += ProcessWatcher_RunningOrFocusedChanged;

                Started = true;
                ProcessWatcher.Start();
            }
        }

        public void Stop()
        {
            if (!Started)
                return;
            lock (_accessLock)
            {

                ProcessWatcher.OneProcessIsRunningChanged -= ProcessWatcher_RunningOrFocusedChanged;
                ProcessWatcher.OneProcessIsFocusedChanged -= ProcessWatcher_RunningOrFocusedChanged;

                ProcessWatcher.Stop();
                HDRSwitcherHandler.DeactivateHDR();
                Started = false;
            }

        }

        private void SwitchTrayIcon(bool showTray)
        {
            TrayMenu.Visibility = showTray ? System.Windows.Visibility.Visible : Visibility.Hidden;
        }

        #region Process handling

        private void ApplicationItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            lock (_accessLock)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (var applicationItem in e.NewItems)
                        {
                            ProcessWatcher.AddProcess(((ApplicationItem)applicationItem));
                            ((ApplicationItem)applicationItem).PropertyChanged += ApplicationItem_PropertyChanged;
                        }

                        break;
                    case NotifyCollectionChangedAction.Remove:
                        foreach (var applicationItem in e.OldItems)
                        {
                            ProcessWatcher.RemoveProcess(((ApplicationItem)applicationItem));
                            ((ApplicationItem)applicationItem).PropertyChanged -= ApplicationItem_PropertyChanged;

                        }
                        break;

                }
                Settings.SaveSettings(SettingsPath);
            }
        }

        private void ApplicationItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Settings.SaveSettings(SettingsPath);
        }

        private void AddAplication()
        {
            ApplicationAdder adder = new ApplicationAdder();
            adder.OKClicked += (o, e) =>
            {
                if (!Settings.ApplicationItems.Any(pi => pi.ApplicationFilePath == adder.ApplicationItem.ApplicationFilePath))
                    Settings.ApplicationItems.Add(adder.ApplicationItem);
            };
            if (DialogService != null)
                DialogService.ShowDialogModal(adder);
        }

        private void RemoveApplication(ApplicationItem process)
        {
            Settings.ApplicationItems.Remove(process);

        }


        private void ProcessWatcher_RunningOrFocusedChanged(object sender, EventArgs e)
        {
            UpdateHDRMode();

        }

        private void UpdateHDRMode()
        {
            lock (_accessLock)
            {
                if ((Settings.HDRMode == HDRMode.Running && ProcessWatcher.OneProcessIsRunning) || Settings.HDRMode == HDRMode.Focused && ProcessWatcher.OneProcessIsFocused)
                {
                    HDRSwitcherHandler.ActivateHDR();
                    CheckIfRestartIsNecessary((IDictionary<ApplicationItem, bool>)ProcessWatcher.Applications);
                }
                else if (Settings.HDRMode != HDRMode.None)
                    HDRSwitcherHandler.DeactivateHDR();
            }
        }


        private void CheckIfRestartIsNecessary(IDictionary<ApplicationItem, bool> applicationStates)
        {
            Dictionary<ApplicationItem, bool> newLastStates = new Dictionary<ApplicationItem, bool>();
            foreach (var applicationState in applicationStates)
            {
                if (!applicationState.Key.RestartProcess)
                    continue;
                newLastStates.Add(applicationState.Key, applicationState.Value);
                if (!_lastApplicationStates.ContainsKey(applicationState.Key) && applicationState.Value)
                    RestartProcess(applicationState.Key);
                else if (_lastApplicationStates.ContainsKey(applicationState.Key) && applicationState.Value && !_lastApplicationStates[applicationState.Key])
                    RestartProcess(applicationState.Key);
            }

            _lastApplicationStates.Clear();
            _lastApplicationStates = newLastStates;
        }

        private void RestartProcess(ApplicationItem application)
        {
            Process.GetProcessesByName(application.ApplicationName).ToList().ForEach(p => p.Kill());
            Process proc = new Process();
            StartApplication(application);
        }

        #endregion Process handling

    }

}
