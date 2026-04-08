<#
.SYNOPSIS
    Compiles the cross-language TypeSpec sample and produces the APIView artifact zip.

.DESCRIPTION
    Runs two steps:
      1. tsp compile  → generates  output/Azure.Samples.CrossLanguage.json  (APIView token)
      2. tsp compile  → generates  output/typespec-metadata.json            (language metadata)
    Then bundles both files into  output/Azure.Samples.CrossLanguage.zip,
    which can be uploaded directly to the APIView service for cross-language review testing.

.EXAMPLE
    # From the cross-language-sample directory:
    pwsh generate.ps1

    # Or from the repo root:
    pwsh tools/apiview/cross-language-sample/generate.ps1
#>
[CmdletBinding()]
param ()

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

$SampleDir = $PSScriptRoot
$OutputDir = Join-Path $SampleDir "output"

# ---------------------------------------------------------------------------
# Step 0: Install npm packages
# ---------------------------------------------------------------------------
Write-Host "==> Installing npm packages..." -ForegroundColor Cyan
Push-Location $SampleDir
try {
    npm install
    if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit code $LASTEXITCODE)" }
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Helper: run tsp compile with a given emit target
# ---------------------------------------------------------------------------
function Invoke-TspCompile([string]$Emit, [string[]]$Options) {
    $args = @("tsp", "compile", ".", "--emit=$Emit") + ($Options | ForEach-Object { "--option", $_ })
    Write-Host "==> npx $args" -ForegroundColor DarkGray
    Push-Location $SampleDir
    try {
        npx @args
        if ($LASTEXITCODE -ne 0) { throw "tsp compile failed for emit '$Emit' (exit code $LASTEXITCODE)" }
    } finally {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# Step 1: Generate the APIView token file
# ---------------------------------------------------------------------------
Write-Host "`n==> Compiling TypeSpec → APIView token file..." -ForegroundColor Cyan
New-Item $OutputDir -ItemType Directory -Force | Out-Null
Invoke-TspCompile "@azure-tools/typespec-apiview"

# The apiview emitter writes to tsp-output/@azure-tools/apiview.json by default.
$generatedToken = Get-Item -Path (Join-Path $SampleDir "tsp-output\@azure-tools\apiview.json") -ErrorAction SilentlyContinue
if (-not $generatedToken) {
    # Wider fallback search (skip node_modules)
    $generatedToken = Get-ChildItem -Path $SampleDir -Filter "apiview.json" -Recurse -Depth 4 `
        | Where-Object { $_.FullName -notlike "*node_modules*" } `
        | Select-Object -First 1
}
if (-not $generatedToken) {
    throw "APIView emitter did not produce Azure.Samples.CrossLanguage.json"
}
$tokenFile = Join-Path $OutputDir "Azure.Samples.CrossLanguage.json"
Copy-Item -Path $generatedToken.FullName -Destination $tokenFile -Force
Write-Host "    Generated: $tokenFile" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Step 2: Generate the typespec-metadata.json (cross-language package info)
# ---------------------------------------------------------------------------
Write-Host "`n==> Compiling TypeSpec → typespec-metadata.json..." -ForegroundColor Cyan
try {
    Invoke-TspCompile "@azure-tools/typespec-metadata" @(
        "@azure-tools/typespec-metadata.outputFile={project-root}/output/typespec-metadata.json",
        "@azure-tools/typespec-metadata.format=json"
    )
    $metadataFile = Join-Path $OutputDir "typespec-metadata.json"
    Write-Host "    Generated: $metadataFile" -ForegroundColor Green
} catch {
    Write-Warning "typespec-metadata emitter failed ($_). Generating a stub metadata file instead."
    # Fallback: write a hand-crafted metadata file matching the expected TypeSpecMetadata schema.
    $metadataFile = Join-Path $OutputDir "typespec-metadata.json"
    $stub = [PSCustomObject]@{
        emitterVersion = "0.7.2"
        generatedAt    = (Get-Date -Format "o")
        typeSpec       = [PSCustomObject]@{
            namespace     = "Azure.Samples.CrossLanguage"
            documentation = "Azure Sample Widget Service cross-language TypeSpec"
            type          = "client"
        }
        languages = [PSCustomObject]@{
            Python = [PSCustomObject]@{
                emitterName = "@azure-tools/typespec-python"
                packageName = "azure-samples-crosslanguage"
                namespace   = "azure.samples.crosslanguage"
            }
            JavaScript = [PSCustomObject]@{
                emitterName = "@azure-tools/typespec-ts"
                packageName = "@azure/samples-crosslanguage"
                namespace   = "@azure/samples-crosslanguage"
            }
            Java = [PSCustomObject]@{
                emitterName = "@azure-tools/typespec-java"
                packageName = "com.azure.samples.crosslanguage"
                namespace   = "com.azure.samples.crosslanguage"
            }
            DotNet = [PSCustomObject]@{
                emitterName = "@azure-tools/typespec-csharp"
                packageName = "Azure.Samples.CrossLanguage"
                namespace   = "Azure.Samples.CrossLanguage"
            }
            Go = [PSCustomObject]@{
                emitterName = "@azure-tools/typespec-go"
                packageName = "azcrosslanguage"
                namespace   = "github.com/Azure/azure-sdk-for-go/sdk/samples/crosslanguage/azcrosslanguage"
            }
        }
    }
    New-Item $OutputDir -ItemType Directory -Force | Out-Null
    $stub | ConvertTo-Json -Depth 10 | Set-Content $metadataFile -Encoding UTF8
    Write-Host "    Stub written: $metadataFile" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Step 3: Bundle into a zip
# ---------------------------------------------------------------------------
$metadataFile = Join-Path $OutputDir "typespec-metadata.json"
$zipFile       = Join-Path $OutputDir "Azure.Samples.CrossLanguage.zip"

Write-Host "`n==> Creating artifact zip: $zipFile" -ForegroundColor Cyan
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($tokenFile, $metadataFile)) {
        if (Test-Path $file) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $file, (Split-Path $file -Leaf)) | Out-Null
            Write-Host "    Added: $(Split-Path $file -Leaf)" -ForegroundColor Green
        }
    }
} finally {
    $zip.Dispose()
}

Write-Host "`n==> Done! Artifact zip ready at:" -ForegroundColor Cyan
Write-Host "    $zipFile" -ForegroundColor White

# ---------------------------------------------------------------------------
# Step 4: Generate per-language token files with real code-like structure
# ---------------------------------------------------------------------------
# Each file uses proper token kinds and language-idiomatic code patterns.
# CrossLanguageId on each line matches the TypeSpec emitter so the APIView UI
# can navigate across languages.
Write-Host "`n==> Generating per-language token files with code structure..." -ForegroundColor Cyan

$xp = "Azure.Samples.CrossLanguage"
$crossLanguagePackageId = $xp

# Token builders – Kind: 0=text 1=punct 2=keyword 3=typeName 4=memberName 5=stringLit 6=literal
function t([int]$k, [string]$v, [bool]$p = $false) { [ordered]@{ Kind = $k; Value = $v; HasPrefixSpace = $p } }
function kw([string]$v, [bool]$p = $false) { t 2 $v $p }
function ty([string]$v, [bool]$p = $false) { t 3 $v $p }
function mb([string]$v, [bool]$p = $false) { t 4 $v $p }
function pu([string]$v, [bool]$p = $false) { t 1 $v $p }
function tx([string]$v, [bool]$p = $false) { t 0 $v $p }
function st([string]$v, [bool]$p = $false) { t 5 $v $p }

# ReviewLine builder
function rl([string]$lid, [string]$xid, [array]$toks, [array]$kids = @(), [bool]$ctx = $false) {
    $l = [ordered]@{ LineId = $lid; CrossLanguageId = $xid; Tokens = $toks; Children = $kids }
    if ($ctx) { $l['IsContextEndLine'] = $true }
    $l
}
function el()                   { rl '' '' @() }                                     # blank separator
function cl([string]$r = '')    {                                                     # closing brace
    $l = [ordered]@{ LineId = ''; CrossLanguageId = ''; Tokens = @(pu '}'); Children = @() }
    $l['IsContextEndLine'] = $true
    if ($r) { $l['RelatedToLine'] = $r }
    $l
}

# Write a finished token file to disk
function Write-TokenFile([string]$lang, [string]$pkg, [string]$ver, [string]$fileName, [array]$lines) {
    $clDef = [ordered]@{}
    foreach ($line in $lines) {
        if ($line.CrossLanguageId) { $clDef["$lang.$($line.CrossLanguageId)"] = $line.CrossLanguageId }
        foreach ($child in $line.Children) {
            if ($child.CrossLanguageId) { $clDef["$lang.$($child.CrossLanguageId)"] = $child.CrossLanguageId }
            foreach ($gc in $child.Children) {
                if ($gc.CrossLanguageId) { $clDef["$lang.$($gc.CrossLanguageId)"] = $gc.CrossLanguageId }
            }
        }
    }
    $file = [ordered]@{
        PackageName            = $pkg
        PackageVersion         = $ver
        ParserVersion          = "stub-1.0.0"
        Language               = $lang
        CrossLanguagePackageId = $crossLanguagePackageId
        CrossLanguageMetadata  = [ordered]@{
            CrossLanguagePackageId    = $crossLanguagePackageId
            CrossLanguageDefinitionId = $clDef
        }
        ReviewLines = $lines
    }
    $path = Join-Path $OutputDir $fileName
    $file | ConvertTo-Json -Depth 15 | Set-Content $path -Encoding UTF8
    Write-Host "    $lang -> $path" -ForegroundColor Green
}

# ===========================================================================
# JAVA
# ===========================================================================
$jLines = @(
    (rl "Java.$xp" $xp @( (kw 'package'); (ty 'com.azure.samples.crosslanguage' $true); (pu ';') )),
    (el),
    (rl "Java.$xp.Widgets" "$xp.Widgets" @( (kw 'public'); (kw 'interface' $true); (ty 'WidgetsClient' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.Widgets.getWidget" "$xp.Widgets.getWidget" @( (ty 'Widget'); (mb 'getWidget' $true); (pu '('); (ty 'String'); (tx 'widgetId' $true); (pu ')'); (pu ';') )),
        (rl "Java.$xp.Widgets.createOrReplaceWidget" "$xp.Widgets.createOrReplaceWidget" @( (ty 'Widget'); (mb 'createOrReplaceWidget' $true); (pu '('); (ty 'String'); (tx 'widgetId' $true); (pu ','); (ty 'Widget' $true); (tx 'widget' $true); (pu ')'); (pu ';') )),
        (rl "Java.$xp.Widgets.updateWidget" "$xp.Widgets.updateWidget" @( (ty 'Widget'); (mb 'updateWidget' $true); (pu '('); (ty 'String'); (tx 'widgetId' $true); (pu ','); (ty 'Widget' $true); (tx 'widget' $true); (pu ')'); (pu ';') )),
        (rl "Java.$xp.Widgets.deleteWidget" "$xp.Widgets.deleteWidget" @( (kw 'void'); (mb 'deleteWidget' $true); (pu '('); (ty 'String'); (tx 'widgetId' $true); (pu ')'); (pu ';') )),
        (rl "Java.$xp.Widgets.listWidgets" "$xp.Widgets.listWidgets" @( (ty 'PagedIterable'); (pu '<'); (ty 'Widget'); (pu '>'); (mb 'listWidgets' $true); (pu '('); (ty 'WidgetListOptions'); (tx 'options' $true); (pu ')'); (pu ';') )),
        (rl "Java.$xp.Widgets.analyzeWidget" "$xp.Widgets.analyzeWidget" @( (ty 'WidgetAnalysisResult'); (mb 'analyzeWidget' $true); (pu '('); (ty 'String'); (tx 'widgetId' $true); (pu ')'); (pu ';') )),
        (cl "Java.$xp.Widgets")
    )),
    (el),
    (rl "Java.$xp.Widget" "$xp.Widget" @( (kw 'public'); (kw 'class' $true); (ty 'Widget' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.Widget.id"             '' @( (kw 'public'); (kw 'final' $true); (ty 'String' $true);        (tx 'id' $true);             (pu ';') )),
        (rl "Java.$xp.Widget.name"           '' @( (kw 'public'); (kw 'final' $true); (ty 'String' $true);        (tx 'name' $true);           (pu ';') )),
        (rl "Java.$xp.Widget.color"          '' @( (kw 'public'); (kw 'final' $true); (ty 'WidgetColor' $true);   (tx 'color' $true);          (pu ';') )),
        (rl "Java.$xp.Widget.description"    '' @( (kw 'public'); (kw 'final' $true); (ty 'String' $true);        (tx 'description' $true);    (pu ';') )),
        (rl "Java.$xp.Widget.manufacturedOn" '' @( (kw 'public'); (kw 'final' $true); (ty 'OffsetDateTime' $true); (tx 'manufacturedOn' $true); (pu ';') )),
        (cl "Java.$xp.Widget")
    )),
    (el),
    (rl "Java.$xp.Versions" "$xp.Versions" @( (kw 'public'); (kw 'enum' $true); (ty 'ServiceVersion' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.Versions.v2024_01_01" "$xp.Versions.v2024_01_01" @( (tx 'V2024_01_01'); (pu '('); (st '"2024-01-01"'); (pu ')') )),
        (cl "Java.$xp.Versions")
    )),
    (el),
    (rl "Java.$xp.WidgetAnalysisResult" "$xp.WidgetAnalysisResult" @( (kw 'public'); (kw 'class' $true); (ty 'WidgetAnalysisResult' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.WidgetAnalysisResult.widgetId"     '' @( (kw 'public'); (kw 'final' $true); (ty 'String' $true); (tx 'widgetId' $true);     (pu ';') )),
        (rl "Java.$xp.WidgetAnalysisResult.qualityScore" '' @( (kw 'public'); (kw 'final' $true); (ty 'float' $true);  (tx 'qualityScore' $true); (pu ';') )),
        (rl "Java.$xp.WidgetAnalysisResult.summary"      '' @( (kw 'public'); (kw 'final' $true); (ty 'String' $true); (tx 'summary' $true);      (pu ';') )),
        (cl "Java.$xp.WidgetAnalysisResult")
    )),
    (el),
    (rl "Java.$xp.WidgetColor" "$xp.WidgetColor" @( (kw 'public'); (kw 'enum' $true); (ty 'WidgetColor' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.WidgetColor.string" "$xp.WidgetColor.string" @( (ty 'String') )),
        (rl "Java.$xp.WidgetColor.Red"    "$xp.WidgetColor.Red"    @( (tx 'RED');   (pu ',') )),
        (rl "Java.$xp.WidgetColor.Green"  "$xp.WidgetColor.Green"  @( (tx 'GREEN'); (pu ',') )),
        (rl "Java.$xp.WidgetColor.Blue"   "$xp.WidgetColor.Blue"   @( (tx 'BLUE') )),
        (cl "Java.$xp.WidgetColor")
    )),
    (el),
    (rl "Java.$xp.WidgetListParams" "$xp.WidgetListParams" @( (kw 'public'); (kw 'class' $true); (ty 'WidgetListOptions' $true); (pu ' {' $true) ) @(
        (rl "Java.$xp.WidgetListParams.color"      '' @( (kw 'public'); (ty 'WidgetColor' $true); (tx 'color' $true);      (pu ';') )),
        (rl "Java.$xp.WidgetListParams.maxResults" '' @( (kw 'public'); (ty 'Integer' $true);     (tx 'maxResults' $true); (pu ';') )),
        (cl "Java.$xp.WidgetListParams")
    ))
)
Write-TokenFile 'Java' 'com.azure.samples.crosslanguage' '1.0.0' 'Java.com_azure_samples_crosslanguage.json' $jLines

# ===========================================================================
# PYTHON
# ===========================================================================
$pyLines = @(
    (rl "Python.$xp" $xp @( (kw 'namespace'); (tx 'azure.samples.crosslanguage' $true) )),
    (el),
    (rl "Python.$xp.Widgets" "$xp.Widgets" @( (kw 'class'); (ty 'WidgetClient' $true); (pu ':') ) @(
        (rl "Python.$xp.Widgets.getWidget" "$xp.Widgets.getWidget" @( (kw 'def'); (mb 'get_widget' $true); (pu '('); (tx 'self'); (pu ','); (tx 'widget_id' $true); (pu ':'); (ty 'str' $true); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (ty 'Widget' $true); (pu ':'); (tx '...' $true) )),
        (rl "Python.$xp.Widgets.createOrReplaceWidget" "$xp.Widgets.createOrReplaceWidget" @( (kw 'def'); (mb 'create_or_replace_widget' $true); (pu '('); (tx 'self'); (pu ','); (tx 'widget_id' $true); (pu ':'); (ty 'str' $true); (pu ','); (tx 'widget' $true); (pu ':'); (ty 'Widget' $true); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (ty 'Widget' $true); (pu ':'); (tx '...' $true) )),
        (rl "Python.$xp.Widgets.updateWidget" "$xp.Widgets.updateWidget" @( (kw 'def'); (mb 'update_widget' $true); (pu '('); (tx 'self'); (pu ','); (tx 'widget_id' $true); (pu ':'); (ty 'str' $true); (pu ','); (tx 'widget' $true); (pu ':'); (ty 'Widget' $true); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (ty 'Widget' $true); (pu ':'); (tx '...' $true) )),
        (rl "Python.$xp.Widgets.deleteWidget" "$xp.Widgets.deleteWidget" @( (kw 'def'); (mb 'delete_widget' $true); (pu '('); (tx 'self'); (pu ','); (tx 'widget_id' $true); (pu ':'); (ty 'str' $true); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (kw 'None' $true); (pu ':'); (tx '...' $true) )),
        (rl "Python.$xp.Widgets.listWidgets" "$xp.Widgets.listWidgets" @( (kw 'def'); (mb 'list_widgets' $true); (pu '('); (tx 'self'); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (ty 'ItemPaged' $true); (pu '['); (ty 'Widget'); (pu ']'); (pu ':'); (tx '...' $true) )),
        (rl "Python.$xp.Widgets.analyzeWidget" "$xp.Widgets.analyzeWidget" @( (kw 'def'); (mb 'analyze_widget' $true); (pu '('); (tx 'self'); (pu ','); (tx 'widget_id' $true); (pu ':'); (ty 'str' $true); (pu ','); (tx '**kwargs' $true); (pu ')'); (pu ' ->'); (ty 'WidgetAnalysisResult' $true); (pu ':'); (tx '...' $true) ))
    )),
    (el),
    (rl "Python.$xp.Widget" "$xp.Widget" @( (kw 'class'); (ty 'Widget' $true); (pu ':') ) @(
        (rl "Python.$xp.Widget.id"             '' @( (tx 'id');              (pu ':'); (ty 'str' $true) )),
        (rl "Python.$xp.Widget.name"           '' @( (tx 'name');            (pu ':'); (ty 'str' $true) )),
        (rl "Python.$xp.Widget.color"          '' @( (tx 'color');           (pu ':'); (ty 'WidgetColor' $true) )),
        (rl "Python.$xp.Widget.description"    '' @( (tx 'description');     (pu ':'); (ty 'Optional' $true); (pu '['); (ty 'str'); (pu ']') )),
        (rl "Python.$xp.Widget.manufacturedOn" '' @( (tx 'manufactured_on'); (pu ':'); (ty 'Optional' $true); (pu '['); (ty 'datetime'); (pu ']') ))
    )),
    (el),
    (rl "Python.$xp.Versions" "$xp.Versions" @( (kw 'class'); (ty 'ApiVersion' $true); (pu '('); (ty 'str'); (pu ','); (ty 'Enum' $true); (pu '):') ) @(
        (rl "Python.$xp.Versions.v2024_01_01" "$xp.Versions.v2024_01_01" @( (tx 'V2024_01_01'); (pu ' ='); (st '"2024-01-01"' $true) ))
    )),
    (el),
    (rl "Python.$xp.WidgetAnalysisResult" "$xp.WidgetAnalysisResult" @( (kw 'class'); (ty 'WidgetAnalysisResult' $true); (pu ':') ) @(
        (rl "Python.$xp.WidgetAnalysisResult.widgetId"     '' @( (tx 'widget_id');     (pu ':'); (ty 'str' $true) )),
        (rl "Python.$xp.WidgetAnalysisResult.qualityScore" '' @( (tx 'quality_score'); (pu ':'); (ty 'float' $true) )),
        (rl "Python.$xp.WidgetAnalysisResult.summary"      '' @( (tx 'summary');       (pu ':'); (ty 'str' $true) ))
    )),
    (el),
    (rl "Python.$xp.WidgetColor" "$xp.WidgetColor" @( (kw 'class'); (ty 'WidgetColor' $true); (pu '('); (ty 'str'); (pu ','); (ty 'Enum' $true); (pu '):') ) @(
        (rl "Python.$xp.WidgetColor.string" "$xp.WidgetColor.string" @( (ty 'str') )),
        (rl "Python.$xp.WidgetColor.Red"    "$xp.WidgetColor.Red"    @( (tx 'RED');   (pu ' ='); (st '"Red"' $true) )),
        (rl "Python.$xp.WidgetColor.Green"  "$xp.WidgetColor.Green"  @( (tx 'GREEN'); (pu ' ='); (st '"Green"' $true) )),
        (rl "Python.$xp.WidgetColor.Blue"   "$xp.WidgetColor.Blue"   @( (tx 'BLUE');  (pu ' ='); (st '"Blue"' $true) ))
    )),
    (el),
    (rl "Python.$xp.WidgetListParams" "$xp.WidgetListParams" @( (kw 'class'); (ty 'WidgetListParams' $true); (pu ':') ) @(
        (rl "Python.$xp.WidgetListParams.color"      '' @( (tx 'color');       (pu ':'); (ty 'Optional' $true); (pu '['); (ty 'WidgetColor'); (pu ']') )),
        (rl "Python.$xp.WidgetListParams.maxResults" '' @( (tx 'max_results'); (pu ':'); (ty 'Optional' $true); (pu '['); (ty 'int'); (pu ']') ))
    ))
)
Write-TokenFile 'Python' 'azure-samples-crosslanguage' '1.0.0' 'Python.azure-samples-crosslanguage.json' $pyLines

# ===========================================================================
# JAVASCRIPT / TYPESCRIPT
# ===========================================================================
$jsLines = @(
    (rl "JavaScript.$xp" $xp @( (kw 'package'); (tx '@azure/samples-crosslanguage' $true) )),
    (el),
    (rl "JavaScript.$xp.Widgets" "$xp.Widgets" @( (kw 'export'); (kw 'class' $true); (ty 'WidgetsClient' $true); (pu ' {' $true) ) @(
        (rl "JavaScript.$xp.Widgets.getWidget" "$xp.Widgets.getWidget" @( (mb 'getWidget'); (pu '('); (tx 'widgetId'); (pu ':'); (ty 'string' $true); (pu ','); (tx 'options' $true); (pu '?:'); (ty 'GetWidgetOptions' $true); (pu ')'); (pu ':'); (ty 'Promise' $true); (pu '<'); (ty 'Widget'); (pu '>'); (pu ';') )),
        (rl "JavaScript.$xp.Widgets.createOrReplaceWidget" "$xp.Widgets.createOrReplaceWidget" @( (mb 'createOrReplaceWidget'); (pu '('); (tx 'widgetId'); (pu ':'); (ty 'string' $true); (pu ','); (tx 'widget' $true); (pu ':'); (ty 'Widget' $true); (pu ')'); (pu ':'); (ty 'Promise' $true); (pu '<'); (ty 'Widget'); (pu '>'); (pu ';') )),
        (rl "JavaScript.$xp.Widgets.updateWidget" "$xp.Widgets.updateWidget" @( (mb 'updateWidget'); (pu '('); (tx 'widgetId'); (pu ':'); (ty 'string' $true); (pu ','); (tx 'widget' $true); (pu ':'); (ty 'Widget' $true); (pu ')'); (pu ':'); (ty 'Promise' $true); (pu '<'); (ty 'Widget'); (pu '>'); (pu ';') )),
        (rl "JavaScript.$xp.Widgets.deleteWidget" "$xp.Widgets.deleteWidget" @( (mb 'deleteWidget'); (pu '('); (tx 'widgetId'); (pu ':'); (ty 'string' $true); (pu ')'); (pu ':'); (ty 'Promise' $true); (pu '<'); (kw 'void'); (pu '>'); (pu ';') )),
        (rl "JavaScript.$xp.Widgets.listWidgets" "$xp.Widgets.listWidgets" @( (mb 'listWidgets'); (pu '('); (tx 'options' $true); (pu '?:'); (ty 'ListWidgetsOptions' $true); (pu ')'); (pu ':'); (ty 'PagedAsyncIterableIterator' $true); (pu '<'); (ty 'Widget'); (pu '>'); (pu ';') )),
        (rl "JavaScript.$xp.Widgets.analyzeWidget" "$xp.Widgets.analyzeWidget" @( (mb 'analyzeWidget'); (pu '('); (tx 'widgetId'); (pu ':'); (ty 'string' $true); (pu ')'); (pu ':'); (ty 'Promise' $true); (pu '<'); (ty 'WidgetAnalysisResult'); (pu '>'); (pu ';') )),
        (cl "JavaScript.$xp.Widgets")
    )),
    (el),
    (rl "JavaScript.$xp.Widget" "$xp.Widget" @( (kw 'export'); (kw 'interface' $true); (ty 'Widget' $true); (pu ' {' $true) ) @(
        (rl "JavaScript.$xp.Widget.id"             '' @( (kw 'readonly'); (tx 'id' $true); (pu ':'); (ty 'string' $true);          (pu ';') )),
        (rl "JavaScript.$xp.Widget.name"           '' @( (tx 'name');              (pu ':'); (ty 'string' $true);          (pu ';') )),
        (rl "JavaScript.$xp.Widget.color"          '' @( (tx 'color');             (pu ':'); (ty 'KnownWidgetColor' $true); (pu ';') )),
        (rl "JavaScript.$xp.Widget.description"    '' @( (tx 'description');       (pu '?:'); (ty 'string' $true);         (pu ';') )),
        (rl "JavaScript.$xp.Widget.manufacturedOn" '' @( (tx 'manufacturedOn');    (pu '?:'); (ty 'Date' $true);           (pu ';') )),
        (cl "JavaScript.$xp.Widget")
    )),
    (el),
    (rl "JavaScript.$xp.Versions" "$xp.Versions" @( (kw 'export'); (kw 'enum' $true); (ty 'ApiVersion' $true); (pu ' {' $true) ) @(
        (rl "JavaScript.$xp.Versions.v2024_01_01" "$xp.Versions.v2024_01_01" @( (tx 'V20240101'); (pu ' ='); (st '"2024-01-01"' $true); (pu ',') )),
        (cl "JavaScript.$xp.Versions")
    )),
    (el),
    (rl "JavaScript.$xp.WidgetAnalysisResult" "$xp.WidgetAnalysisResult" @( (kw 'export'); (kw 'interface' $true); (ty 'WidgetAnalysisResult' $true); (pu ' {' $true) ) @(
        (rl "JavaScript.$xp.WidgetAnalysisResult.widgetId"     '' @( (tx 'widgetId');     (pu ':'); (ty 'string' $true); (pu ';') )),
        (rl "JavaScript.$xp.WidgetAnalysisResult.qualityScore" '' @( (tx 'qualityScore'); (pu ':'); (ty 'number' $true); (pu ';') )),
        (rl "JavaScript.$xp.WidgetAnalysisResult.summary"      '' @( (tx 'summary');      (pu ':'); (ty 'string' $true); (pu ';') )),
        (cl "JavaScript.$xp.WidgetAnalysisResult")
    )),
    (el),
    (rl "JavaScript.$xp.WidgetColor" "$xp.WidgetColor" @( (kw 'export'); (kw 'const' $true); (ty 'KnownWidgetColor' $true); (pu ' = {' $true) ) @(
        (rl "JavaScript.$xp.WidgetColor.string" "$xp.WidgetColor.string" @( (ty 'string') )),
        (rl "JavaScript.$xp.WidgetColor.Red"    "$xp.WidgetColor.Red"    @( (tx 'Red');   (pu ':'); (st '"Red"' $true);   (pu ',') )),
        (rl "JavaScript.$xp.WidgetColor.Green"  "$xp.WidgetColor.Green"  @( (tx 'Green'); (pu ':'); (st '"Green"' $true); (pu ',') )),
        (rl "JavaScript.$xp.WidgetColor.Blue"   "$xp.WidgetColor.Blue"   @( (tx 'Blue');  (pu ':'); (st '"Blue"' $true) )),
        (cl "JavaScript.$xp.WidgetColor")
    )),
    (el),
    (rl "JavaScript.$xp.WidgetListParams" "$xp.WidgetListParams" @( (kw 'export'); (kw 'interface' $true); (ty 'WidgetQueryParamProperties' $true); (pu ' {' $true) ) @(
        (rl "JavaScript.$xp.WidgetListParams.color"      '' @( (tx 'color');      (pu '?:'); (ty 'KnownWidgetColor' $true); (pu ';') )),
        (rl "JavaScript.$xp.WidgetListParams.maxResults" '' @( (tx 'maxResults'); (pu '?:'); (ty 'number' $true);           (pu ';') )),
        (cl "JavaScript.$xp.WidgetListParams")
    ))
)
Write-TokenFile 'JavaScript' '@azure/samples-crosslanguage' '1.0.0' 'JavaScript.azuresamples-crosslanguage.json' $jsLines

# ===========================================================================
# C#
# ===========================================================================
$csLines = @(
    (rl "CSharp.$xp" $xp @( (kw 'namespace'); (ty 'Azure.Samples.CrossLanguage' $true) )),
    (el),
    (rl "CSharp.$xp.Widgets" "$xp.Widgets" @( (kw 'public'); (kw 'class' $true); (ty 'WidgetServiceClient' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.Widgets.getWidget" "$xp.Widgets.getWidget" @( (kw 'public'); (kw 'virtual' $true); (ty 'Response' $true); (pu '<'); (ty 'Widget'); (pu '>'); (mb 'GetWidget' $true); (pu '('); (ty 'string'); (tx 'widgetId' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (rl "CSharp.$xp.Widgets.createOrReplaceWidget" "$xp.Widgets.createOrReplaceWidget" @( (kw 'public'); (kw 'virtual' $true); (ty 'Response' $true); (pu '<'); (ty 'Widget'); (pu '>'); (mb 'CreateOrReplaceWidget' $true); (pu '('); (ty 'string'); (tx 'widgetId' $true); (pu ','); (ty 'Widget' $true); (tx 'widget' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (rl "CSharp.$xp.Widgets.updateWidget" "$xp.Widgets.updateWidget" @( (kw 'public'); (kw 'virtual' $true); (ty 'Response' $true); (pu '<'); (ty 'Widget'); (pu '>'); (mb 'UpdateWidget' $true); (pu '('); (ty 'string'); (tx 'widgetId' $true); (pu ','); (ty 'Widget' $true); (tx 'widget' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (rl "CSharp.$xp.Widgets.deleteWidget" "$xp.Widgets.deleteWidget" @( (kw 'public'); (kw 'virtual' $true); (ty 'Response' $true); (mb 'DeleteWidget' $true); (pu '('); (ty 'string'); (tx 'widgetId' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (rl "CSharp.$xp.Widgets.listWidgets" "$xp.Widgets.listWidgets" @( (kw 'public'); (kw 'virtual' $true); (ty 'Pageable' $true); (pu '<'); (ty 'Widget'); (pu '>'); (mb 'GetWidgets' $true); (pu '('); (ty 'WidgetColor'); (pu '?'); (tx 'color' $true); (pu ' ='); (kw 'null' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (rl "CSharp.$xp.Widgets.analyzeWidget" "$xp.Widgets.analyzeWidget" @( (kw 'public'); (kw 'virtual' $true); (ty 'Response' $true); (pu '<'); (ty 'WidgetAnalysisResult'); (pu '>'); (mb 'AnalyzeWidget' $true); (pu '('); (ty 'string'); (tx 'widgetId' $true); (pu ','); (ty 'CancellationToken' $true); (tx 'cancellationToken' $true); (pu ' ='); (kw 'default' $true); (pu ')'); (pu ';') )),
        (cl "CSharp.$xp.Widgets")
    )),
    (el),
    (rl "CSharp.$xp.Widget" "$xp.Widget" @( (kw 'public'); (kw 'class' $true); (ty 'Widget' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.Widget.id"             '' @( (kw 'public'); (ty 'string' $true);        (mb 'Id' $true);             (pu '{'); (kw 'get'); (pu '}') )),
        (rl "CSharp.$xp.Widget.name"           '' @( (kw 'public'); (ty 'string' $true);        (mb 'Name' $true);           (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (rl "CSharp.$xp.Widget.color"          '' @( (kw 'public'); (ty 'WidgetColor' $true);   (mb 'Color' $true);          (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (rl "CSharp.$xp.Widget.description"    '' @( (kw 'public'); (ty 'string' $true);  (pu '?'); (mb 'Description' $true);   (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (rl "CSharp.$xp.Widget.manufacturedOn" '' @( (kw 'public'); (ty 'DateTimeOffset' $true); (pu '?'); (mb 'ManufacturedOn' $true); (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (cl "CSharp.$xp.Widget")
    )),
    (el),
    (rl "CSharp.$xp.Versions" "$xp.Versions" @( (kw 'public'); (kw 'class' $true); (ty 'WidgetServiceClientOptions' $true); (pu ':'); (ty 'ClientOptions' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.Versions.inner" '' @( (kw 'public'); (kw 'enum' $true); (ty 'ServiceVersion' $true); (pu ' {' $true) ) @(
            (rl "CSharp.$xp.Versions.v2024_01_01" "$xp.Versions.v2024_01_01" @( (tx 'V2024_01_01'); (pu ' = 1') )),
            (cl)
        )),
        (cl "CSharp.$xp.Versions")
    )),
    (el),
    (rl "CSharp.$xp.WidgetAnalysisResult" "$xp.WidgetAnalysisResult" @( (kw 'public'); (kw 'class' $true); (ty 'WidgetAnalysisResult' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.WidgetAnalysisResult.widgetId"     '' @( (kw 'public'); (ty 'string' $true); (mb 'WidgetId' $true);     (pu '{'); (kw 'get'); (pu '}') )),
        (rl "CSharp.$xp.WidgetAnalysisResult.qualityScore" '' @( (kw 'public'); (ty 'float' $true);  (mb 'QualityScore' $true); (pu '{'); (kw 'get'); (pu '}') )),
        (rl "CSharp.$xp.WidgetAnalysisResult.summary"      '' @( (kw 'public'); (ty 'string' $true); (mb 'Summary' $true);      (pu '{'); (kw 'get'); (pu '}') )),
        (cl "CSharp.$xp.WidgetAnalysisResult")
    )),
    (el),
    (rl "CSharp.$xp.WidgetColor" "$xp.WidgetColor" @( (kw 'public'); (kw 'enum' $true); (ty 'WidgetColor' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.WidgetColor.string" "$xp.WidgetColor.string" @( (ty 'string') )),
        (rl "CSharp.$xp.WidgetColor.Red"    "$xp.WidgetColor.Red"    @( (mb 'Red');   (pu ',') )),
        (rl "CSharp.$xp.WidgetColor.Green"  "$xp.WidgetColor.Green"  @( (mb 'Green'); (pu ',') )),
        (rl "CSharp.$xp.WidgetColor.Blue"   "$xp.WidgetColor.Blue"   @( (mb 'Blue') )),
        (cl "CSharp.$xp.WidgetColor")
    )),
    (el),
    (rl "CSharp.$xp.WidgetListParams" "$xp.WidgetListParams" @( (kw 'public'); (kw 'class' $true); (ty 'GetWidgetsOptions' $true); (pu ' {' $true) ) @(
        (rl "CSharp.$xp.WidgetListParams.color"      '' @( (kw 'public'); (ty 'WidgetColor' $true); (pu '?'); (mb 'Color' $true);      (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (rl "CSharp.$xp.WidgetListParams.maxResults" '' @( (kw 'public'); (ty 'int' $true);         (pu '?'); (mb 'MaxResults' $true);  (pu '{'); (kw 'get'); (pu ';'); (kw 'set'); (pu ';}') )),
        (cl "CSharp.$xp.WidgetListParams")
    ))
)
Write-TokenFile 'C#' 'Azure.Samples.CrossLanguage' '1.0.0' 'C#.Azure_Samples_CrossLanguage.json' $csLines

# ===========================================================================
# GO
# ===========================================================================
$goLines = @(
    (rl "Go.$xp" $xp @( (kw 'package'); (tx 'azcrosslanguage' $true) )),
    (el),
    (rl "Go.$xp.Widgets" "$xp.Widgets" @( (kw 'type'); (ty 'WidgetsClient' $true); (kw 'struct' $true); (pu ' {}') ) @()),
    (el),
    (rl "Go.$xp.Widgets.getWidget" "$xp.Widgets.getWidget" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'GetWidget' $true); (pu '('); (tx 'ctx'); (ty 'context.Context' $true); (pu ','); (tx 'widgetID' $true); (ty 'string' $true); (pu ','); (tx 'options' $true); (pu ' *'); (ty 'GetWidgetOptions'); (pu ')'); (pu ' ('); (ty 'WidgetsClientGetWidgetResponse'); (pu ','); (ty 'error' $true); (pu ')') )),
    (rl "Go.$xp.Widgets.createOrReplaceWidget" "$xp.Widgets.createOrReplaceWidget" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'CreateOrReplaceWidget' $true); (pu '('); (tx 'ctx'); (ty 'context.Context' $true); (pu ','); (tx 'widgetID' $true); (ty 'string' $true); (pu ','); (tx 'widget' $true); (ty 'Widget' $true); (pu ')'); (pu ' ('); (ty 'WidgetsClientCreateOrReplaceWidgetResponse'); (pu ','); (ty 'error' $true); (pu ')') )),
    (rl "Go.$xp.Widgets.updateWidget" "$xp.Widgets.updateWidget" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'UpdateWidget' $true); (pu '('); (tx 'ctx'); (ty 'context.Context' $true); (pu ','); (tx 'widgetID' $true); (ty 'string' $true); (pu ','); (tx 'widget' $true); (ty 'Widget' $true); (pu ')'); (pu ' ('); (ty 'WidgetsClientUpdateWidgetResponse'); (pu ','); (ty 'error' $true); (pu ')') )),
    (rl "Go.$xp.Widgets.deleteWidget" "$xp.Widgets.deleteWidget" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'DeleteWidget' $true); (pu '('); (tx 'ctx'); (ty 'context.Context' $true); (pu ','); (tx 'widgetID' $true); (ty 'string' $true); (pu ')'); (pu ' ('); (ty 'WidgetsClientDeleteWidgetResponse'); (pu ','); (ty 'error' $true); (pu ')') )),
    (rl "Go.$xp.Widgets.listWidgets" "$xp.Widgets.listWidgets" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'ListWidgets' $true); (pu '('); (tx 'options' $true); (pu ' *'); (ty 'ListWidgetsOptions'); (pu ')'); (pu ' *'); (ty 'runtime.Pager'); (pu '['); (ty 'WidgetsClientListWidgetsResponse'); (pu ']') )),
    (rl "Go.$xp.Widgets.analyzeWidget" "$xp.Widgets.analyzeWidget" @( (kw 'func'); (pu '('); (tx 'c'); (pu ' *'); (ty 'WidgetsClient'); (pu ')'); (mb 'AnalyzeWidget' $true); (pu '('); (tx 'ctx'); (ty 'context.Context' $true); (pu ','); (tx 'widgetID' $true); (ty 'string' $true); (pu ')'); (pu ' ('); (ty 'WidgetsClientAnalyzeWidgetResponse'); (pu ','); (ty 'error' $true); (pu ')') )),
    (el),
    (rl "Go.$xp.Widget" "$xp.Widget" @( (kw 'type'); (ty 'Widget' $true); (kw 'struct' $true); (pu ' {' $true) ) @(
        (rl "Go.$xp.Widget.id"             '' @( (mb 'ID');            (ty 'string' $true) )),
        (rl "Go.$xp.Widget.name"           '' @( (mb 'Name');          (ty 'string' $true) )),
        (rl "Go.$xp.Widget.color"          '' @( (mb 'Color');         (ty 'WidgetColor' $true) )),
        (rl "Go.$xp.Widget.description"    '' @( (mb 'Description');   (pu ' *'); (ty 'string') )),
        (rl "Go.$xp.Widget.manufacturedOn" '' @( (mb 'ManufacturedOn'); (pu ' *'); (ty 'time.Time') )),
        (cl "Go.$xp.Widget")
    )),
    (el),
    (rl "Go.$xp.Versions" "$xp.Versions" @( (kw 'type'); (ty 'ServiceVersion' $true); (ty 'string' $true) ) @()),
    (rl "Go.$xp.Versions.v2024_01_01" "$xp.Versions.v2024_01_01" @( (kw 'const'); (mb 'ServiceVersionV20240101' $true); (ty 'ServiceVersion' $true); (pu ' ='); (st '"2024-01-01"' $true) )),
    (el),
    (rl "Go.$xp.WidgetAnalysisResult" "$xp.WidgetAnalysisResult" @( (kw 'type'); (ty 'WidgetAnalysisResult' $true); (kw 'struct' $true); (pu ' {' $true) ) @(
        (rl "Go.$xp.WidgetAnalysisResult.widgetId"     '' @( (mb 'WidgetID');     (ty 'string' $true) )),
        (rl "Go.$xp.WidgetAnalysisResult.qualityScore" '' @( (mb 'QualityScore'); (ty 'float32' $true) )),
        (rl "Go.$xp.WidgetAnalysisResult.summary"      '' @( (mb 'Summary');      (ty 'string' $true) )),
        (cl "Go.$xp.WidgetAnalysisResult")
    )),
    (el),
    (rl "Go.$xp.WidgetColor" "$xp.WidgetColor" @( (kw 'type'); (ty 'WidgetColor' $true); (ty 'string' $true) ) @()),
    (rl "Go.$xp.WidgetColor.string" "$xp.WidgetColor.string" @( (ty 'string') )),
    (rl "Go.$xp.WidgetColor.consts" '' @( (kw 'const') ) @(
        (rl "Go.$xp.WidgetColor.Red"   "$xp.WidgetColor.Red"   @( (mb 'WidgetColorRed');   (ty 'WidgetColor' $true); (pu ' ='); (st '"Red"' $true) )),
        (rl "Go.$xp.WidgetColor.Green" "$xp.WidgetColor.Green" @( (mb 'WidgetColorGreen'); (ty 'WidgetColor' $true); (pu ' ='); (st '"Green"' $true) )),
        (rl "Go.$xp.WidgetColor.Blue"  "$xp.WidgetColor.Blue"  @( (mb 'WidgetColorBlue');  (ty 'WidgetColor' $true); (pu ' ='); (st '"Blue"' $true) )),
        (cl "Go.$xp.WidgetColor")
    )),
    (el),
    (rl "Go.$xp.WidgetListParams" "$xp.WidgetListParams" @( (kw 'type'); (ty 'WidgetsClientListWidgetsOptions' $true); (kw 'struct' $true); (pu ' {' $true) ) @(
        (rl "Go.$xp.WidgetListParams.color"      '' @( (mb 'Color');      (pu ' *'); (ty 'WidgetColor') )),
        (rl "Go.$xp.WidgetListParams.maxResults" '' @( (mb 'MaxResults'); (pu ' *'); (ty 'int32') )),
        (cl "Go.$xp.WidgetListParams")
    ))
)
Write-TokenFile 'Go' 'azcrosslanguage' 'v1.0.0' 'Go.azcrosslanguage.json' $goLines

Write-Host ""
Write-Host "==> All done!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Upload order to APIView ($($Env:COMPUTERNAME)):" -ForegroundColor Cyan
Write-Host "  1. TypeSpec review  → output\Azure.Samples.CrossLanguage.zip" -ForegroundColor White
Write-Host "     (Creates the project + expected packages for all 5 languages)" -ForegroundColor DarkGray
Write-Host "  2. Per-language reviews → output\<Language>.*.json files" -ForegroundColor White
Write-Host "     (Each will be linked to the project via CrossLanguagePackageId)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Tip: Replace the stub .json files with real SDK-generated token files" -ForegroundColor Yellow
Write-Host "     for accurate API surface testing." -ForegroundColor Yellow


