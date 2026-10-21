@echo off
setlocal
set "AZSDK_DEV_CLI_ROOT=%~dp0..\..\artifacts\bin\Azure.Sdk.Tools.Cli\"
powershell.exe -NoProfile -Command "$ErrorActionPreference = 'Stop'; $root = [IO.Path]::GetFullPath($env:AZSDK_DEV_CLI_ROOT); $processes = Get-CimInstance Win32_Process -Filter 'Name = ''azsdk.exe'''; foreach ($process in $processes) { if ($process.ExecutablePath -and $process.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and $process.CommandLine -match '(?i)^(?:\x22[^\x22]+\x22|\S+)\s+(?:(?:--debug(?:(?:[=:]|\s+)(?:\x22(?:true|false)\x22|true|false))?|(?:--output|-o)(?:[=:]|\s+)(?:\x22(?:plain|json|hidden)\x22|plain|json|hidden))\s+)*(?:mcp|start)(?:\s|$)') { [Console]::Error.WriteLine('Stopping repository MCP server PID ' + $process.ProcessId); $running = Get-Process -Id $process.ProcessId; Stop-Process -Id $process.ProcessId -Force; if (-not $running.WaitForExit(10000)) { throw 'MCP server did not exit within 10 seconds.' } } }"
if errorlevel 1 exit /b %errorlevel%
dotnet run --project "%~dp0Azure.Sdk.Tools.Cli" -- %*
exit /b %errorlevel%
