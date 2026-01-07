@echo off
REM Sync Manager - Batch File Wrapper
REM This batch file runs the PowerShell sync script with proper execution policy handling

setlocal enabledelayedexpansion

REM Get the directory where this batch file is located
set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%Sync-Manager.ps1"

REM Check if PS script exists
if not exist "!PS_SCRIPT!" (
    echo Error: Sync-Manager.ps1 not found in %SCRIPT_DIR%
    pause
    exit /b 1
)

REM Parse command line arguments
set "SOURCE=%~1"
set "DESTINATION=%~2"
set "BACKUP_VERSIONS=%~3"
set "DRY_RUN=%~4"

REM Validate inputs
if "!SOURCE!"=="" (
    echo.
    echo Usage: sync.bat "C:\SourceFolder" "D:\DestinationFolder" [BackupVersions] [--dryrun]
    echo.
    echo Examples:
    echo   sync.bat "C:\MyFolder" "D:\Backup" 3
    echo   sync.bat "C:\MyFolder" "D:\Backup" 5 --dryrun
    echo.
    pause
    exit /b 1
)

if "!DESTINATION!"=="" (
    echo Error: Destination folder is required
    pause
    exit /b 1
)

REM Set defaults
if "!BACKUP_VERSIONS!"=="" set "BACKUP_VERSIONS=3"

REM Build PowerShell command
set "PS_CMD=powershell.exe -NoProfile -ExecutionPolicy Bypass -File "!PS_SCRIPT!" -SourceFolder "!SOURCE!" -DestinationFolder "!DESTINATION!" -BackupVersions !BACKUP_VERSIONS!"

REM Add dry-run flag if specified
if /i "!DRY_RUN!"=="--dryrun" (
    set "PS_CMD=!PS_CMD! -DryRun"
    echo.
    echo Running in DRY RUN mode (no changes will be made)
    echo.
)

REM Execute PowerShell script
!PS_CMD!

REM Keep window open to see results
pause
