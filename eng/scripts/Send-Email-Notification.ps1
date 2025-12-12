<#
.SYNOPSIS
    Sends an email notification using a specified Azure SDK email service endpoint.

.DESCRIPTION
    This script posts an email payload to the Azure SDK email service via a REST API call. 
    It accepts recipient details, subject, and body content, converts them to JSON, and sends 
    the request to the provided URI.

.PARAMETER AzureSDKEmailUri
    The Uri of the app used to send email notifications

.PARAMETER To
    The primary recipient email address(es).

.PARAMETER Cc
    Optional CC recipient email address(es).

.PARAMETER Subject
    The subject line of the email.

.PARAMETER Body
    The HTML or plain text body of the email message.
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $AzureSDKEmailUri, 

    [Parameter(Mandatory = $true)]
    [string] $To,

    [Parameter(Mandatory = $false)]
    [string] $CC,

    [Parameter(Mandatory = $true)]
    [string] $Subject,

    [Parameter(Mandatory = $true)]
    [string] $Body
)

Set-StrictMode -Version 3

try {
    $requestBody = @{ EmailTo = $To; CC = $CC; Subject = $Subject; Body = $Body} | ConvertTo-Json -Depth 3
    Write-Host "Sending Email - To: $To`nCC: $CC`nSubject: $Subject`nBody: $Body"
    $response = Invoke-RestMethod -Uri $AzureSDKEmailUri -Method Post -Body $requestBody -ContentType "application/json"
    Write-Host "Successfully Sent Email - To: $To`nCC: $CC`nSubject: $Subject`nBody: $Body"
} catch {
    Write-Error "Failed to send email.`nTo: $To`nCC: $CC`nSubject: $Subject`nBody: $Body`nException message: $($_.Exception.Message)"
}