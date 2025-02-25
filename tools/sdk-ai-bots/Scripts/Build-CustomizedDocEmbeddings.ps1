<#
.SYNOPSIS
This code build the embeddings for the customized document set which are public accessible markdowns.

.DESCRIPTION
This code is responsible for refreshing the embeddings for the customized document set.

.PARAMETER IncrementalEmbedding
Control the incremental building behavior for the embeddings.

.EXAMPLE
Build-CustomizedDocEmbeddings.ps1 -IncrementalEmbedding $true
#>
[CmdletBinding()]
param (
  [Parameter(Position = 0)]
  [ValidateNotNullOrEmpty()]
  [string] $IncrementalEmbedding = $true
)

function Load-CustomizedDocsMetadata {
  param (
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string] $JsonFilePath
  )

  $jsonContent = $null
  try {
    $jsonContent = Get-Content -Path $JsonFilePath -Raw | ConvertFrom-Json
  }
  catch {
    Write-Error "Failed to read or convert JSON content from file: $JsonFilePath"
    return $null
  }
  return $jsonContent
}

function Download-GitHubFile {
    param (
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $FileUrl,

        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string] $DestinationFilePath
    )

    if($FileUrl -match "https://github.com/.*") {
      $FileUrl = $FileUrl -replace "https://github.com/(.*)/(tree|blob)", "https://raw.githubusercontent.com/`$1"
    }

    try {
        Invoke-WebRequest -Uri $FileUrl -OutFile $DestinationFilePath
        Write-Host "File downloaded successfully to: $DestinationFilePath"
    }
    catch {
        Write-Error "Failed to download file from GitHub: $FileUrl"
        exit 1
    }
}


# Set the working directory, current location is supposed to be the root of the repository
$buildSourceDirectory = Get-Location
$workingDirectory = Join-Path $buildSourceDirectory "tools\sdk-ai-bots"
$scriptsRoot = Join-Path $workingDirectory "Scripts"
$embeddingToolFolder = Join-Path $workingDirectory "Embeddings"

Write-Host "scriptsRoot: $scriptsRoot"
Write-Host "embeddingToolFolder: $embeddingToolFolder"
. (Join-Path $scriptsRoot Common.ps1)

# Create embeddingSource folder on current location
$embeddingSourceFolder = Join-Path -Path $workingDirectory -ChildPath "embeddingSource"
if (-not (Test-Path -Path $embeddingSourceFolder)) {
  New-Item -ItemType Directory -Path $embeddingSourceFolder
}

# Create folder to save the customized documents
$customizedDocsDestFolder = Join-Path -Path $embeddingSourceFolder -ChildPath "customized-docs"
if (-not (Test-Path -Path $customizedDocsDestFolder)) {
  New-Item -ItemType Directory -Path $customizedDocsDestFolder
}

# Load metadata_customized_docs.json file and download the customized documents
$customizedDocsMetadataFile = Join-Path -Path $embeddingToolFolder -ChildPath "settings/metadata_customized_docs.json"
if(Test-Path $customizedDocsMetadataFile) {
  $customizedDocsMetadata = Load-CustomizedDocsMetadata -JsonFilePath $customizedDocsMetadataFile
  if(-not $customizedDocsMetadata) {
    exit 1
  }
  foreach ($key in $customizedDocsMetadata.PSObject.Properties.Name) {
    $url = $customizedDocsMetadata.$key.url
    Write-Debug "URL for $key, $url"
    Download-GitHubFile -FileUrl $url -DestinationFilePath "$customizedDocsDestFolder/$key"
  }
}
else {
  Write-Error "Failed to find metadata_customized_docs.json file at: $customizedDocsMetadataFile."
  exit 1
}

# Download previous saved embeddings(last_rag_chunks_customized_docs.json) from Azure Blob Storage
# Using Azure PowerShell login type for AzCopy.
# When running this script locally, first using 'Connect-AzAccount' then 'Set-AzContext' to switch to the correct subscription
$env:AZCOPY_AUTO_LOGIN_TYPE="PSCRED"
$blobName = "last_rag_chunks_customized_docs.json"
$destinationPath = $embeddingSourceFolder
$ragChunkPath = Join-Path -Path $embeddingSourceFolder -ChildPath $blobName
$storageAccountName = $env:AZURE_STORAGE_ACCOUNT_NAME
$containerName = $env:AZURE_STORAGE_ACCOUNT_CONTAINER
if(-not $containerName) {
  Write-Error "Please set the environment variable 'AZURE_STORAGE_ACCOUNT_CONTAINER'."
  exit 1
}
if($IncrementalEmbedding -eq $true) {
  Write-Host "Downloading previous saved embeddings $blobName from Azure Blob Storage"
  if(-not (Download-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -DestinationPath $destinationPath)) {
    exit 1
  }
}

# Build embeddings
Write-Host "Building embeddings for customized documents"
$env:RAG_CHUNK_PATH = $ragChunkPath
$env:METADATA_PATH = $customizedDocsMetadataFile
$env:DOCUMENT_PATH = $customizedDocsDestFolder
$env:INCREMENTAL_EMBEDDING = $IncrementalEmbedding
$env:AZURESEARCH_FIELDS_CONTENT = "Text"
$env:AZURESEARCH_FIELDS_CONTENT_VECTOR = "Embedding"
$env:AZURESEARCH_FIELDS_TAG = "AdditionalMetadata"
$env:AZURESEARCH_FIELDS_ID = "Id"

if(-not (Build-Embeddings -EmbeddingToolFolder $embeddingToolFolder)) {
  exit 1
}

# Upload embeddings output to Azure Blob Storage
Write-Host "Uploading embeddings output $ragChunkPath to Azure Blob Storage"
if(-not (Upload-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -SourceFile $ragChunkPath)) {
  exit 1
}