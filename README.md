# Sync Manager - Folder Synchronization & Backup Tool

A Windows desktop application for automated folder synchronization with version-controlled backups and real-time monitoring.

## Features

### Core Functionality
- **Manual Sync** - On-demand synchronization via "Sync Now" button
- **Auto-Sync** - Real-time file monitoring with automatic synchronization
- **Scheduled Sync** - Windows Task Scheduler integration for periodic syncs
- **Version Control** - Configurable backup versions (keep last N backups)
- **Smart Detection** - SHA256 hash-based change detection to avoid unnecessary copies

### Backup Management
- **Separate Backup Location** - Backups stored in configurable folder (default: Documents\SyncManagerBackups)
- **Original Extensions Preserved** - Backup files maintain their original extensions (e.g., `.docx`, `.pdf`)
- **Automatic Rotation** - Old backups automatically deleted based on retention count
- **Organized Structure** - Each file's backups stored in dedicated subfolders

### Auto-Sync Features
- **Real-Time Monitoring** - FileSystemWatcher detects changes instantly
- **Debouncing** - 2-second delay prevents sync storms during rapid changes
- **Smart Filtering** - Ignores temporary files (`.tmp`, `~$`, etc.)
- **Per-Profile Toggle** - Enable/disable auto-sync for each profile independently

### User Interface
- **Professional WPF GUI** - Modern Windows interface with 5 tabs
- **Profile Management** - Create, edit, delete sync profiles
- **Real-Time Progress** - Live sync status and detailed output
- **Activity Log** - Comprehensive logging with filtering capabilities
- **System Tray** - Minimize to tray and background operation
- **Dry-Run Mode** - Test sync operations without making changes

---

## Screenshots

### Main Interface
```
┌─────────────────────────────────────────────────────┐
│  Folder Sync Manager                                │
│  Automated backup with version history              │
├─────────────────────────────────────────────────────┤
│ [📋 Sync Profiles] [⚡ Sync Now] [🕐 Scheduled]    │
│ [📊 Activity Log] [⚙️ Settings]                     │
├─────────────────────────────────────────────────────┤
│ Active Sync Profiles                                │
│ ┌───────────────────────────────────────────────┐  │
│ │Name    │Source  │Dest   │Backups│Auto│Status │  │
│ │MyDocs  │C:\Docs │D:\Bak │3      │✓   │Active │  │
│ └───────────────────────────────────────────────┘  │
│                                                     │
│ Create New Sync Profile                            │
│ Profile Name:      [_____________]                  │
│ Source Folder:     [_____________] [Browse]         │
│ Destination:       [_____________] [Browse]         │
│ Backup Folder:     [_____________] [Browse]         │
│ Backup Versions:   [3]                              │
│ ☑ Enable Auto-Sync (monitor folder for changes)    │
│                           [Create] [Test Dry-Run]   │
└─────────────────────────────────────────────────────┘
```

---

## System Requirements

### Windows (Target Platform)
- **OS:** Windows 10 or later (64-bit)
- **Framework:** .NET 8.0 (included in self-contained build)
- **Disk Space:** ~170 MB for installation
- **Permissions:** User-level access (Administrator for Task Scheduler)

### Ubuntu (Build Platform)
- **OS:** Ubuntu 20.04 or later
- **SDK:** .NET 8.0 SDK
- **Disk Space:** ~500 MB for SDK and build
- **Architecture:** x64

---

## Setup & Build Instructions

### Option 1: Build on Ubuntu (Cross-Compile for Windows)

#### Step 1: Install .NET 8.0 SDK

```bash
# Download Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Install the package
sudo dpkg -i packages-microsoft-prod.deb

# Clean up
rm packages-microsoft-prod.deb

# Update package list
sudo apt-get update

# Install .NET SDK
sudo apt-get install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
# Should output: 8.0.xxx
```

#### Step 2: Clone/Download Source Code

```bash
# If using Git
git clone <your-repository-url>
cd foldersync

# Or download and extract ZIP
unzip foldersync.zip
cd foldersync
```

#### Step 3: Build Windows Executable

```bash
# Clean previous builds
dotnet clean

# Build self-contained Windows x64 executable
dotnet publish -c Release -r win-x64 --self-contained -o ./publish

# Verify build
ls -lh publish/SyncManager.exe
file publish/SyncManager.exe
# Should show: PE32+ executable (GUI) x86-64, for MS Windows
```

**Build Output:**
- Location: `./publish/SyncManager.exe`
- Size: ~148 KB executable + ~161 MB .NET runtime
- Type: Self-contained (no .NET required on target Windows machine)

#### Step 4: Transfer to Windows

```bash
# Option A: Using SCP
scp -r publish/ user@windows-machine:/path/to/destination/

# Option B: Using network share
cp -r publish/ /mnt/windows-share/SyncManager/

# Option C: USB drive
cp -r publish/ /media/usb-drive/SyncManager/

# Option D: Create ZIP for manual transfer
zip -r SyncManager.zip publish/
# Transfer SyncManager.zip to Windows and extract
```

---

### Option 2: Build on Windows

#### Step 1: Install .NET 8.0 SDK

1. Download .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Run the installer (`dotnet-sdk-8.0.xxx-win-x64.exe`)
3. Follow installation wizard
4. Verify installation:

```powershell
dotnet --version
# Should output: 8.0.xxx
```

#### Step 2: Install Build Tools (Optional)

For Visual Studio users:
1. Install **Visual Studio 2022** (Community/Professional/Enterprise)
2. During installation, select:
   - .NET desktop development workload
   - Windows Presentation Foundation components

For command-line builds:
- .NET SDK is sufficient (no Visual Studio required)

#### Step 3: Clone/Download Source Code

```powershell
# If using Git
git clone <your-repository-url>
cd foldersync

# Or download and extract ZIP
Expand-Archive -Path foldersync.zip -DestinationPath .
cd foldersync
```

#### Step 4: Build Application

**Option A: Using Command Line**

```powershell
# Clean previous builds
dotnet clean

# Build self-contained executable
dotnet publish -c Release -r win-x64 --self-contained -o .\publish

# Verify build
dir .\publish\SyncManager.exe
```

**Option B: Using Visual Studio**

1. Open `SyncManager.sln` (or create solution from .csproj)
2. Set build configuration to **Release**
3. Right-click project → **Publish**
4. Choose folder publish target
5. Configure:
   - Target Runtime: `win-x64`
   - Deployment Mode: `Self-contained`
   - Target Location: `.\publish`
6. Click **Publish**

**Build Output:**
- Location: `.\publish\SyncManager.exe`
- Size: ~148 KB executable + ~161 MB .NET runtime

---

## Running the Application

### First-Time Setup

1. Navigate to the `publish` folder
2. **Important:** Right-click `SyncManager.exe` → Properties
3. If you see "Unblock" checkbox at the bottom, check it and click Apply
4. Double-click `SyncManager.exe` to launch

### Basic Usage

#### Creating a Sync Profile

1. Go to **"Sync Profiles"** tab
2. Fill in the form:
   ```
   Profile Name:      My Documents
   Source Folder:     C:\Users\YourName\Documents
   Destination:       D:\Backup\Documents
   Backup Folder:     (leave empty for default, or choose custom)
   Backup Versions:   3
   ☑ Enable Auto-Sync
   ```
3. Click **"Create Profile"**

**Default Backup Location:**
If left empty: `C:\Users\YourName\Documents\SyncManagerBackups\{ProfileName}\`

#### Manual Sync

1. Select a profile from the list
2. Click **"Sync Now"** button
3. Watch progress in the output window

#### Auto-Sync (Real-Time Monitoring)

1. Check the **"Auto-Sync"** checkbox for a profile
2. Status changes to **"Auto-Sync Active"**
3. Edit/create files in the source folder
4. Wait 2-3 seconds
5. Files automatically sync to destination
6. Check **Activity Log** tab for sync events

#### Scheduled Sync

1. Go to **"Scheduled Tasks"** tab
2. Select a profile
3. Choose frequency (Hourly/Daily/Weekly)
4. Click **"Create Task"**
5. Windows Task Scheduler will run sync automatically

---

## File Structure

### Source Code Files (Check into Version Control)

```
foldersync/
├── App.xaml                 # Application entry point (XAML)
├── App.xaml.cs             # Application startup logic
├── MainWindow.xaml         # Main UI definition
├── MainWindow.xaml.cs      # Main UI logic and event handlers
├── Models.cs               # Data models (SyncProfile, LogEntry, etc.)
├── Services.cs             # Business logic services
├── SyncManager.csproj      # Project configuration
├── .gitignore              # Git ignore rules
└── README.md               # This file
```

### Build Artifacts (Do NOT Check In)

```
bin/                        # Intermediate build files
obj/                        # Object files
publish/                    # Published application
.vs/                        # Visual Studio settings
*.user                      # User-specific settings
```

### Application Data (Runtime)

The application creates these folders at runtime:

**Windows:**
```
C:\Users\YourName\AppData\Roaming\SyncManager\
├── Logs\                   # Activity logs
│   └── sync-20260106.log
├── profiles.json           # Saved sync profiles
├── tasks.json              # Scheduled tasks
└── settings.json           # Application settings
```

**Backup Storage:**
```
C:\Users\YourName\Documents\SyncManagerBackups\
└── {ProfileName}\          # One folder per profile
    └── {FileName}\         # One folder per backed-up file
        ├── report_20260106_195800.docx
        ├── report_20260106_200100.docx
        └── report_20260106_201500.docx
```

---

## Configuration

### Sync Profile Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Profile Name | Unique identifier | Required |
| Source Folder | Folder to monitor and sync from | Required |
| Destination Folder | Where files are copied to | Required |
| Backup Folder | Where old versions are stored | `Documents\SyncManagerBackups\{ProfileName}` |
| Backup Versions | Number of old versions to keep | 3 |
| Auto-Sync | Enable real-time monitoring | Enabled |

### Application Settings

Access via **Settings** tab:

| Setting | Description | Default |
|---------|-------------|---------|
| Start Minimized | Launch to system tray | Disabled |
| Minimize to Tray | Hide window when minimized | Enabled |
| Enable Notifications | Show sync completion alerts | Enabled |
| Default Backup Versions | Default value for new profiles | 3 |
| Exclusion Patterns | Files/folders to skip | `.tmp`, `.backups`, `~$` |

---

## Advanced Features

### Debounce Delay Configuration

The auto-sync debounce delay prevents sync storms when many files change rapidly.

**Default:** 2000 ms (2 seconds)

To change:
1. Open `Services.cs`
2. Find line ~499: `private const int DEBOUNCE_DELAY_MS = 2000;`
3. Modify value:
   ```csharp
   private const int DEBOUNCE_DELAY_MS = 5000; // 5 seconds
   ```
4. Rebuild application

**Recommendations:**
- SSD/Fast local drives: 1000-2000 ms
- Network drives: 3000-5000 ms
- Slow/remote drives: 5000-10000 ms

### File Exclusions

Auto-sync ignores these file patterns:

**Extensions:**
- `.tmp`, `.temp` - Temporary files
- `.crdownload` - Chrome downloads
- `.part` - Partial downloads

**Prefixes:**
- `~$` - Microsoft Office temp files
- `.~` - Various application temp files

**Folders:**
- `.backups` - Backup storage folders
- `.sync-metadata.json` - Sync metadata
- `System Volume Information` - Windows system folder

To add more exclusions, modify `ShouldIgnoreFile()` in `Services.cs`.

---

## Troubleshooting

### Build Issues

**Problem:** `dotnet: command not found`

**Solution:**
```bash
# Ubuntu
export PATH="$HOME/.dotnet:$PATH"
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc

# Windows - Add to PATH environment variable
# C:\Program Files\dotnet\
```

**Problem:** Build succeeds but warnings about nullable references

**Solution:** These are safe to ignore. They're C# 8.0 nullable reference warnings.

---

### Runtime Issues

**Problem:** Application doesn't start on Windows

**Solutions:**
1. Right-click `SyncManager.exe` → Properties → Unblock
2. Run as Administrator (right-click → Run as administrator)
3. Check Windows Event Viewer for detailed error
4. Ensure Windows 10+ (64-bit)

**Problem:** Antivirus blocks the executable

**Solutions:**
1. Add exception for `SyncManager.exe`
2. Add exception for entire `publish` folder
3. Code-sign the executable (for production deployment)

**Problem:** Auto-sync not triggering

**Solutions:**
1. Verify "Auto-Sync" checkbox is checked
2. Check status shows "Auto-Sync Active"
3. Wait 2-3 seconds after file changes
4. Check source folder path exists
5. Review Activity Log for error messages

**Problem:** "Access Denied" errors during sync

**Solutions:**
1. Ensure you have write permissions to destination
2. Close files before syncing (don't sync open files)
3. Run application as Administrator if needed
4. Check antivirus isn't locking files

---

## How It Works

### Sync Algorithm

1. **File Discovery**
   - Recursively scan source folder
   - Get all files and subdirectories
   - Apply exclusion filters

2. **Change Detection**
   - Compare file sizes first (fast check)
   - If sizes match, compute SHA256 hashes
   - Only copy if hashes differ

3. **Backup Process**
   - If destination file exists and differs:
     - Copy existing file to backup folder
     - Filename: `{name}_{timestamp}.{ext}`
     - Location: `{BackupFolder}\{ProfileName}\{FileName}\`

4. **File Copy**
   - Copy source file to destination
   - Preserve directory structure
   - Overwrite existing file

5. **Backup Rotation**
   - List all backups for file
   - Sort by creation time (newest first)
   - Delete backups beyond retention count

### Auto-Sync Flow

```
File Change Detected
    ↓
FileSystemWatcher Event
    ↓
Filter Temporary Files
    ↓
Start/Reset 2-Second Timer
    ↓
(No more changes for 2 seconds)
    ↓
Trigger Sync
    ↓
Execute Full Sync Process
    ↓
Log Activity
```

---

## Technical Details

### Technology Stack
- **Framework:** .NET 8.0
- **UI:** WPF (Windows Presentation Foundation)
- **Language:** C# 10
- **Target:** Windows 10+ (x64)

### Key Components

| Component | Purpose |
|-----------|---------|
| `SyncService` | Core sync logic and file operations |
| `FileSystemWatcherService` | Real-time folder monitoring |
| `LogService` | Activity logging to files |
| `ConfigService` | JSON-based configuration persistence |
| `TaskSchedulerService` | Windows Task Scheduler integration |

### Dependencies

All dependencies are included in self-contained build:
- .NET 8.0 Runtime
- WindowsBase (WPF)
- System.Drawing (Icons)
- System.Windows.Forms (Folder browser dialog)

---

## Development

### Building from Source

```bash
# Debug build
dotnet build

# Run without publishing
dotnet run

# Run tests (if any)
dotnet test

# Clean build artifacts
dotnet clean
```

### Project Structure

```csharp
namespace SyncManager
{
    // Models - Data structures
    class SyncProfile { }
    class ScheduledTask { }
    class LogEntry { }
    class AppSettings { }

    // Services - Business logic
    class SyncService { }
    class FileSystemWatcherService { }
    class LogService { }
    class ConfigService { }

    // UI - Main window
    class MainWindow : Window { }
}
```

---

## Version History

### Version 1.0.1 (2026-01-06)
- Fixed window closing crash
- Added configurable backup folder location
- Fixed backup file naming to preserve extensions
- Default backup location: Documents\SyncManagerBackups

### Version 1.0.0 (2026-01-06)
- Initial release
- Manual sync functionality
- Auto-sync with FileSystemWatcher
- Scheduled sync via Task Scheduler
- Version-controlled backups
- SHA256 change detection
- WPF GUI with 5 tabs
- System tray integration
- Activity logging
- Dry-run mode

---

## Contributing

Contributions welcome! To contribute:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly on Windows
5. Submit a pull request

**Areas for contribution:**
- Additional sync modes (two-way sync, mirror)
- Network drive support improvements
- Conflict resolution UI
- File/folder filters in UI
- macOS/Linux support (Avalonia UI migration)
- Unit tests

---

## License

MIT License - Feel free to use, modify, and distribute.

---

**Last Updated:** January 6, 2026
**Version:** 1.0.1
