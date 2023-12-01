param(
    [Parameter(Mandatory=$true)]
    [string]$Image,
    [Parameter(Mandatory=$true)]
    [string]$Mirror,
    [switch]$RegistryLogin
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($RegistryLogin) {
    $mirrorRegistry = $Mirror.Split('.')[0]
    Write-Host "Logging in to $mirrorRegistry"
    az acr login -n $mirrorRegistry
}

Write-Host "docker pull $Image"
docker pull $Image
Write-Host "docker tag $Image $Mirror"
docker tag $Image $Mirror
Write-Host "docker push $Mirror"
docker push $Mirror
