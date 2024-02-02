<#
.SYNOPSIS
    Downloads Kusto schema from a Kusto cluster and saves it to the file system.
#>

param(
    [string]$ClusterUri = "https://azsdkengsys.westus2.kusto.windows.net",
    [string]$DatabaseName = "Pipelines",
    [string]$OutputPath = (Join-Path $PSScriptRoot "kusto")
)

$accessToken = az account get-access-token --resource "https://api.kusto.windows.net" --query "accessToken" --output tsv
$headers = @{ Authorization="Bearer $accessToken" }
$nl = [System.Environment]::NewLine

function InvokeKustoCommand($command) {
    $response = Invoke-RestMethod -Uri "$ClusterUri/v1/rest/mgmt" -Headers $headers -Method Post -Body (@{csl=$command; db=$DatabaseName} | ConvertTo-Json) -ContentType "application/json"
    $columns = $response.Tables[0].Columns
    $rows = $response.Tables[0].Rows
    
    $results = @()
    foreach ($row in $rows) {
        $obj = @{}
        for ($i = 0; $i -lt $columns.Length; $i++) {
            $obj[$columns[$i].ColumnName] = $row[$i]
        }
        $results += [PSCustomObject]$obj
    }
    return $results
}

function ToCamelCase($str) {
    return $str.Substring(0, 1).ToLower() + $str.Substring(1)
}

function Quote($str, $quoteChar = '"') {
    $str ??= ""
    return $quoteChar + $str.Replace('\', '\\').Replace($quoteChar, "\" + $quoteChar) + $quoteChar
}

function WriteKustoFile($kustoObject, $folder, $fileContents) {
    $parentFolder = Join-Path $OutputPath $folder
    
    if($kustoObject.Folder) {
        $parentFolder = Join-Path $parentFolder $kustoObject.Folder
    }

    if (-not (Test-Path $parentFolder -PathType Container)) {
        New-Item -ItemType Directory -Path $parentFolder | Out-Null
    }

    $filePath = Join-Path $parentFolder "$($kustoObject.Name).kql"

    Set-Content -Path $filePath -Value $fileContents -Encoding ASCII
}

function ExtractTables {
    $clusterSchema = (InvokeKustoCommand '.show database schema as json').DatabaseSchema | ConvertFrom-Json
    $tables = $clusterSchema.Databases.$DatabaseName.Tables.PSObject.Properties.Value

    foreach ($table in $tables) {
        $fileContents = (
                ".create-merge table $($table.Name) (",
                (($table.OrderedColumns | ForEach-Object { "    $($_.Name): $($_.CslType)" }) -join ",$nl"),
                ") with (folder=$(Quote $table.Folder "'"), docstring=$(Quote $table.DocString "'"))",
                "",
                ".create-or-alter table $($table.Name) ingestion json mapping '$($table.Name)`_mapping' ``````[",
                (($table.OrderedColumns | ForEach-Object { "    { `"column`": `"$($_.Name)`", `"path`": `"$['$(ToCamelCase $_.Name)']`" }" }) -join ",$nl"),
                "]``````"
        ) -join $nl

        WriteKustoFile $table "tables" $fileContents
    }
}

function ExtractFunctions {
    $functions = InvokeKustoCommand '.show functions'
    foreach ($function in $functions) {
        $fileContents = ".create-or-alter function with (folder=$(Quote $function.Folder "'"), docstring=$(Quote $function.DocString "'")) $($function.Name)$($function.Parameters)$nl$($function.Body)"
        WriteKustoFile $function "functions" $fileContents
    }
}

function ExtractViews {
    $materializedViews = InvokeKustoCommand '.show materialized-views'
    foreach ($view in $materializedViews) {
        $fileContents = (
                ".create-or-alter materialized-view with (folder=$(Quote $view.Folder "'"), docstring=$(Quote $view.DocString "'")) $($view.Name) on table $($view.SourceTable)",
                "{",
                $view.Query,
                "}"
        ) -join $nl

        WriteKustoFile $view "views" $fileContents
    }
}

ExtractTables
ExtractFunctions
ExtractViews
