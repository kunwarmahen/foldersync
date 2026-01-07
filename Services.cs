using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using SyncManager.Models;
using SyncManager.Utilities;

namespace SyncManager.Services
{
    public class SyncService
    {
        private readonly LogService _logService;
        private readonly string _backupSubFolder = ".backups";
        private readonly string _metadataFile = ".sync-metadata.json";

        public SyncService(LogService logService)
        {
            _logService = logService;
        }

        public SyncResult ExecuteSync(SyncProfile profile, bool dryRun, Action<string> progressCallback, SyncTask cancelToken)
        {
            var result = new SyncResult();

            try
            {
                progressCallback($"[{DateTime.Now:HH:mm:ss}] Validating folders...");

                // Validate source
                if (!Directory.Exists(profile.SourceFolder))
                {
                    result.Error = "Source folder does not exist";
                    result.Success = false;
                    return result;
                }

                // Create destination if needed
                if (!Directory.Exists(profile.DestinationFolder))
                {
                    Directory.CreateDirectory(profile.DestinationFolder);
                    progressCallback($"Created destination folder");
                }

                // Initialize backup folder (separate from destination)
                var backupFolder = profile.GetBackupFolder();
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                    progressCallback($"Created backup folder: {backupFolder}");
                }

                progressCallback($"[{DateTime.Now:HH:mm:ss}] Starting sync operation...");

                // Get all files from source
                var sourceFiles = Directory.GetFiles(profile.SourceFolder, "*", SearchOption.AllDirectories);
                progressCallback($"Found {sourceFiles.Length} files in source");

                foreach (var sourceFile in sourceFiles)
                {
                    if (cancelToken?.CancellationRequested == true)
                    {
                        progressCallback("Sync cancelled by user");
                        break;
                    }

                    var relativePath = sourceFile.Substring(profile.SourceFolder.Length).TrimStart('\\');
                    var destFile = Path.Combine(profile.DestinationFolder, relativePath);
                    var destDir = Path.GetDirectoryName(destFile);

                    // Check exclusions
                    if (IsExcluded(relativePath, profile.DestinationFolder))
                    {
                        result.FilesSkipped++;
                        continue;
                    }

                    try
                    {
                        // Create destination directory
                        if (!Directory.Exists(destDir))
                        {
                            if (!dryRun)
                            {
                                Directory.CreateDirectory(destDir);
                            }
                        }

                        // Check if file has changed
                        if (HasFileChanged(sourceFile, destFile))
                        {
                            progressCallback($"[{DateTime.Now:HH:mm:ss}] Syncing: {relativePath}");

                            if (!dryRun)
                            {
                                // Create backup if destination exists
                                if (File.Exists(destFile))
                                {
                                    BackupFile(destFile, backupFolder, Path.GetFileName(destFile));
                                    result.FilesBackedUp++;
                                }

                                // Copy new file
                                File.Copy(sourceFile, destFile, overwrite: true);

                                // Rotate old backups
                                RotateBackups(backupFolder, Path.GetFileName(destFile), profile.BackupVersions);
                            }

                            result.FilesProcessed++;
                        }
                        else
                        {
                            result.FilesSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        progressCallback($"ERROR syncing {relativePath}: {ex.Message}");
                        result.FilesFailed++;
                        _logService.LogError($"Failed to sync {relativePath}: {ex.Message}", profile.Name);
                    }
                }

                progressCallback($"[{DateTime.Now:HH:mm:ss}] Sync operation completed");
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                progressCallback($"FATAL ERROR: {ex.Message}");
                _logService.LogError(ex.Message, profile.Name);
            }

            return result;
        }

        private bool HasFileChanged(string sourceFile, string destFile)
        {
            if (!File.Exists(destFile))
                return true;

            var sourceInfo = new FileInfo(sourceFile);
            var destInfo = new FileInfo(destFile);

            // Quick check: compare sizes
            if (sourceInfo.Length != destInfo.Length)
                return true;

            // Deep check: compare file hashes
            return GetFileHash(sourceFile) != GetFileHash(destFile);
        }

        private string GetFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
            catch
            {
                return null;
            }
        }

        private void BackupFile(string filePath, string backupFolder, string fileName)
        {
            try
            {
                var fileBackupFolder = Path.Combine(backupFolder, fileName);
                if (!Directory.Exists(fileBackupFolder))
                {
                    Directory.CreateDirectory(fileBackupFolder);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Preserve original file extension
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var backupName = $"{fileNameWithoutExt}_{timestamp}{extension}";

                var backupPath = Path.Combine(fileBackupFolder, backupName);

                File.Copy(filePath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to backup {fileName}: {ex.Message}", "Sync");
            }
        }

        private void RotateBackups(string backupFolder, string fileName, int retentionCount)
        {
            try
            {
                var fileBackupFolder = Path.Combine(backupFolder, fileName);
                if (!Directory.Exists(fileBackupFolder))
                    return;

                // Match pattern: {fileNameWithoutExt}_*.{extension}
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var pattern = $"{fileNameWithoutExt}_*{extension}";

                var backups = Directory.GetFiles(fileBackupFolder, pattern)
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();

                if (backups.Count > retentionCount)
                {
                    foreach (var oldBackup in backups.Skip(retentionCount))
                    {
                        File.Delete(oldBackup);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to rotate backups: {ex.Message}", "Sync");
            }
        }

        private bool IsExcluded(string relativePath, string destFolder)
        {
            var exclusions = new[]
            {
                ".sync-metadata.json",
                ".backups",
                "System Volume Information",
                "Thumbs.db",
                ".tmp"
            };

            return exclusions.Any(pattern => 
                relativePath.Contains(pattern) || 
                relativePath.StartsWith("~") ||
                relativePath.Contains("~$"));
        }
    }

    public class LogService
    {
        private readonly string _logDirectory;
        private readonly string _logFile;

        public LogService()
        {
            _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "SyncManager", "Logs");
            Directory.CreateDirectory(_logDirectory);
            _logFile = Path.Combine(_logDirectory, $"sync-{DateTime.Now:yyyyMMdd}.log");
        }

        public void LogInfo(string message, string profileName)
        {
            Log("INFO", message, profileName);
        }

        public void LogSuccess(string message, string profileName)
        {
            Log("SUCCESS", message, profileName);
        }

        public void LogWarning(string message, string profileName)
        {
            Log("WARN", message, profileName);
        }

        public void LogError(string message, string profileName)
        {
            Log("ERROR", message, profileName);
        }

        private void Log(string level, string message, string profileName)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] [{level}] [{profileName}] {message}";
                
                File.AppendAllText(_logFile, logEntry + Environment.NewLine);
            }
            catch { }
        }

        public List<LogEntry> GetLogs(int limit = 1000)
        {
            var logs = new List<LogEntry>();

            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                    .OrderByDescending(f => f)
                    .Take(5); // Last 5 days

                foreach (var file in logFiles)
                {
                    var lines = File.ReadAllLines(file).TakeLast(limit);
                    foreach (var line in lines)
                    {
                        if (TryParseLogEntry(line, out var entry))
                        {
                            logs.Add(entry);
                        }
                    }
                }
            }
            catch { }

            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        private bool TryParseLogEntry(string line, out LogEntry entry)
        {
            entry = null;
            try
            {
                // Parse format: [timestamp] [level] [profile] message
                var parts = line.Split(new[] { "] [" }, StringSplitOptions.None);
                if (parts.Length < 4) return false;

                var timestamp = DateTime.Parse(parts[0].Substring(1));
                var level = parts[1];
                var profile = parts[2];
                var message = string.Join("] [", parts.Skip(3)).TrimEnd(']');

                entry = new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level,
                    ProfileName = profile,
                    Message = message
                };

                return true;
            }
            catch { }

            return false;
        }

        public void ClearLogs()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_logDirectory, "*.log"))
                {
                    File.Delete(file);
                }
            }
            catch { }
        }
    }

    public class ConfigService
    {
        private readonly string _configDirectory;
        private readonly string _profilesFile;
        private readonly string _tasksFile;
        private readonly string _settingsFile;

        public ConfigService()
        {
            _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "SyncManager");
            Directory.CreateDirectory(_configDirectory);

            _profilesFile = Path.Combine(_configDirectory, "profiles.json");
            _tasksFile = Path.Combine(_configDirectory, "tasks.json");
            _settingsFile = Path.Combine(_configDirectory, "settings.json");
        }

        public List<SyncProfile> LoadProfiles()
        {
            try
            {
                if (!File.Exists(_profilesFile))
                    return new List<SyncProfile>();

                var json = File.ReadAllText(_profilesFile);
                return JsonSerializer.Deserialize<List<SyncProfile>>(json) ?? new List<SyncProfile>();
            }
            catch
            {
                return new List<SyncProfile>();
            }
        }

        public void SaveProfiles(List<SyncProfile> profiles)
        {
            try
            {
                var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_profilesFile, json);
            }
            catch { }
        }

        public List<ScheduledTask> LoadScheduledTasks()
        {
            try
            {
                if (!File.Exists(_tasksFile))
                    return new List<ScheduledTask>();

                var json = File.ReadAllText(_tasksFile);
                return JsonSerializer.Deserialize<List<ScheduledTask>>(json) ?? new List<ScheduledTask>();
            }
            catch
            {
                return new List<ScheduledTask>();
            }
        }

        public void SaveScheduledTasks(List<ScheduledTask> tasks)
        {
            try
            {
                var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tasksFile, json);
            }
            catch { }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFile))
                    return new AppSettings();

                var json = File.ReadAllText(_settingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }
    }

    public class TaskSchedulerService
    {
        private readonly LogService _logService;

        public TaskSchedulerService(LogService logService)
        {
            _logService = logService;
        }

        public List<ScheduledTask> GetScheduledTasks()
        {
            // In a full implementation, this would query Windows Task Scheduler
            // For now, we load from config
            var config = new ConfigService();
            return config.LoadScheduledTasks();
        }

        public void CreateScheduledTask(ScheduledTask task, SyncProfile profile)
        {
            try
            {
                // This would integrate with Windows Task Scheduler API
                // For MVP, we just log it
                _logService.LogInfo($"Task scheduled: {task.TaskName}", profile.Name);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to create task: {ex.Message}", "System");
            }
        }

        public void DeleteScheduledTask(string taskName)
        {
            try
            {
                _logService.LogInfo($"Task deleted: {taskName}", "System");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to delete task: {ex.Message}", "System");
            }
        }
    }

    public class FileSystemWatcherService
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers;
        private readonly Dictionary<string, System.Timers.Timer> _debounceTimers;
        private readonly LogService _logService;
        private const int DEBOUNCE_DELAY_MS = 2000; // Wait 2 seconds after last change before syncing

        public event EventHandler<string>? SyncTriggered;

        public FileSystemWatcherService(LogService logService)
        {
            _watchers = new Dictionary<string, FileSystemWatcher>();
            _debounceTimers = new Dictionary<string, System.Timers.Timer>();
            _logService = logService;
        }

        public void StartWatching(SyncProfile profile)
        {
            if (string.IsNullOrEmpty(profile.SourceFolder) || !Directory.Exists(profile.SourceFolder))
            {
                _logService.LogWarning($"Cannot start watching: Source folder does not exist", profile.Name ?? "Unknown");
                return;
            }

            // Stop existing watcher if any
            StopWatching(profile.Name ?? "");

            try
            {
                var watcher = new FileSystemWatcher
                {
                    Path = profile.SourceFolder,
                    NotifyFilter = NotifyFilters.FileName |
                                 NotifyFilters.DirectoryName |
                                 NotifyFilters.LastWrite |
                                 NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                // Subscribe to all change events
                watcher.Changed += (s, e) => OnFileChanged(profile.Name ?? "", e);
                watcher.Created += (s, e) => OnFileChanged(profile.Name ?? "", e);
                watcher.Deleted += (s, e) => OnFileChanged(profile.Name ?? "", e);
                watcher.Renamed += (s, e) => OnFileChanged(profile.Name ?? "", e);

                _watchers[profile.Name ?? ""] = watcher;

                _logService.LogInfo($"Started monitoring folder: {profile.SourceFolder}", profile.Name ?? "");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to start monitoring: {ex.Message}", profile.Name ?? "");
            }
        }

        public void StopWatching(string profileName)
        {
            if (_watchers.ContainsKey(profileName))
            {
                _watchers[profileName].EnableRaisingEvents = false;
                _watchers[profileName].Dispose();
                _watchers.Remove(profileName);

                _logService.LogInfo($"Stopped monitoring", profileName);
            }

            if (_debounceTimers.ContainsKey(profileName))
            {
                _debounceTimers[profileName].Stop();
                _debounceTimers[profileName].Dispose();
                _debounceTimers.Remove(profileName);
            }
        }

        public void StopAll()
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

            foreach (var timer in _debounceTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _debounceTimers.Clear();
        }

        public bool IsWatching(string profileName)
        {
            return _watchers.ContainsKey(profileName) && _watchers[profileName].EnableRaisingEvents;
        }

        private void OnFileChanged(string profileName, FileSystemEventArgs e)
        {
            // Ignore temporary files and system files
            if (ShouldIgnoreFile(e.Name ?? ""))
                return;

            _logService.LogInfo($"Detected change: {e.ChangeType} - {e.Name}", profileName);

            // Debounce: Reset timer on each change
            // This prevents multiple rapid syncs when many files change at once
            if (_debounceTimers.ContainsKey(profileName))
            {
                _debounceTimers[profileName].Stop();
                _debounceTimers[profileName].Start();
            }
            else
            {
                var timer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
                timer.AutoReset = false;
                timer.Elapsed += (s, args) => TriggerSync(profileName);
                _debounceTimers[profileName] = timer;
                timer.Start();
            }
        }

        private void TriggerSync(string profileName)
        {
            _logService.LogInfo($"Auto-sync triggered after detecting changes", profileName);
            SyncTriggered?.Invoke(this, profileName);
        }

        private bool ShouldIgnoreFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return true;

            var ignoredExtensions = new[] { ".tmp", ".temp", ".crdownload", ".part" };
            var ignoredPrefixes = new[] { "~$", ".~" };

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (ignoredExtensions.Contains(extension))
                return true;

            foreach (var prefix in ignoredPrefixes)
            {
                if (fileName.StartsWith(prefix))
                    return true;
            }

            return false;
        }
    }

    public class NotificationService
    {
        public void ShowNotification(string title, string message)
        {
            try
            {
                // Windows 10+ toast notification
                // Note: Toast notifications require UWP APIs, not available in WPF
                // For WPF, use system tray notifications or custom windows instead

                // Simplified for now - placeholder for future implementation
            }
            catch { }
        }
    }
}

namespace SyncManager.Utilities
{
    public class SyncTask
    {
        public bool CancellationRequested { get; set; }

        public void Cancel()
        {
            CancellationRequested = true;
        }
    }
}
