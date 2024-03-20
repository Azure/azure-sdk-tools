<#
.DESCRIPTION
This script contains some common functions.
#>
# Clone document source repository
function Clone-Repository {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $RepoUrl,
        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string] $RootFolder
    )
    try {
        if(-not (Test-Path $RootFolder)) {
            New-Item -ItemType Directory -Path $RootFolder            
        }
        
        Push-Location $RootFolder
        # Clone repository
        git clone $RepoUrl
    }
    catch {
        Write-Error "Failed to clone repository: {$RepoUrl} with exception:`n$_ "
        return $false
    }
    finally {
        Pop-Location
    }
    return $true
}

function Download-AzureBlob {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $StorageAccountName,
        
        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string] $ContainerName,
        
        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty()]
        [string] $BlobName,
        
        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty()]
        [string] $DestinationPath
    )
    
    $storageAccountKey = $env:AZURE_STORAGE_ACCOUNT_KEY
    if (-not $storageAccountKey) {
        Write-Error "Please set the environment variable 'AZURE_STORAGE_ACCOUNT_KEY'."
        return $false
    }
    try {
        $context = New-AzStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $storageAccountKey
        
        $blob = Get-AzStorageBlob -Context $context -Container $ContainerName -Blob $BlobName
        
        $destinationFile = Join-Path -Path $DestinationPath -ChildPath $BlobName
        
        $blob | Get-AzStorageBlobContent -Destination $destinationFile -Force
        return $true
    }
    catch {
        Write-Error "Failed to download Azure blob: $BlobName with exception:`n$_"
    }
    return $false
}

function Build-Embeddings {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $EmbeddingToolFolder,
        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string] $CondaPath
    )

    if(-not (Test-Path $embeddingToolFolder)){
        Write-Error "The embedding tool folder does not exist: $embeddingToolFolder"
        return $false
    }
    
    $stopwatch = Measure-Command {
        Write-Host "Building embeddings..."
        try {
            Push-Location $embeddingToolFolder            
            
            Write-Host "Create Conda environment"
            & $CondaPath create -n myenv python=3.11 -y

            # Print Python version
            $pythonVersion = & $CondaPath run -n myenv python -c "import sys; print(sys.version)"
            Write-Host "Python version: $pythonVersion"
            # Print Python executable path
            $pythonEnvExePath = & $CondaPath run -n myenv python -c "import sys; print(sys.executable)"
            Write-Host "Python executable path: $pythonEnvExePath"

            # setup python environment and install required packages
            Write-Host "Setting up python environment"
            & $CondaPath run -n myenv python -m pip install --upgrade pip

            Write-Host "Installing required packages"
            & $CondaPath run -n myenv python -m pip install -r requirements.txt

            Write-Host "List package versions..."
            & $CondaPath run -n myenv python -m pip list > pip_list.txt

            Write-Host "Print the content of pip_list.txt"
            $installedPkg = Get-Content -Path "pip_list.txt"
            Write-Host $installedPkg
            
            Write-Host "Starts building"
            & $CondaPath run -n myenv python main.py
        }
        catch {
            Write-Error "Failed to build embeddings with exception:`n$_"
            return $false
        }
        finally {
            Pop-Location
        }    
    }
    Write-Host "Finishes building with time: $($stopwatch.TotalSeconds) seconds"
    return $true
}

function Upload-AzureBlob {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $StorageAccountName,
        
        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string] $ContainerName,
        
        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty()]
        [string] $BlobName,
        
        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty()]
        [string] $SourceFile
    )
    
    $storageAccountKey = $env:AZURE_STORAGE_ACCOUNT_KEY
    if (-not $storageAccountKey) {
        Write-Error "Please set the environment variable 'AZURE_STORAGE_ACCOUNT_KEY'."
        return $false
    }
    try {
        $context = New-AzStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $storageAccountKey
        
        $blob = Set-AzStorageBlobContent -Context $context -Container $ContainerName -Blob $BlobName -File $SourceFile -Force
        return $true
    }
    catch {
        Write-Error "Failed to upload Azure blob: $BlobName with exception:`n$_"
    }
    return $false
}

# only support windows platform
function Initialize-CondaEnv {
    $condaPath = ""
    try {
        Get-Command conda -ErrorAction Stop >$null
        Write-Host "Conda is installed."
        $condaPath = (Get-Command conda -All | Where-Object { $_.CommandType -eq 'Application' -and $_.Source -like '*conda.exe' }).Source
        Write-Host "Conda path: $condaPath"
    } catch {
        Write-Host "Conda is not installed."
        Write-Host "Installing Miniconda"
        Invoke-WebRequest -Uri "https://repo.anaconda.com/miniconda/Miniconda3-latest-Windows-x86_64.exe" -OutFile "miniconda.exe"
        Start-Process "miniconda.exe" -ArgumentList "/S /D=C:\Miniconda" -Wait
        $condaPath = "C:\Miniconda\Scripts\conda.exe"
        Write-Host "Conda path: $condaPath"
    }
    
    return $condaPath
}
