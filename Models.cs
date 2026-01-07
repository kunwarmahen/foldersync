using System;
using System.Collections.Generic;
using System.IO;

namespace SyncManager.Models
{
    public class SyncProfile
    {
        public string Name { get; set; }
        public string SourceFolder { get; set; }
        public string DestinationFolder { get; set; }
        public string BackupFolder { get; set; }
        public int BackupVersions { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public bool AutoSyncEnabled { get; set; } = false;

        // Helper to get backup folder with default
        public string GetBackupFolder()
        {
            if (!string.IsNullOrEmpty(BackupFolder))
                return BackupFolder;

            // Default: Documents\SyncManagerBackups\{ProfileName}
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "SyncManagerBackups", Name ?? "Default");
        }
    }

    public class ScheduledTask
    {
        public string TaskName { get; set; }
        public string ProfileName { get; set; }
        public string Frequency { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRun { get; set; }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string ProfileName { get; set; }
        public string Message { get; set; }
    }

    public class AppSettings
    {
        public bool StartMinimized { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool EnableNotifications { get; set; }
        public int DefaultBackupVersions { get; set; } = 3;
        public List<string> ExclusionPatterns { get; set; } = new();
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesBackedUp { get; set; }
        public int FilesSkipped { get; set; }
        public int FilesFailed { get; set; }
        public string Error { get; set; }
    }
}
