<#
.SYNOPSIS
This code build the index metadata file for the markdown files.

.DESCRIPTION
This script is used to generate metadata file for all the specified markdown files. 
Then use the metadata file and these markdown files to generate the embeddings in azure search 
which would be used as the knowledge base of Teams Assistant for Azure SDK.

.PARAMETER MarkdownDirectory
The directory of markdown files.

.PARAMETER OutputDirectory
The output directory of metadata file.

.PARAMETER DocBaseUrl
The base url that will be used to access the markdown file.

.EXAMPLE
Markdown-BuildIndexMetadata.ps1 -MarkdownDirectory "C:\repos\azure-sdk-docs\docs" -OutputDirectory "C:\repos\azure-sdk-docs\embeddingSource\enghub-docs"
#>
[CmdletBinding()]
param (
  [Parameter(Position = 0)]
  [ValidateNotNullOrEmpty()]
  [string] $MarkdownDirectory,
  [Parameter(Position = 1)]
  [string] $OutputDirectory,
  [string] $DocBaseUrl
  
)

if (-not $DocBaseUrl)
{
    $DocBaseUrl = "https://eng.ms/docs/products/azure-developer-experience"
}

Write-Host "Building metadata for markdown files in $MarkdownDirectory and output to $OutputDirectory. DocBaseUrl: $DocBaseUrl"

function Generate-Metadata([string]$rootFolder, [string]$outputFolder)
{
    $metadataFileName = Join-Path $outputFolder "metadata.json"

    # Define an empty hashtable to hold the objects in JSON
    $markdownFiles = @{}

    if (-not [System.IO.Path]::IsPathRooted($rootFolder))
    {
        $rootFolder = Convert-Path $rootFolder
    }
    
    Get-ChildItem -Path $rootFolder -Recurse -File | ForEach-Object {
        if(($_.Directory.Name -ne "docs") -and $_.Name.EndsWith(".md"))
        {
            $fileName = $_.BaseName
            $typespecRepoName = "typespec-azure"
            if($_.DirectoryName.Contains($typespecRepoName))
            {
                # use full file name for typespec-azure repo document
                $fileName = $_.Name
            }
            $pagePath = $_.DirectoryName.Substring($_.DirectoryName.IndexOf("\docs\")+"\docs\".Length).Replace('\','/')
            $url = $DocBaseUrl + '/' + $pagePath + '/' + $fileName
            $url = $url.Replace(' ', '%20')
            Write-Host "The URL of the Markdown file is: $url"
            $title = Get-TitleFromMarkdown $_.FullName
            Write-Host "The title of the Markdown file is: $title"
            # adding path path to key to avoid name conflict
            $key = $pagePath + '/' + $_.Name
            $key = $key.Replace(' ', '-')
            $key = $key.Replace('/', '-')
            Write-Host "The key of the Markdown file is: $key"
            $fileData = @{
                "title" = $title
                "url" = $url
            }
            $markdownFiles.Add($key, $fileData)
        }
    }

    # Convert the hashtable to JSON format
    $json = ConvertTo-Json $markdownFiles -Depth 2

    # Save the JSON to a file
    Set-Content -Path $metadataFileName -Value $json
}

function Get-TitleFromMarkdown([string]$filePath)
{
    $markdown = Get-Content -Path $filePath

    # Find the first line that starts with a single '#' character
    $titleLine = $markdown | Where-Object { $_ -match "^#\s(.+)" } | Select-Object -First 1

    # Extract the title from the title line
    $title = $titleLine -replace "^#\s"

    return $title
}

function Copy-Files([string]$rootFolder, [string]$outputFolder) {
    Get-ChildItem -Path $rootFolder -Recurse -File | ForEach-Object {
        if(($_.Directory.Name -ne "docs") -and $_.Name.EndsWith(".md"))
        {
            $pagePath = $_.DirectoryName.Substring($_.DirectoryName.IndexOf("\docs\")+"\docs\".Length).Replace('\','/')
            $key = $pagePath + '/' + $_.Name
            $key = $key.Replace(' ', '-')
            $key = $key.Replace('/', '-')

            if(!(Test-Path $outputFolder))
            {
                New-Item -ItemType Directory -Path $outputFolder
            }
            $newFileName = Join-Path $outputFolder $key
            Write-Debug "Copying file $_ to $newFileName"
            Copy-Item -Path $_.FullName -Destination $newFileName
        }
    }
}

# Convert path to absolute path
$MarkdownDirectory = Convert-Path $MarkdownDirectory
$OutputDirectory = Convert-Path $OutputDirectory

# Generate metadata file
Write-Host "Generating metadata file..."
Generate-Metadata $MarkdownDirectory $OutputDirectory
# Copy markdown files to output folder
Write-Host "Copying markdown files to output folder..."
Copy-Files $MarkdownDirectory $OutputDirectory


