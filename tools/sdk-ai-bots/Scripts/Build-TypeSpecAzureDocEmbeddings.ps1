<#
.SYNOPSIS
This code build the embeddings for the document under https://github.com/Azure/typespec-azure.

.DESCRIPTION
This code is responsible for refreshing the embeddings for the document of typespec.

.PARAMETER IncrementalEmbedding
Control the incremental building behavior for the embeddings.

.EXAMPLE
Build-TypeSpecAzureDocEmbeddings.ps1 -IncrementalEmbedding $true
#>

[CmdletBinding()]
param (
  [Parameter(Position = 0)]
  [ValidateNotNullOrEmpty()]
  [string] $IncrementalEmbedding = $true
)
# Set the working directory, current location is supposed to be the root of the repository
$buildSourceDirectory = Get-Location
$workingDirectory = Join-Path $buildSourceDirectory "tools\sdk-ai-bots"
if($env:AGENT_ID) {
  # Running in Azure DevOps
  $workingDirectory = Join-Path $buildSourceDirectory "azure-sdk-tools\tools\sdk-ai-bots"
}
$scriptsRoot = Join-Path $workingDirectory "Scripts"
$embeddingToolFolder = Join-Path $workingDirectory "Embeddings"

Write-Host "scriptsRoot: $scriptsRoot"
Write-Host "embeddingToolFolder: $embeddingToolFolder"
. (Join-Path $scriptsRoot Common.ps1)

# Install Az.Storage module
if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
  Install-Module -Name Az.Storage -Force -AllowClobber -Scope CurrentUser
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

$reposFolder = Join-Path -Path $buildSourceDirectory -ChildPath "typespec-azure"
if(-not (Test-Path $reposFolder)) {
  # Clone Azure/typespec-azure repository
  Write-Host "Cloning Azure/typespec-azure repository at $buildSourceDirectory"
  if(-not (Clone-Repository -RepoUrl "https://github.com/Azure/typespec-azure.git" -RootFolder $buildSourceDirectory)) {
    exit 1
  }
}
$typespecDocsSrcFolder = Join-Path -Path $buildSourceDirectory -ChildPath "typespec-azure/docs"
if(-not (Test-Path $typespecDocsSrcFolder)) {
  Write-Error "Failed to find the typespec documents folder at $typespecDocsSrcFolder"
  exit 1
}

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
