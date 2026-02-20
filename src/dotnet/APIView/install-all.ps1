# Install and build APIViewWeb\Client
Write-Host "Installing and building APIViewWeb\Client..."
Push-Location "APIViewWeb\Client"
npm install
npm run-script build
Pop-Location

# Install and build ClientSPA
Write-Host "Installing and building ClientSPA..."
Push-Location "ClientSPA"
npm install
npm run-script build
Pop-Location