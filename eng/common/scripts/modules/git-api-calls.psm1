# Retrieves the list of all tags that exist on the target repository
function GetExistingTags($apiUrl) {
    try {
      return (Invoke-RestMethod -Method "GET" -Uri $apiUrl  ) | % { $_.ref.Replace("refs/tags/", "") }
    }
    catch {
      $statusCode = $_.Exception.Response.StatusCode.value__
      $statusDescription = $_.Exception.Response.StatusDescription
  
      Write-Host "Failed to retrieve tags from repository."
      Write-Host "StatusCode:" $statusCode
      Write-Host "StatusDescription:" $statusDescription
  
      # Return an empty list if there are no tags in the repo
      if ($statusCode -eq 404) {
        return @()
      }
  
      exit(1)
    }
}

function FireAPIRequest($url, $method, $body = $null, $headers = $null) {
    $attempts = 1
  
    while ($attempts -le 3) {
      try {
        return Invoke-RestMethod -Method $method -Uri $url -Body $body -Headers $headers
      }
      catch {
        $response = $_.Exception.Response
  
        $statusCode = $response.StatusCode.value__
        $statusDescription = $response.StatusDescription
  
        if ($statusCode) {
          Write-Host "API request attempt number $attempts to $url failed with statuscode $statusCode"
          Write-Host $statusDescription
  
          Write-Host "Rate Limit Details:"
          Write-Host "Total: $($response.Headers.GetValues("X-RateLimit-Limit"))"
          Write-Host "Remaining: $($response.Headers.GetValues("X-RateLimit-Remaining"))"
          Write-Host "Reset Epoch: $($response.Headers.GetValues("X-RateLimit-Reset"))"
        }
        else {
          Write-Host "API request attempt number $attempts to $url failed with no statuscode present, exception follows:"
          Write-Host $_.Exception.Response
          Write-Host $_.Exception
        }
  
        if ($attempts -ge 3) {
          Write-Host "Abandoning Request $url after 3 attempts."
          exit(1)
        }
  
        Start-Sleep -s 10
      }
  
      $attempts += 1
    }
}

Export-ModuleMember -Function 'GetExistingTags'
Export-ModuleMember -Function 'FireAPIRequest'