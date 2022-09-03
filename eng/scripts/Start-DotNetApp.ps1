[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$ArgumentList,
  [Parameter(Mandatory = $true)]
  [string]$Port
)

Start-Process dotnet -PassThru -ArgumentList $ArgumentList
$timeout = 10
do
{
    Start-Sleep -Seconds 2
    $listeningHost = Get-NetTCPConnection -State Listen | Where-Object { $_.LocalAddress -eq "127.0.0.1" -and $_.LocalPort -eq $Port }
    if ($listeningHost.Count -gt 0)
    {
    Write-Host "Started APIView on localhost:${Port}"
    }
    else
    {
    Write-Host "APIView not yet started"
    }
    $timeout -= 2
}
while(($timeout -eq 0) -or ($listeningHost.Count -eq 0))