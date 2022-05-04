#!/usr/bin/env pwsh

# See https://helm.sh/docs/topics/plugins/#downloader-plugins
param(
    [string]$certFile,
    [string]$keyFile,
    [string]$caFile,
    [string]$url
)

$path = $url -replace "file://"
Get-Content "$path" -Raw
