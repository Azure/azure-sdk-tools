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

function Test-AzCopyInstalled {
    try {
        $azcopyCommand = Get-Command azcopy -ErrorAction Stop
        if ($azcopyCommand) {
            return $true
        }
    }
    catch {
        Write-Error "AzCopy is not installed."
    }
    return $false
}

function Download-AzCopy {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $DestinationPath
    )

    try {
        $AzCopyUrl = "https://azcopyvnext.azureedge.net/release20220315/azcopy_windows_amd64_10.14.1.zip"
        $azCopyZip = Join-Path $DestinationPath "azcopy.zip"
        Invoke-WebRequest -Uri $AzCopyUrl -OutFile $azCopyZip
        Expand-Archive -Path $azCopyZip -DestinationPath $DestinationPath -Force
        return $true
    }
    catch {
        Write-Error "Failed to download AzCopy with exception:`n$_"
    }
    return $false
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
    try {
        $blobPath = "https://$StorageAccountName.blob.core.windows.net/$ContainerName/$BlobName"
        $destinationFile = Join-Path -Path $DestinationPath -ChildPath $BlobName
        $azcopyCmd = "azcopy copy $blobPath $destinationFile --recursive"
        if (-not (Test-AzCopyInstalled)) {
            if(Download-AzCopy (Get-Location).Path) {
                $azFilePath = (Get-ChildItem -Recurse |Where-object {$_.Name -eq 'azcopy.exe'} | Select-Object -First 1).FullName
                $azcopyCmd = "$azFilePath copy $blobPath $destinationFile --recursive"
            }
            else
            {
                return $false
            }
        }
        # If the following command stuck for a long time, it may be caused by the login need to be done manually.
        # You can run the azcopycmd manually.
        Write-Host "azcopyCmd: $azcopyCmd"
        $azcopyOutput = Invoke-Expression $azcopyCmd
        Write-Host "azcopyOutput: $azcopyOutput"
        if(Test-Path $destinationFile) {
            Write-Host "$destinationFile downloaded successfully."
        }
        else {
            Write-Error "$destinationFile failed to download."
            return $false
        }
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
        [string] $EmbeddingToolFolder
    )

    if(-not (Test-Path $embeddingToolFolder)){
        Write-Error "The embedding tool folder does not exist: $embeddingToolFolder"
        return $false
    }
    $stopwatch = Measure-Command {
        Write-Host "Building embeddings..."
        try {
            Push-Location $embeddingToolFolder

            # Print Python version
            $pythonVersion = python -c "import sys; print(sys.version)"
            Write-Host "Python version: $pythonVersion"
            # Print Python executable path
            $pythonEnvExePath = python -c "import sys; print(sys.executable)"
            Write-Host "Python executable path: $pythonEnvExePath"

            # setup python environment and install required packages
            Write-Host "Setting up python environment"
            python -m pip install --upgrade pip

            Write-Host "Installing required packages"
            python -m pip install -r requirements.txt

            Write-Host "List package versions..."
            python -m pip list > pip_list.txt

            Write-Host "Print the content of pip_list.txt"
            $installedPkg = Get-Content -Path "pip_list.txt"
            Write-Host $installedPkg

            Write-Host "Starts building"
            python main.py
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
    
    try {
        $blobPath = "https://$StorageAccountName.blob.core.windows.net/$ContainerName/$BlobName"
        $azcopyCmd = "azcopy copy $SourceFile $blobPath"
        if (-not (Test-AzCopyInstalled)) {
            if(Download-AzCopy (Get-Location).Path) {
                $azFilePath = (Get-ChildItem -Recurse |Where-object {$_.Name -eq 'azcopy.exe'} | Select-Object -First 1).FullName
                $azcopyCmd = "$azFilePath copy $SourceFile $blobPath"
            }
            else {
                return $false
            }
        }
        Write-Host "azcopyCmd: $azcopyCmd"
        $azcopyOutput = Invoke-Expression $azcopyCmd
        Write-Host "azcopyOutput: $azcopyOutput"
        return $true
    }
    catch {
        Write-Error "Failed to upload Azure blob: $BlobName with exception:`n$_"
    }
    return $false
}
