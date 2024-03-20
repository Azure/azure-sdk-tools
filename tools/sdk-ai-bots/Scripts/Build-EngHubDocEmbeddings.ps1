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

# Create folder to save the enghub documents
$enghubDocsDestFolder = Join-Path -Path $embeddingSourceFolder -ChildPath "enghub-docs"
if (-not (Test-Path -Path $enghubDocsDestFolder)) {
  New-Item -ItemType Directory -Path $enghubDocsDestFolder
}

$reposFolder = Join-Path -Path $buildSourceDirectory -ChildPath "azure-sdk-docs-eng.ms"
if(-not (Test-Path $reposFolder)) {
  # Clone eng hub repository
  Write-Host "Cloning azure-sdk-docs-eng.ms repository at $buildSourceDirectory"
  if(-not (Clone-Repository -RepoUrl "https://azure-sdk@dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms" -RootFolder $buildSourceDirectory)) {
    exit 1
  }
}
$enghubDocsSrcFolder = Join-Path -Path $buildSourceDirectory -ChildPath "azure-sdk-docs-eng.ms/docs"
if(-not (Test-Path $enghubDocsSrcFolder)) {
  Write-Error "Failed to find the enghub documents folder at $enghubDocsSrcFolder"
  exit 1
}

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

$env:AZURESEARCH_FIELDS_CONTENT = "Text"
$env:AZURESEARCH_FIELDS_CONTENT_VECTOR = "Embedding"
$env:AZURESEARCH_FIELDS_TAG = "AdditionalMetadata"
$env:AZURESEARCH_FIELDS_ID = "Id"

if(-not (Initialize-CondaEnv)) {
  exit 1
}

# print the variables
Write-Host "RAG_CHUNK_PATH: $env:RAG_CHUNK_PATH"
Write-Host "METADATA_PATH: $env:METADATA_PATH"
Write-Host "DOCUMENT_PATH: $env:DOCUMENT_PATH"
Write-Host "INCREMENTAL_EMBEDDING: $env:INCREMENTAL_EMBEDDING"
Write-Host "AZURE_OPENAI_ENDPOINT: $env:AZURE_OPENAI_ENDPOINT"
Write-Host "AZURE_SEARCH_ENDPOINT: $env:AZURE_SEARCH_ENDPOINT"
Write-Host "AZURE_SEARCH_INDEX_NAME: $env:AZURE_SEARCH_INDEX_NAME"
Write-Host "AZURE_OPENAI_EMBEDDING_MODEL: $env:AZURE_OPENAI_EMBEDDING_MODEL"
Write-Host "AZURE_OPENAI_API_KEY: $env:AZURE_OPENAI_API_KEY"
Write-Host "AZURE_SEARCH_KEY: $env:AZURE_SEARCH_KEY"
Write-Host "AZURE_STORAGE_ACCOUNT_KEY: $env:AZURE_STORAGE_ACCOUNT_KEY"
Write-Host "AZURESEARCH_FIELDS_CONTENT: $env:AZURESEARCH_FIELDS_CONTENT"
Write-Host "AZURESEARCH_FIELDS_CONTENT_VECTOR: $env:AZURESEARCH_FIELDS_CONTENT_VECTOR"
Write-Host "AZURESEARCH_FIELDS_TAG: $env:AZURESEARCH_FIELDS_TAG"
Write-Host "AZURESEARCH_FIELDS_ID: $env:AZURESEARCH_FIELDS_ID"
if(-not (Build-Embeddings -EmbeddingToolFolder $embeddingToolFolder)) {
  exit 1
}

# Upload embeddings output to Azure Blob Storage
Write-Host "Uploading embeddings output $ragChunkPath to Azure Blob Storage"
if(-not (Upload-AzureBlob -StorageAccountName $storageAccountName -ContainerName $containerName -BlobName $blobName -SourceFile $ragChunkPath)) {
  exit 1
}
