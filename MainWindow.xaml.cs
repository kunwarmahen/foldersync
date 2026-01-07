using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using SyncManager.Models;
using SyncManager.Services;
using SyncManager.Utilities;
using MessageBox = System.Windows.MessageBox;

namespace SyncManager
{
    public partial class MainWindow : Window
    {
        private SyncService _syncService;
        private TaskSchedulerService _taskSchedulerService;
        private LogService _logService;
        private ConfigService _configService;
        private NotificationService _notificationService;
        private FileSystemWatcherService _watcherService;

        private ObservableCollection<SyncProfile> _syncProfiles;
        private ObservableCollection<ScheduledTask> _scheduledTasks;
        private ObservableCollection<LogEntry> _logEntries;

        private NotifyIcon _notifyIcon;
        private bool _isClosing = false;
        private bool _syncInProgress = false;
        private SyncTask _currentSyncTask;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            try
            {
                // Initialize services
                _logService = new LogService();
                _configService = new ConfigService();
                _syncService = new SyncService(_logService);
                _taskSchedulerService = new TaskSchedulerService(_logService);
                _notificationService = new NotificationService();
                _watcherService = new FileSystemWatcherService(_logService);

                // Subscribe to auto-sync events
                _watcherService.SyncTriggered += WatcherService_SyncTriggered;

                // Initialize collections
                _syncProfiles = new ObservableCollection<SyncProfile>();
                _scheduledTasks = new ObservableCollection<ScheduledTask>();
                _logEntries = new ObservableCollection<LogEntry>();

                // Bind data
                ProfilesDataGrid.ItemsSource = _syncProfiles;
                TasksDataGrid.ItemsSource = _scheduledTasks;
                LogDataGrid.ItemsSource = _logEntries;

                // Load configurations
                LoadProfiles();
                LoadScheduledTasks();
                LoadLogs();
                LoadSettings();

                // Setup system tray
                SetupSystemTray();

                _logService.LogInfo("Application started", "System");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _logService.LogError($"Initialization failed: {ex.Message}", "System");
            }
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Sync Manager",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("Exit", null, (s, e) =>
            {
                _isClosing = true;
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void LoadProfiles()
        {
            try
            {
                var profiles = _configService.LoadProfiles();
                _syncProfiles.Clear();
                foreach (var profile in profiles)
                {
                    _syncProfiles.Add(profile);

                    // Start watching if auto-sync is enabled
                    if (profile.AutoSyncEnabled)
                    {
                        _watcherService.StartWatching(profile);
                        profile.Status = "Auto-Sync Active";
                    }
                }
                UpdateComboBoxes();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to load profiles: {ex.Message}", "System");
            }
        }

        private void WatcherService_SyncTriggered(object? sender, string profileName)
        {
            // This is called from a background thread, so we need to dispatch to UI thread
            Dispatcher.Invoke(() =>
            {
                var profile = _syncProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile != null && !_syncInProgress)
                {
                    _logService.LogInfo("Auto-sync triggered by file changes", profileName);
                    ExecuteSync(profile, false);
                }
            });
        }

        private void LoadScheduledTasks()
        {
            try
            {
                var tasks = _taskSchedulerService.GetScheduledTasks();
                _scheduledTasks.Clear();
                foreach (var task in tasks)
                {
                    _scheduledTasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to load scheduled tasks: {ex.Message}", "System");
            }
        }

        private void LoadLogs()
        {
            try
            {
                var logs = _logService.GetLogs(limit: 1000);
                _logEntries.Clear();
                foreach (var log in logs.OrderByDescending(l => l.Timestamp))
                {
                    _logEntries.Add(log);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load logs: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _configService.LoadSettings();
                StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
                MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
                EnableNotificationsCheckBox.IsChecked = settings.EnableNotifications;
                DefaultBackupVersionsTextBox.Text = settings.DefaultBackupVersions.ToString();
                ExclusionPatternsTextBox.Text = string.Join("\n", settings.ExclusionPatterns);
            }
            catch { }
        }

        private void UpdateComboBoxes()
        {
            SyncProfileComboBox.ItemsSource = _syncProfiles;
            SyncProfileComboBox.DisplayMemberPath = "Name";
            SyncProfileComboBox.SelectedIndex = 0;

            TaskProfileComboBox.ItemsSource = _syncProfiles;
            TaskProfileComboBox.DisplayMemberPath = "Name";
        }

        // ==================== PROFILE MANAGEMENT ====================

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder("Select Source Folder");
            if (!string.IsNullOrEmpty(folder))
            {
                SourceFolderTextBox.Text = folder;
            }
        }

        private void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder("Select Destination Folder");
            if (!string.IsNullOrEmpty(folder))
            {
                DestFolderTextBox.Text = folder;
            }
        }

        private void BrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder("Select Backup Folder");
            if (!string.IsNullOrEmpty(folder))
            {
                BackupFolderTextBox.Text = folder;
            }
        }

        private string BrowseFolder(string title)
        {
            using (var dialog = new FolderBrowserDialog { Description = title })
            {
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a profile name.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(SourceFolderTextBox.Text))
                {
                    MessageBox.Show("Please select a source folder.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(DestFolderTextBox.Text))
                {
                    MessageBox.Show("Please select a destination folder.");
                    return;
                }
                if (!int.TryParse(BackupVersionsTextBox.Text, out int backupVersions) || backupVersions < 1)
                {
                    MessageBox.Show("Backup versions must be a positive number.");
                    return;
                }

                var profile = new SyncProfile
                {
                    Name = ProfileNameTextBox.Text,
                    SourceFolder = SourceFolderTextBox.Text,
                    DestinationFolder = DestFolderTextBox.Text,
                    BackupFolder = string.IsNullOrWhiteSpace(BackupFolderTextBox.Text) ? null : BackupFolderTextBox.Text,
                    BackupVersions = backupVersions,
                    CreatedAt = DateTime.Now,
                    Status = "Idle",
                    AutoSyncEnabled = AutoSyncCheckBox.IsChecked ?? false
                };

                // Validate folders exist
                if (!Directory.Exists(profile.SourceFolder))
                {
                    MessageBox.Show("Source folder does not exist.");
                    return;
                }

                if (!Directory.Exists(profile.DestinationFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(profile.DestinationFolder);
                    }
                    catch
                    {
                        MessageBox.Show("Cannot create destination folder. Check permissions.");
                        return;
                    }
                }

                _syncProfiles.Add(profile);
                _configService.SaveProfiles(_syncProfiles.ToList());
                UpdateComboBoxes();

                // Start watching if auto-sync is enabled
                if (profile.AutoSyncEnabled)
                {
                    _watcherService.StartWatching(profile);
                    profile.Status = "Auto-Sync Active";
                }

                // Clear inputs
                ProfileNameTextBox.Clear();
                SourceFolderTextBox.Clear();
                DestFolderTextBox.Clear();
                BackupFolderTextBox.Clear();
                BackupVersionsTextBox.Text = "3";
                AutoSyncCheckBox.IsChecked = true;

                _logService.LogInfo($"Profile created: {profile.Name}", "System");
                MessageBox.Show("Profile created successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating profile: {ex.Message}");
                _logService.LogError($"Failed to create profile: {ex.Message}", "System");
            }
        }

        private void ProfilesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Handle auto-sync checkbox toggle
            if (e.Column.Header.ToString() == "Auto-Sync")
            {
                var profile = e.Row.Item as SyncProfile;
                if (profile != null)
                {
                    // Delay execution until after edit is committed
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (profile.AutoSyncEnabled)
                        {
                            _watcherService.StartWatching(profile);
                            profile.Status = "Auto-Sync Active";
                            _logService.LogInfo($"Auto-sync enabled", profile.Name ?? "");
                        }
                        else
                        {
                            _watcherService.StopWatching(profile.Name ?? "");
                            profile.Status = "Idle";
                            _logService.LogInfo($"Auto-sync disabled", profile.Name ?? "");
                        }

                        // Save updated profiles
                        _configService.SaveProfiles(_syncProfiles.ToList());
                        ProfilesDataGrid.Items.Refresh();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void TestDryRun_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SourceFolderTextBox.Text) || 
                string.IsNullOrWhiteSpace(DestFolderTextBox.Text))
            {
                MessageBox.Show("Please fill in source and destination folders.");
                return;
            }

            var profile = new SyncProfile
            {
                Name = "Test Profile",
                SourceFolder = SourceFolderTextBox.Text,
                DestinationFolder = DestFolderTextBox.Text,
                BackupFolder = string.IsNullOrWhiteSpace(BackupFolderTextBox.Text) ? null : BackupFolderTextBox.Text,
                BackupVersions = int.TryParse(BackupVersionsTextBox.Text, out int v) ? v : 3
            };

            ExecuteSync(profile, dryRun: true);
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProfilesDataGrid.SelectedItem as SyncProfile;
            if (selected == null) return;

            ProfileNameTextBox.Text = selected.Name;
            SourceFolderTextBox.Text = selected.SourceFolder;
            DestFolderTextBox.Text = selected.DestinationFolder;
            BackupFolderTextBox.Text = selected.BackupFolder ?? "";
            BackupVersionsTextBox.Text = selected.BackupVersions.ToString();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProfilesDataGrid.SelectedItem as SyncProfile;
            if (selected == null) return;

            if (MessageBox.Show($"Delete profile '{selected.Name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Stop watching if auto-sync is enabled
                if (selected.AutoSyncEnabled)
                {
                    _watcherService.StopWatching(selected.Name ?? "");
                }

                _syncProfiles.Remove(selected);
                _configService.SaveProfiles(_syncProfiles.ToList());
                UpdateComboBoxes();
                _logService.LogInfo($"Profile deleted: {selected.Name}", "System");
            }
        }

        // ==================== SYNC OPERATIONS ====================

        private void SyncNow_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProfilesDataGrid.SelectedItem as SyncProfile;
            if (selected == null)
            {
                MessageBox.Show("Please select a profile to sync.");
                return;
            }

            ExecuteSync(selected, dryRun: false);
        }

        private void StartSync_Click(object sender, RoutedEventArgs e)
        {
            var selected = SyncProfileComboBox.SelectedItem as SyncProfile;
            if (selected == null)
            {
                MessageBox.Show("Please select a profile.");
                return;
            }

            bool dryRun = DryRunCheckBox.IsChecked == true;
            ExecuteSync(selected, dryRun);
        }

        private void CancelSync_Click(object sender, RoutedEventArgs e)
        {
            _currentSyncTask?.Cancel();
            _syncInProgress = false;
            UpdateSyncUI();
        }

        private async void ExecuteSync(SyncProfile profile, bool dryRun = false)
        {
            if (_syncInProgress)
            {
                MessageBox.Show("A sync operation is already in progress.");
                return;
            }

            _syncInProgress = true;
            _currentSyncTask = new SyncTask();
            UpdateSyncUI();
            SyncOutputTextBox.Clear();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var message = dryRun ? $"[{timestamp}] Starting DRY RUN sync for '{profile.Name}'" 
                                  : $"[{timestamp}] Starting sync for '{profile.Name}'";
            
            AppendSyncOutput(message);

            try
            {
                profile.Status = "Syncing";
                var result = await Task.Run(() => 
                {
                    return _syncService.ExecuteSync(profile, dryRun, 
                        (msg) => AppendSyncOutput(msg), _currentSyncTask);
                });

                if (result.Success)
                {
                    profile.Status = "Success";
                    var summary = $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync completed successfully!\n" +
                                $"Files processed: {result.FilesProcessed}\n" +
                                $"Files backed up: {result.FilesBackedUp}\n" +
                                $"Files skipped: {result.FilesSkipped}";
                    AppendSyncOutput(summary);

                    _logService.LogSuccess($"Sync completed: {result.FilesProcessed} files", profile.Name);
                    
                    if (_configService.LoadSettings().EnableNotifications)
                    {
                        _notificationService.ShowNotification("Sync Complete", 
                            $"{profile.Name}: {result.FilesProcessed} files synced");
                    }
                }
                else
                {
                    profile.Status = "Failed";
                    AppendSyncOutput($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync failed: {result.Error}");
                    _logService.LogError(result.Error, profile.Name);
                }

                RefreshLogs();
            }
            catch (Exception ex)
            {
                profile.Status = "Error";
                AppendSyncOutput($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}");
                _logService.LogError(ex.Message, profile.Name);
            }
            finally
            {
                _syncInProgress = false;
                _currentSyncTask = null;
                UpdateSyncUI();
                ProfilesDataGrid.Items.Refresh();
            }
        }

        private void AppendSyncOutput(string message)
        {
            Dispatcher.Invoke(() =>
            {
                SyncOutputTextBox.AppendText(message + "\n");
                SyncOutputTextBox.ScrollToEnd();
                SyncStatusText.Text = "Status: Running...";
            });
        }

        private void UpdateSyncUI()
        {
            StartSyncButton.IsEnabled = !_syncInProgress;
            CancelSyncButton.IsEnabled = _syncInProgress;
            SyncProfileComboBox.IsEnabled = !_syncInProgress;
            SyncProgressBar.Visibility = _syncInProgress ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================== SCHEDULED TASKS ====================

        private void CreateTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a task name.");
                    return;
                }

                var profile = TaskProfileComboBox.SelectedItem as SyncProfile;
                if (profile == null)
                {
                    MessageBox.Show("Please select a profile.");
                    return;
                }

                var frequencyText = (TaskFrequencyComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (string.IsNullOrEmpty(frequencyText))
                {
                    MessageBox.Show("Please select a frequency.");
                    return;
                }

                var task = new ScheduledTask
                {
                    TaskName = TaskNameTextBox.Text,
                    ProfileName = profile.Name,
                    Frequency = frequencyText,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                // Create Windows scheduled task
                _taskSchedulerService.CreateScheduledTask(task, profile);
                _scheduledTasks.Add(task);
                _configService.SaveScheduledTasks(_scheduledTasks.ToList());

                TaskNameTextBox.Clear();
                _logService.LogInfo($"Scheduled task created: {task.TaskName}", "System");
                MessageBox.Show("Scheduled task created successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating task: {ex.Message}");
                _logService.LogError($"Failed to create task: {ex.Message}", "System");
            }
        }

        private void RunTask_Click(object sender, RoutedEventArgs e)
        {
            var selected = TasksDataGrid.SelectedItem as ScheduledTask;
            if (selected == null) return;

            var profile = _syncProfiles.FirstOrDefault(p => p.Name == selected.ProfileName);
            if (profile != null)
            {
                ExecuteSync(profile, dryRun: false);
                selected.LastRun = DateTime.Now;
                TasksDataGrid.Items.Refresh();
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            var selected = TasksDataGrid.SelectedItem as ScheduledTask;
            if (selected == null) return;

            if (MessageBox.Show($"Delete task '{selected.TaskName}'?", "Confirm", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _taskSchedulerService.DeleteScheduledTask(selected.TaskName);
                _scheduledTasks.Remove(selected);
                _configService.SaveScheduledTasks(_scheduledTasks.ToList());
                _logService.LogInfo($"Scheduled task deleted: {selected.TaskName}", "System");
            }
        }

        // ==================== ACTIVITY LOG ====================

        private void RefreshLogs_Click(object sender, RoutedEventArgs e)
        {
            RefreshLogs();
        }

        private void RefreshLogs()
        {
            LoadLogs();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear all logs?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _logService.ClearLogs();
                _logEntries.Clear();
            }
        }

        // ==================== SETTINGS ====================

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new AppSettings
                {
                    StartMinimized = StartMinimizedCheckBox.IsChecked == true,
                    MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true,
                    EnableNotifications = EnableNotificationsCheckBox.IsChecked == true,
                    DefaultBackupVersions = int.TryParse(DefaultBackupVersionsTextBox.Text, out int v) ? v : 3,
                    ExclusionPatterns = ExclusionPatternsTextBox.Text.Split(new[] { "\r\n", "\n" }, 
                        StringSplitOptions.RemoveEmptyEntries).ToList()
                };

                _configService.SaveSettings(settings);
                _logService.LogInfo("Settings saved", "System");
                MessageBox.Show("Settings saved successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}");
            }
        }

        // ==================== WINDOW EVENTS ====================

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing && _configService.LoadSettings().MinimizeToTray)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                ExitApplication();
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            _isClosing = true;

            // Stop all file watchers
            _watcherService?.StopAll();

            _notifyIcon?.Dispose();

            // Don't call this.Close() - it causes crash when called during Window_Closing
            // The window will close naturally from the Window_Closing event
        }
    }
}
