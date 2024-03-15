[CmdletBinding()]
param (
  [Parameter(Position = 0)]
  [ValidateNotNullOrEmpty()]
  [string] $IncrementalEmbedding = $true
)

$workingDirectory = Get-Location
if($env:AGENT_ID) {
  $workingDirectory = $(System.DefaultWorkingDirectory)
}

$scriptsRoot = Join-Path $workingDirectory "Scripts"
$embeddingToolFolder = Join-Path $workingDirectory "embeddings"

. (Join-Path $scriptsRoot common.ps1)

Write-Host "scriptsRoot: $scriptsRoot"
Write-Host "embeddingToolFolder: $embeddingToolFolder"

# Create 'repos' folder on current location
$reposFolder = Join-Path -Path $workingDirectory -ChildPath "repos"
if (-not (Test-Path -Path $reposFolder)) {
  New-Item -ItemType Directory -Path $reposFolder
}

# Clone Azure/typespec-azure repository
Write-Host "Cloning Azure/typespec-azure repository at $reposFolder"
if(-not (Clone-Repository -RepoUrl "https://github.com/Azure/typespec-azure.git" -RootFolder $reposFolder)) {
  exit 1
}

# Create embeddingSource folder on current location
$embeddingSourceFolder = Join-Path -Path $workingDirectory -ChildPath "embeddingSource"
if (-not (Test-Path -Path $embeddingSourceFolder)) {
  New-Item -ItemType Directory -Path $embeddingSourceFolder
}

# Create folder to save the Azure/typespec-azure documents
$typespecDocsDestFolder = Join-Path -Path $embeddingSourceFolder -ChildPath "typespec-docs"
if (-not (Test-Path -Path $typespecDocsDestFolder)) {
  New-Item -ItemType Directory -Path $typespecDocsDestFolder
}

$typespecDocsSrcFolder = Join-Path -Path $reposFolder -ChildPath "typespec-azure/docs"

# Call the script to build the metadata.json file
Write-Host "Building metadata.json file for typespec documents"
$buildMetadataScript = Join-Path $scriptsRoot "Markdown-BuildIndexMetadata.ps1"
& $buildMetadataScript -MarkdownDirectory $typespecDocsSrcFolder -OutputDirectory $typespecDocsDestFolder -DocBaseUrl "https://github.com/Azure/typespec-azure/tree/main/docs"

if(Test-Path $typespecDocsDestFolder/metadata.json) {
  Copy-Item -Path $typespecDocsDestFolder/metadata.json -Destination "$embeddingSourceFolder/metadata_typespec_docs.json"
}
else {
  Write-Error "Failed to build metadata.json file for typespec documents"
  exit 1
}

# Download previous saved embeddings(last_rag_chunks_typespec_docs.json) from Azure Blob Storage
$storageAccountName = "saazuresdkbot"
$containerName = "rag-contents"
$blobName = "last_rag_chunks_typespec_docs.json"
$destinationPath = $embeddingSourceFolder
$ragChunkPath = Join-Path -Path $embeddingSourceFolder -ChildPath $blobName
if($IncrementalEmbedding -eq $true) {
  Write-Host "Downloading previous saved embeddings $blobName from Azure Blob Storage"
  if(-not (Download-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -DestinationPath $destinationPath)) {
    exit 1
  }
}

# Build embeddings
Write-Host "Building embeddings for typespec documents"
$env:RAG_CHUNK_PATH = $ragChunkPath
$env:METADATA_PATH = "$embeddingSourceFolder/metadata_typespec_docs.json"
$env:DOCUMENT_PATH = $typespecDocsDestFolder
$env:INCREMENTAL_EMBEDDING = $IncrementalEmbedding
if(-not (Build-Embeddings -EmbeddingToolFolder $embeddingToolFolder)) {
  exit 1
}

# Upload embeddings output to Azure Blob Storage
Write-Host "Uploading embeddings output $ragChunkPath to Azure Blob Storage"
if(-not (Upload-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -SourceFile $ragChunkPath)) {
  exit 1
}
