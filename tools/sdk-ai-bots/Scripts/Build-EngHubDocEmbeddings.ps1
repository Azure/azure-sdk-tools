<#
.SYNOPSIS
This code build the embeddings for the document under https://eng.ms/docs/products/azure-developer-experience.

.DESCRIPTION
This code is responsible for refreshing the embeddings for the document in engineering hub site.

.PARAMETER IncrementalEmbedding
Control the incremental building behavior for the embeddings.

.EXAMPLE
Build-EngHubDocEmbeddings.ps1 -IncrementalEmbedding $true
#>
[CmdletBinding()]
param (
  [Parameter(Position = 0)]
  [ValidateNotNullOrEmpty()]
  [string] $IncrementalEmbedding = $true
)

$workingDirectory = Join-Path (Get-Location) "tools\sdk-ai-bots"
$scriptsRoot = Join-Path $workingDirectory "Scripts"
$embeddingToolFolder = Join-Path $workingDirectory "Embeddings"

Write-Host "scriptsRoot: $scriptsRoot"
Write-Host "embeddingToolFolder: $embeddingToolFolder"
. (Join-Path $scriptsRoot Common.ps1)

# Create 'repos' folder on current location
$reposFolder = Join-Path -Path $workingDirectory -ChildPath "repos"
if (-not (Test-Path -Path $reposFolder)) {
  New-Item -ItemType Directory -Path $reposFolder
}

# Create embeddingSource folder on current location
$embeddingSourceFolder = Join-Path -Path $workingDirectory -ChildPath "embeddingSource"
if (-not (Test-Path -Path $embeddingSourceFolder)) {
  New-Item -ItemType Directory -Path $embeddingSourceFolder
}

# Create folder to save the enghub documents
$enghubDocsDestFolder = Join-Path -Path $embeddingSourceFolder -ChildPath "enghub-docs"
if (-not (Test-Path -Path $enghubDocsDestFolder)) {
  New-Item -ItemType Directory -Path $enghubDocsDestFolder
}

$enghubDocsSrcFolder = Join-Path -Path $reposFolder -ChildPath "azure-sdk-docs-eng.ms/docs"

# Call the script to build the metadata.json file
Write-Host "Building metadata.json file for enghub documents"
$buildMetadataScript = Join-Path $scriptsRoot "Markdown-BuildIndexMetadata.ps1"
& $buildMetadataScript -MarkdownDirectory $enghubDocsSrcFolder -OutputDirectory $enghubDocsDestFolder

if(Test-Path $enghubDocsDestFolder/metadata.json) {
  Copy-Item -Path $enghubDocsDestFolder/metadata.json -Destination "$embeddingSourceFolder/metadata_enghub_docs.json"
}
else {
  Write-Error "Failed to build metadata.json file for enghub documents"
  exit 1
}

# Download previous saved embeddings(last_rag_chunks_enghub_docs.json) from Azure Blob Storage
$storageAccountName = "saazuresdkbot"
$containerName = "rag-contents"
$blobName = "last_rag_chunks_enghub_docs.json"
$destinationPath = $embeddingSourceFolder
$ragChunkPath = Join-Path -Path $embeddingSourceFolder -ChildPath $blobName
if($IncrementalEmbedding -eq $true) {
  Write-Host "Downloading previous saved embeddings $blobName from Azure Blob Storage"
  if(-not (Download-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -DestinationPath $destinationPath)) {
    exit 1
  }
}

# Build embeddings
Write-Host "Building embeddings for enghub documents"
$env:RAG_CHUNK_PATH = $ragChunkPath
$env:METADATA_PATH = "$embeddingSourceFolder/metadata_enghub_docs.json"
$env:DOCUMENT_PATH = $enghubDocsDestFolder
$env:INCREMENTAL_EMBEDDING = $IncrementalEmbedding
if(-not (Build-Embeddings -EmbeddingToolFolder $embeddingToolFolder)) {
  exit 1
}

# Upload embeddings output to Azure Blob Storage
Write-Host "Uploading embeddings output $ragChunkPath to Azure Blob Storage"
if(-not (Upload-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -SourceFile $ragChunkPath)) {
  exit 1
}
