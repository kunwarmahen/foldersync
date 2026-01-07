# Setup-SyncTask.ps1
# This script creates a Windows Task Scheduler task for automated folder syncing

param(
    [Parameter(Mandatory=$true)]
    [string]$TaskName = "FolderSync",
    
    [Parameter(Mandatory=$true)]
    [string]$SourceFolder,
    
    [Parameter(Mandatory=$true)]
    [string]$DestinationFolder,
    
    [Parameter(Mandatory=$false)]
    [int]$BackupVersions = 3,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Minute", "FiveMinutes", "TenMinutes", "FifteenMinutes", "ThirtyMinutes", "Hourly", "Daily")]
    [string]$Frequency = "FiveMinutes",
    
    [Parameter(Mandatory=$false)]
    [string]$ScriptPath = "$PSScriptRoot\Sync-Manager.ps1"
)

# Verify script exists
if (-not (Test-Path $ScriptPath)) {
    Write-Host "Error: Sync-Manager.ps1 not found at $ScriptPath" -ForegroundColor Red
    exit 1
}

# Check if running as administrator
$isAdmin = ([System.Security.Principal.WindowsPrincipal] [System.Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Error: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Task Scheduler Setup for Folder Sync" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Task Name: $TaskName" -ForegroundColor Green
Write-Host "Source: $SourceFolder" -ForegroundColor Green
Write-Host "Destination: $DestinationFolder" -ForegroundColor Green
Write-Host "Backup Versions: $BackupVersions" -ForegroundColor Green
Write-Host "Frequency: $Frequency" -ForegroundColor Green
Write-Host ""

# Check if task already exists
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Task '$TaskName' already exists. Unregistering old task..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Start-Sleep -Seconds 2
}

# Create task action
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -SourceFolder `"$SourceFolder`" -DestinationFolder `"$DestinationFolder`" -BackupVersions $BackupVersions"

# Create trigger based on frequency
$trigger = switch ($Frequency) {
    "Minute"          { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days 365) }
    "FiveMinutes"     { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 365) }
    "TenMinutes"      { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 10) -RepetitionDuration (New-TimeSpan -Days 365) }
    "FifteenMinutes"  { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration (New-TimeSpan -Days 365) }
    "ThirtyMinutes"   { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 30) -RepetitionDuration (New-TimeSpan -Days 365) }
    "Hourly"          { New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 1) -RepetitionDuration (New-TimeSpan -Days 365) }
    "Daily"           { New-ScheduledTaskTrigger -Daily -At "02:00 AM" }
}

# Create task settings
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -DontStopOnIdleEnd `
    -RunOnlyIfNetworkAvailable

# Register the task
try {
    Register-ScheduledTask -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Description "Automatically syncs $SourceFolder to $DestinationFolder with backup versioning" `
        -RunLevel Highest | Out-Null
    
    Write-Host "✓ Task created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Task Details:" -ForegroundColor Cyan
    Write-Host "  Name: $TaskName" -ForegroundColor White
    Write-Host "  Location: \Microsoft\Windows\PowerShell\ScheduledJobs\" -ForegroundColor White
    Write-Host "  Status: Enabled" -ForegroundColor White
    Write-Host "  Frequency: $Frequency" -ForegroundColor White
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. The task will run automatically according to the schedule" -ForegroundColor White
    Write-Host "  2. Check Task Scheduler to view task properties or change frequency" -ForegroundColor White
    Write-Host "  3. Logs are stored in: $(Get-Item $ScriptPath | Split-Path)\sync-logs\" -ForegroundColor White
    Write-Host ""
    
    # Optionally run the task immediately
    $response = Read-Host "Run the sync task immediately? (y/n)"
    if ($response -eq 'y') {
        Write-Host "Starting sync task..." -ForegroundColor Yellow
        Start-ScheduledTask -TaskName $TaskName
        Start-Sleep -Seconds 2
        Write-Host "Task started. Check logs for results." -ForegroundColor Green
    }
}
catch {
    Write-Host "Error creating task: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
