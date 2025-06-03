
function FireAPIRequest($url, $method, $body = $null, $headers = $null, $rawFile = $null)
{
  $attempts = 1

  while($attempts -le 3)
  {
    try
    {
      if ($rawFile) {
        return Invoke-RestMethod -Method $method -Uri $url -Body $body -Headers $headers -Infile $rawFile
      }
      else {
        return Invoke-RestMethod -Method $method -Uri $url -Body $body -Headers $headers
      }
    }
    catch
    {
      $response = $_.Exception.Response

      $statusCode = $response.StatusCode.value__
      $statusDescription = $response.StatusDescription

      if($statusCode)
      {
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

      if ($attempts -ge 3)
      {
        Write-Host "Abandoning Request $url after 3 attempts."
        exit 1
      }

      Start-Sleep -s 10
    }

    $attempts += 1
  }
}

function GetReleaseId {
  param(
     [Parameter(mandatory=$true)]
     $ReleaseName
  )

  $url = "https://api.github.com/repos/$RepoId/releases/tags/$ReleaseName"

  $headers = @{
    "Authorization" = "Bearer $($env:GH_TOKEN)"
    "X-GitHub-Api-Version" = "2022-11-28"
    "Accept" = "application/vnd.github+json"
  }

  $release = FireAPIRequest -url $url -headers $headers -method "Get"

  return $release.id
}