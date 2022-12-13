[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$Process,
  [string]$ArgumentList = "",
  [Parameter(Mandatory = $true)]
  [string]$Port
)

Start-Process $Process -PassThru -ArgumentList $ArgumentList
$timeout = 100
do
{
  Start-Sleep -Seconds 2
  $listeningHost = Get-NetTCPConnection -State Listen | Where-Object { $_.LocalAddress -eq "127.0.0.1" -and $_.LocalPort -eq $Port }
  if ($listeningHost.Count -gt 0)
  {
    Write-Host "Started $Process app on localhost:${Port}"
    exit(0)
  }
  else
  {
    Write-Host "$Process app not yet started"
  }
  $timeout -= 1
}
while(($timeout -gt 0) -and ($listeningHost.Count -eq 0))
exit(1)