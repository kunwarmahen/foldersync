# Sync Manager - Windows Folder Sync with Automated Backup Versioning
# This script syncs a source folder to a destination folder and maintains version history

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFolder,
    
    [Parameter(Mandatory=$true)]
    [string]$DestinationFolder,
    
    [Parameter(Mandatory=$false)]
    [int]$BackupVersions = 3,
    
    [Parameter(Mandatory=$false)]
    [string]$LogPath = "$PSScriptRoot\sync-logs",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

# ============================================================================
# CONFIGURATION
# ============================================================================

$ErrorActionPreference = "Continue"
$BackupSubFolder = "\.backups"
$MetadataFile = "$DestinationFolder\.sync-metadata.json"
$ExclusionPatterns = @(
    "\.sync-metadata\.json",
    "\.backups",
    "System Volume Information",
    "Thumbs\.db",
    "~*",
    "\.tmp"
)

# ============================================================================
# LOGGING SETUP
# ============================================================================

function Initialize-Logging {
    if (-not (Test-Path $LogPath)) {
        New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
    }
    
    $script:LogFile = Join-Path $LogPath "sync-$(Get-Date -Format 'yyyyMMdd').log"
    $script:ErrorLogFile = Join-Path $LogPath "sync-errors-$(Get-Date -Format 'yyyyMMdd').log"
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("INFO", "WARN", "ERROR", "SUCCESS")]
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    Write-Host $logMessage
    Add-Content -Path $script:LogFile -Value $logMessage
    
    if ($Level -eq "ERROR") {
        Add-Content -Path $script:ErrorLogFile -Value $logMessage
    }
}

# ============================================================================
# VALIDATION
# ============================================================================

function Test-Prerequisites {
    Write-Log "Validating prerequisites..." "INFO"
    
    # Check if source folder exists
    if (-not (Test-Path $SourceFolder -PathType Container)) {
        Write-Log "Source folder does not exist: $SourceFolder" "ERROR"
        exit 1
    }
    
    # Check if destination folder exists, create if not
    if (-not (Test-Path $DestinationFolder -PathType Container)) {
        Write-Log "Destination folder does not exist. Creating: $DestinationFolder" "INFO"
        New-Item -ItemType Directory -Path $DestinationFolder -Force | Out-Null
    }
    
    # Verify write access to destination
    $testFile = Join-Path $DestinationFolder ".write-test-$(Get-Random)"
    try {
        "test" | Out-File -FilePath $testFile -Force
        Remove-Item $testFile -Force
        Write-Log "Write access verified on destination folder" "INFO"
    }
    catch {
        Write-Log "No write access to destination folder: $_" "ERROR"
        exit 1
    }
    
    Write-Log "Prerequisites validation passed" "SUCCESS"
}

# ============================================================================
# METADATA MANAGEMENT
# ============================================================================

function Initialize-Metadata {
    if (-not (Test-Path $MetadataFile)) {
        $metadata = @{
            createdAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            lastSync = $null
            fileVersions = @{}
        }
        
        $metadata | ConvertTo-Json | Out-File -FilePath $MetadataFile -Force
        Write-Log "Metadata file created: $MetadataFile" "INFO"
    }
}

function Get-Metadata {
    if (Test-Path $MetadataFile) {
        return Get-Content $MetadataFile | ConvertFrom-Json
    }
    return $null
}

function Update-Metadata {
    param([object]$Metadata)
    
    $Metadata.lastSync = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Metadata | ConvertTo-Json -Depth 10 | Out-File -FilePath $MetadataFile -Force
}

# ============================================================================
# BACKUP MANAGEMENT
# ============================================================================

function Get-BackupFolder {
    param([string]$DestPath)
    return Join-Path $DestPath $BackupSubFolder
}

function Initialize-BackupFolder {
    $backupFolder = Get-BackupFolder $DestinationFolder
    
    if (-not (Test-Path $backupFolder)) {
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        Write-Log "Backup folder created: $backupFolder" "INFO"
    }
}

function Backup-File {
    param(
        [string]$FilePath,
        [string]$FileName
    )
    
    $backupFolder = Get-BackupFolder $DestinationFolder
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    
    # Create subfolder for each file in backups
    $fileBackupFolder = Join-Path $backupFolder $FileName
    if (-not (Test-Path $fileBackupFolder)) {
        New-Item -ItemType Directory -Path $fileBackupFolder -Force | Out-Null
    }
    
    # Create backup with timestamp
    $backupFileName = "$FileName`_$timestamp.bak"
    $backupPath = Join-Path $fileBackupFolder $backupFileName
    
    try {
        Copy-Item -Path $FilePath -Destination $backupPath -Force
        Write-Log "Backup created: $backupPath" "INFO"
        return $backupPath
    }
    catch {
        Write-Log "Failed to create backup for $FileName : $_" "ERROR"
        return $null
    }
}

function Rotate-Backups {
    param(
        [string]$FileName
    )
    
    $backupFolder = Get-BackupFolder $DestinationFolder
    $fileBackupFolder = Join-Path $backupFolder $FileName
    
    if (-not (Test-Path $fileBackupFolder)) {
        return
    }
    
    # Get all backup files, sorted by creation time (newest first)
    $backups = Get-ChildItem -Path $fileBackupFolder -Filter "$FileName`_*.bak" | 
               Sort-Object -Property CreationTime -Descending
    
    # Delete backups beyond the retention count
    if ($backups.Count -gt $BackupVersions) {
        $backupsToDelete = $backups | Select-Object -Skip $BackupVersions
        
        foreach ($backup in $backupsToDelete) {
            try {
                Remove-Item -Path $backup.FullName -Force
                Write-Log "Deleted old backup: $($backup.Name)" "INFO"
            }
            catch {
                Write-Log "Failed to delete backup $($backup.Name): $_" "ERROR"
            }
        }
    }
}

# ============================================================================
# FILE COMPARISON
# ============================================================================

function Get-FileHash-Safe {
    param([string]$FilePath)
    
    try {
        # Use SHA256 for reliable comparison
        $hash = (Get-FileHash -Path $FilePath -Algorithm SHA256 -ErrorAction Stop).Hash
        return $hash
    }
    catch {
        Write-Log "Could not compute hash for $FilePath : $_" "WARN"
        return $null
    }
}

function Has-FileChanged {
    param(
        [string]$SourceFile,
        [string]$DestFile,
        [object]$Metadata
    )
    
    # If destination doesn't exist, it's a new file
    if (-not (Test-Path $DestFile)) {
        return $true
    }
    
    # Compare file sizes first (faster)
    $sourceSize = (Get-Item $SourceFile).Length
    $destSize = (Get-Item $DestFile).Length
    
    if ($sourceSize -ne $destSize) {
        return $true
    }
    
    # If sizes match, compare hashes
    $sourceHash = Get-FileHash-Safe $SourceFile
    $destHash = Get-FileHash-Safe $DestFile
    
    if ($sourceHash -and $destHash) {
        return $sourceHash -ne $destHash
    }
    
    # If hash comparison fails, assume changed to be safe
    return $true
}

# ============================================================================
# SYNC LOGIC
# ============================================================================

function Sync-Folders {
    Write-Log "========================================" "INFO"
    Write-Log "Starting sync operation" "INFO"
    Write-Log "Source: $SourceFolder" "INFO"
    Write-Log "Destination: $DestinationFolder" "INFO"
    Write-Log "Backup versions to keep: $BackupVersions" "INFO"
    if ($DryRun) { Write-Log "DRY RUN MODE - No changes will be made" "WARN" }
    Write-Log "========================================" "INFO"
    
    $metadata = Get-Metadata
    $filesProcessed = 0
    $filesBackedUp = 0
    $filesSkipped = 0
    $filesFailed = 0
    
    # Get all files from source recursively
    $sourceFiles = Get-ChildItem -Path $SourceFolder -Recurse -File -ErrorAction Continue
    
    foreach ($sourceFile in $sourceFiles) {
        # Calculate relative path
        $relativePath = $sourceFile.FullName.Substring($SourceFolder.Length).TrimStart('\')
        $destFile = Join-Path $DestinationFolder $relativePath
        $destDir = Split-Path -Parent $destFile
        
        # Check exclusions
        $isExcluded = $false
        foreach ($pattern in $ExclusionPatterns) {
            if ($relativePath -match $pattern) {
                $isExcluded = $true
                break
            }
        }
        
        if ($isExcluded) {
            $filesSkipped++
            continue
        }
        
        try {
            # Create destination directory if it doesn't exist
            if (-not (Test-Path $destDir)) {
                if (-not $DryRun) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }
                Write-Log "Created directory: $destDir" "INFO"
            }
            
            # Check if file has changed
            if (Has-FileChanged -SourceFile $sourceFile.FullName -DestFile $destFile -Metadata $metadata) {
                Write-Log "File changed/new: $relativePath" "INFO"
                
                if (-not $DryRun) {
                    # Create backup if destination file exists
                    if (Test-Path $destFile) {
                        Backup-File -FilePath $destFile -FileName (Split-Path -Leaf $destFile)
                        $filesBackedUp++
                    }
                    
                    # Copy new version
                    Copy-Item -Path $sourceFile.FullName -Destination $destFile -Force
                    
                    # Rotate old backups
                    Rotate-Backups -FileName (Split-Path -Leaf $destFile)
                    
                    Write-Log "Synced: $relativePath" "SUCCESS"
                }
                else {
                    Write-Log "[DRY RUN] Would sync: $relativePath" "INFO"
                }
                
                $filesProcessed++
            }
            else {
                $filesSkipped++
            }
        }
        catch {
            Write-Log "Error syncing $relativePath : $_" "ERROR"
            $filesFailed++
        }
    }
    
    # Handle deletions (optional - remove if you want to keep files)
    $destFiles = Get-ChildItem -Path $DestinationFolder -Recurse -File -ErrorAction Continue | 
                 Where-Object { $_.FullName -notmatch '\.backups' -and $_.Name -ne '.sync-metadata.json' }
    
    foreach ($destFile in $destFiles) {
        $relativePath = $destFile.FullName.Substring($DestinationFolder.Length).TrimStart('\')
        $sourceFile = Join-Path $SourceFolder $relativePath
        
        if (-not (Test-Path $sourceFile)) {
            Write-Log "File deleted from source: $relativePath (keeping in destination for safety)" "WARN"
        }
    }
    
    # Update metadata
    if (-not $DryRun) {
        Update-Metadata -Metadata $metadata
    }
    
    Write-Log "========================================" "INFO"
    Write-Log "Sync complete!" "SUCCESS"
    Write-Log "Files processed: $filesProcessed" "INFO"
    Write-Log "Files backed up: $filesBackedUp" "INFO"
    Write-Log "Files skipped (unchanged): $filesSkipped" "INFO"
    Write-Log "Files failed: $filesFailed" "INFO"
    Write-Log "========================================" "INFO"
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

function Main {
    Initialize-Logging
    Initialize-Metadata
    Initialize-BackupFolder
    Test-Prerequisites
    Sync-Folders
}

# Run the sync
Main
