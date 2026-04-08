<#
.SYNOPSIS
    Uploads cross-language APIView token files to a running APIView instance.

.DESCRIPTION
    Uploads in the correct order:
      1. Azure.Samples.CrossLanguage.zip  (TypeSpec review — creates the project)
      2. Per-language .json files         (Python, JavaScript, Java, C#, Go)

    Authentication uses a GitHub Personal Access Token (PAT) belonging to a member
    of the Azure or Microsoft GitHub organization, which is what the local dev
    server and the staging environment both accept.

    How to get a GitHub PAT:
      - Go to https://github.com/settings/tokens
      - Classic token → scope: (no scopes needed, the token just proves identity)
      - Or pass it via the APIVIEW_GITHUB_TOKEN environment variable.

.PARAMETER ApiViewUrl
    Base URL of the APIView instance. Defaults to http://localhost:5000 (local dev).
    Use https://apiviewstagingtest.com for the staging env.

.PARAMETER GitHubToken
    GitHub PAT. Falls back to the APIVIEW_GITHUB_TOKEN env variable.

.PARAMETER OutputDir
    Directory containing the generated artifact files. Defaults to ./output.

.PARAMETER Label
    Label attached to the uploaded revision (e.g. "cross-language-test").

.EXAMPLE
    # Local dev server (start APIViewWeb first with `dotnet run`)
    pwsh upload.ps1 -GitHubToken ghp_xxxx

    # Staging
    pwsh upload.ps1 -ApiViewUrl https://apiviewstagingtest.com -GitHubToken ghp_xxxx
#>
[CmdletBinding()]
param (
    [string] $ApiViewUrl   = "http://localhost:5000",
    [string] $GitHubToken  = $env:APIVIEW_GITHUB_TOKEN,
    [string] $OutputDir    = (Join-Path $PSScriptRoot "output"),
    [string] $Label        = "cross-language-sample"
)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

if (-not $GitHubToken) {
    throw "GitHub PAT required. Pass -GitHubToken or set the APIVIEW_GITHUB_TOKEN environment variable."
}

$UploadUrl = "$($ApiViewUrl.TrimEnd('/'))/autoreview/upload"
Write-Host "==> Upload endpoint: $UploadUrl" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Helper: upload a single file to /autoreview/upload
# ---------------------------------------------------------------------------
function Invoke-Upload([string]$FilePath, [string]$FileLabel) {
    $fileName = Split-Path $FilePath -Leaf
    Write-Host "    Uploading: $fileName ..." -ForegroundColor DarkGray

    # Multipart form body
    $boundary = [System.Guid]::NewGuid().ToString()
    $mpContent = [System.Net.Http.MultipartFormDataContent]::new($boundary)

    # file part
    $fileStream   = [IO.FileStream]::new($FilePath, [IO.FileMode]::Open, [IO.FileAccess]::Read)
    $streamContent = [System.Net.Http.StreamContent]::new($fileStream)
    $streamContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")
    $mpContent.Add($streamContent, "file", $fileName)

    # label part
    $labelContent = [System.Net.Http.StringContent]::new($FileLabel)
    $mpContent.Add($labelContent, "label")

    $headers = @{ Authorization = "Bearer $GitHubToken" }

    try {
        $response = Invoke-WebRequest -Method POST -Uri $UploadUrl -Body $mpContent -Headers $headers -UseBasicParsing
        $url = $response.Content -replace '"', ''
        Write-Host "    OK ($($response.StatusCode)): $url" -ForegroundColor Green
        return $url
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        $msg  = $_.ErrorDetails.Message
        Write-Warning "    FAILED ($code): $msg"
        return $null
    } finally {
        $fileStream.Dispose()
        $mpContent.Dispose()
    }
}

# ---------------------------------------------------------------------------
# 1. Upload TypeSpec JSON directly — uploading the .zip fails because the
#    server routes .zip files to the C language service (ArgumentNullException).
#    The raw .json token file is a valid serialized CodeFile and goes through
#    JsonLanguageService which simply calls CodeFile.DeserializeAsync, producing
#    the same result with Language="TypeSpec" and CrossLanguagePackageId intact.
# ---------------------------------------------------------------------------
$tspJson = Join-Path $OutputDir "Azure.Samples.CrossLanguage.json"
if (-not (Test-Path $tspJson)) {
    throw "TypeSpec token file not found at '$tspJson'. Run generate.ps1 first."
}

Write-Host "`n==> Step 1: Upload TypeSpec review (raw JSON — zip workaround)" -ForegroundColor Cyan
$tspUrl = Invoke-Upload $tspJson $Label

# ---------------------------------------------------------------------------
# 2. Upload per-language token files
# ---------------------------------------------------------------------------
$languageFiles = @(
    @{ File = "Python.azure-samples-crosslanguage.json";    Lang = "Python"     }
    @{ File = "JavaScript.azuresamples-crosslanguage.json"; Lang = "JavaScript" }
    @{ File = "Java.com_azure_samples_crosslanguage.json";  Lang = "Java"       }
    @{ File = "C#.Azure_Samples_CrossLanguage.json";        Lang = "C#"         }
    @{ File = "Go.azcrosslanguage.json";                    Lang = "Go"         }
)

Write-Host "`n==> Step 2: Upload per-language reviews" -ForegroundColor Cyan
$results = @{}
foreach ($lf in $languageFiles) {
    $path = Join-Path $OutputDir $lf.File
    if (-not (Test-Path $path)) {
        Write-Warning "    Skipping $($lf.Lang): file not found at $path"
        continue
    }
    $url = Invoke-Upload $path $Label
    $results[$lf.Lang] = $url
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host "`n==> Summary" -ForegroundColor Cyan
Write-Host "  TypeSpec : $tspUrl" -ForegroundColor White
foreach ($lang in $results.Keys | Sort-Object) {
    Write-Host ("  {0,-12}: {1}" -f $lang, $results[$lang]) -ForegroundColor White
}
Write-Host ""
Write-Host "Open any of the URLs above in APIView to verify cross-language navigation." -ForegroundColor Yellow
Write-Host "In the review, click a method/type and use the language switcher panel" -ForegroundColor Yellow
Write-Host "to jump to the same concept in another language." -ForegroundColor Yellow
