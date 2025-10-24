@echo off
REM TSP-Client Validation Automation - Windows Batch Wrapper
REM This provides a simple interface to the PowerShell automation script

setlocal enabledelayedexpansion

echo TSP-Client Validation Automation
echo ==================================

REM Check if PowerShell is available
pwsh -version >nul 2>&1
if errorlevel 1 (
    echo Error: PowerShell Core (pwsh) is required but not found.
    echo Please install PowerShell Core from: https://github.com/PowerShell/PowerShell
    pause
    exit /b 1
)

REM Check if the PowerShell script exists
if not exist "Invoke-TspClientValidation.ps1" (
    echo Error: Invoke-TspClientValidation.ps1 not found in current directory.
    echo Please run this from the tools/tsp-client-validation directory.
    pause
    exit /b 1
)

REM Get user input
echo.
echo Please provide the following information:
echo.

set /p PR_NUMBER="Enter PR number (e.g., 12360): "
if "%PR_NUMBER%"=="" (
    echo Error: PR number is required.
    pause
    exit /b 1
)

set /p SYNC_BRANCH="Enter sync branch name (e.g., sync-eng/common-update-tsp-client-%PR_NUMBER%): "
if "%SYNC_BRANCH%"=="" (
    set SYNC_BRANCH=sync-eng/common-update-tsp-client-%PR_NUMBER%
    echo Using default sync branch: !SYNC_BRANCH!
)

echo.
echo Available languages: Python, NET, Java, JS, Go
set /p LANGUAGES="Enter languages to validate (comma-separated, or press Enter for all): "

echo.
set /p DRY_RUN="Run in dry-run mode? (y/N): "

echo.
echo Configuration:
echo   PR Number: %PR_NUMBER%
echo   Sync Branch: %SYNC_BRANCH%
echo   Languages: %LANGUAGES%
echo   Dry Run: %DRY_RUN%
echo.

set /p CONFIRM="Continue? (Y/n): "
if /i "%CONFIRM%"=="n" (
    echo Operation cancelled.
    pause
    exit /b 0
)

REM Build PowerShell command
set PS_COMMAND=.\Invoke-TspClientValidation.ps1 -PRNumber %PR_NUMBER% -SyncBranch "%SYNC_BRANCH%"

if not "%LANGUAGES%"=="" (
    REM Convert comma-separated list to PowerShell array format
    set LANGUAGES=!LANGUAGES: =!
    set LANGUAGES=!LANGUAGES:,=','!
    set PS_COMMAND=!PS_COMMAND! -Languages @('!LANGUAGES!')
)

if /i "%DRY_RUN%"=="y" (
    set PS_COMMAND=!PS_COMMAND! -DryRun
)

echo.
echo Executing: pwsh -Command "!PS_COMMAND!"
echo.

REM Execute PowerShell script
pwsh -Command "!PS_COMMAND!"

echo.
echo Batch script completed.
pause