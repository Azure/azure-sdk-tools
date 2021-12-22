function Get-CodeOwnersTool($toolPath, $toolName, $toolVersion, $toolCommandName, $feedUrl)
{
  $command = Join-Path $toolPath $toolCommandName
  # Check if the dotnet tool exsits or not.
  if (Get-Command $command -errorAction SilentlyContinue) {
    return $command
  }
  if (!(Test-Path $toolPath)) {
    New-Item -ItemType Directory -Path $toolPath | Out-Null
  }
  Write-Host "Installing the $toolName tool under $toolPath... "

  # Run command under tool path to avoid dotnet tool install command checking .csproj files. 
  # This is a bug for dotnet tool command. Issue: https://github.com/dotnet/sdk/issues/9623
  Push-Location $toolPath
  dotnet tool install --tool-path $toolPath --add-source $feedUrl --version $toolVersion $toolName | Out-Null
  Pop-Location
  # Test to see if the tool properly installed.
  if (!(Get-Command $command -errorAction SilentlyContinue)) {
    Write-Error "The $toolName tool is not properly installed. Please check your tool path. $toolPath"
    return 
  }
  return $command
}