#!/usr/bin/env pwsh

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory=$True)]
    [String]$Path,

    [Parameter(Mandatory=$True)]
    [String]$PipelineId,

    [Parameter(Mandatory=$False)]
    [String]$Branch = "master",

    [Parameter(Mandatory=$False)]
    [String]$Project = "internal",

    [Parameter(Mandatory=$False)]
    [String]$OutputPath = "output.yml"
)

function GetAuthString() {
    $pass = $env:PATVAR
    if (!$pass) {
        throw "PATVAR environment variable must contain azure pipelines personal access token"
    }
    $pair = ":$($pass)"
    $encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
    $basicAuthValue = "Basic $encodedCreds"
    return $basicAuthValue
}

function BuildBody() {
    $body = @{
        resources = @{
            repositories = @{
                self = @{ refName = "$($Branch)" }
            }
        };
        PreviewRun = $true;
        YamlOverride = [string](Get-Content -Raw $Path)
    }

    return $body | ConvertTo-Json -Depth 10
}

function BuildRequest() {
    $basicAuthValue = GetAuthString
    $headers = @{ Authorization = $basicAuthValue }
    $uri = "https://dev.azure.com/azure-sdk/$($Project)/_apis/pipelines/$($PipelineId)/runs?api-version=6.0-preview.1"
    $body = BuildBody
    $contentType = "application/json"
    return $headers, $uri, $body, $contentType
}


$headers, $uri, $body, $contentType = BuildRequest

try {
    $resp = Invoke-WebRequest -Headers $headers -Uri $uri -Body $body -ContentType $contentType -Method Post
} catch {
    Write-Error ($_.ErrorDetails.Message | ConvertFrom-Json | Select-Object -ExpandProperty message)
    exit 1
}

$resp.Content | ConvertFrom-Json | Select-Object -ExpandProperty finalYaml | Out-File $($OutputPath)
Write-Host "YAML is valid! Generated pipeline written to $($OutputPath)"

<#
.SYNOPSIS
Validates a Azure Pipelines yaml file via the Azure Pipelines API.

.DESCRIPTION
This script submits pipelines yaml to the server-side API for advanced validation which includes parameter expansion.
It writes the generated yaml to a file.
For basic pipelines yaml schema validation, see the VSCode extension: https://github.com/Microsoft/azure-pipelines-vscode

.PARAMETER Path
Path to the pipelines yaml file

.PARAMETER PipelineId
The Pipeline ID (number) that the pipeline yaml is defined for. You can retrieve the numeric ID by navigating
to the pipeline in the UI, and grabbing it from the url: https://dev.azure.com/azure-sdk/internal/_build?definitionId=<PIPELINE_ID>&_a=summary

.PARAMETER Branch
The Pipelines API supports validating any additional yaml templates included by the pipeline file.
In order to do so, these files must be checked in to the repository.
In this case -Branch must be specified to reference those changes.
Referencing external repositories for validation is not supported by the Pipelines API.

.PARAMETER Project
Optional Azure DevOps project the pipeline is defined under. Defaults to 'internal'.

.PARAMETER OutputPath
Optional output path for the generated pipeline yaml. Defaults to 'output.yml'.

.EXAMPLE
Test changes to a pipeline file against master:

$env:PATVAR = "<personal access token>"
./scripts/powershell/Validate-PipelineYaml.ps1 `
    -Path ./eng/pipelines/pipeline-generation.yml `
    -Pipeline 773

.EXAMPLE
Test changes to a pipeline file against a branch with extra template changes:

$env:PATVAR = "<personal access token>"
./scripts/powershell/Validate-PipelineYaml.ps1 `
    -Path ./eng/pipelines/pipeline-generation.yml `
    -Pipeline 773 `
    -Branch <your branch>

#>
