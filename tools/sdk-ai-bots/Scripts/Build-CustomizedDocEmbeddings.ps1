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
        Invoke-WebRequest -Uri $url -OutFile $DestinationFilePath
        Write-Host "File downloaded successfully to: $DestinationFilePath"
    }
    catch {
        Write-Error "Failed to download file from GitHub: $url"
        exit 1
    }
}


$workingDirectory = Get-Location
if($env:AGENT_ID) {
  $workingDirectory = $(System.DefaultWorkingDirectory)
}

$scriptsRoot = Join-Path $workingDirectory "Scripts"
$embeddingToolFolder = Join-Path $workingDirectory "embeddings"

. (Join-Path $scriptsRoot common.ps1)

Write-Host "scriptsRoot: $scriptsRoot"
Write-Host "embeddingToolFolder: $embeddingToolFolder"

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
$storageAccountName = "saazuresdkbot"
$containerName = "rag-contents"
$blobName = "last_rag_chunks_customized_docs.json"
$destinationPath = $embeddingSourceFolder
$ragChunkPath = Join-Path -Path $embeddingSourceFolder -ChildPath $blobName
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
if(-not (Build-Embeddings -EmbeddingToolFolder $embeddingToolFolder)) {
  exit 1
}

# Upload embeddings output to Azure Blob Storage
Write-Host "Uploading embeddings output $ragChunkPath to Azure Blob Storage"
if(-not (Upload-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -SourceFile $ragChunkPath)) {
  exit 1
}