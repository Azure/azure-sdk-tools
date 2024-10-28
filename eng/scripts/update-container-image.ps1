param(
    [Parameter(Mandatory=$true)]
    [string]$Image,
    [Parameter(Mandatory=$true)]
    [string]$Mirror,
    [string]$Changes,
    [switch]$RegistryLogin
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($RegistryLogin -and $Mirror) {
    $mirrorRegistry = $Mirror.Split('.')[0]
    Write-Host "Logging in to $mirrorRegistry"
    az acr login -n $mirrorRegistry
}

Write-Host "docker pull $Image"
docker pull $Image

$target = $Image
if ($Mirror) {
    Write-Host "docker tag $Image $Mirror"
    docker tag $Image $Mirror
    $target = $Mirror
}

if ($Changes) {
    $cmd = "docker run $target $Changes"
    Write-Host $cmd
    Invoke-Expression $cmd
    $output = docker ps -al --format json | ConvertFrom-Json
    Write-Host "docker commit $($output.ID) $target"
    docker commit $output.ID $target
}

Write-Host "docker push $target"
docker push $target
