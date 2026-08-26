[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
  $PSNativeCommandUseErrorActionPreference = $true
}

$issues_repo = "Azure/azure-sdk-pr"

# Should there be more than --limit issues at once (unlikely),
# given that we close them after processing,
# the next batch will be picked up during the next run.
$issues = `
    gh issue list `
      --repo "$issues_repo" `
      --label "product-onboarding" `
      --state open `
      --limit 300 `
      --json number `
  | ConvertFrom-Json `
  | Select-Object -ExpandProperty number

$issues_count = 0
if ($null -ne $issues) {
  $issues_count = $issues.Count
}
[Console]::Error.WriteLine("Found $issues_count issues to process.")

foreach ($issue_number in $issues) {
  $issue = gh issue view $issue_number --repo "$issues_repo"
  $issue = $issue -join "`n"

  $product_id = ""
  $product_name = ""
  $product_type = ""
  $product_lifecycle = ""
  $service_id = ""
  $service_name = ""
  $needs_sdk_str = ""
  $data_plane = "N/A"
  $mgmt_plane = "N/A"
  $submitter = ""

  if ($issue -match "\#\#\#\sProduct\sID.*\n.*\n(?<ProductID>[^\n]+)") {
    $product_id = $matches["ProductID"]
    $product_id = $product_id.Trim()
  }

  if ($issue -match "\#\#\#\sProduct\sName.*\n.*\n(?<ProductName>[^\n]+)") {
    $product_name = $matches["ProductName"]
    $product_name = $product_name.Trim()
  }

  if ($issue -match "\#\#\#\sProduct\sType.*\n.*\n(?<ProductType>[^\n]+)") {
    $product_type = $matches["ProductType"]
    $product_type = $product_type.Trim()
  }

  if ($issue -match "\#\#\#\sProduct\sLifecycle.*\n.*\n(?<ProductLifecycle>[^\n]+)") {
    $product_lifecycle = $matches["ProductLifecycle"]
    $product_lifecycle = $product_lifecycle.Trim()
  }

  if ($issue -match "\#\#\#\sService\sID.*\n.*\n(?<ServiceID>[^\n]+)") {
    $service_id = $matches["ServiceID"]
    $service_id = $service_id.Trim()
  }

  if ($issue -match "\#\#\#\sService\sName.*\n.*\n(?<ServiceName>[^\n]+)") {
    $service_name = $matches["ServiceName"]
    $service_name = $service_name.Trim()
  }

  if ($issue -match "\#\#\#\s.*Azure\sSDK.*\n.*\n(?<NeedsSDK>[^\n]+)") {
    $needs_sdk_str = $matches["NeedsSDK"]
    $needs_sdk_str = $needs_sdk_str.Trim()
  }

  if ($issue -match "\#\#\#\s.*[Dd]ata\s[Pp]lane.*\n.*\n(?<DataPlane>[^\n]+)") {
    $data_plane = $matches["DataPlane"]
    $data_plane = $data_plane.Trim()
    if ($data_plane -eq "None") {
        $data_plane = ""
    }
  }

  if ($issue -match "\#\#\#\s.*[Mm]anagement\s[Pp]lane.*\n.*\n(?<MgmtPlane>[^\n]+)") {
    $mgmt_plane = $matches["MgmtPlane"]
    $mgmt_plane = $mgmt_plane.Trim()
    if ($mgmt_plane -eq "None") {
        $mgmt_plane = ""
    }
  }

  if ($issue -match "\#\#\#\sSubmitter.*\n.*\n(?<Submitter>[^\n]+)") {
    $submitter = $matches["Submitter"]
    $submitter = $submitter.Trim()
  }

  if (
         ($product_id        -ne "") `
    -and ($product_name      -ne "") `
    -and ($product_type      -ne "") `
    -and ($product_lifecycle -ne "") `
    -and ($service_id        -ne "") `
    -and ($service_name      -ne "") `
    -and ($needs_sdk_str     -ne "") `
    -and ($data_plane        -ne "") `
    -and ($mgmt_plane        -ne "") `
    -and ($submitter         -ne "") `
  ) {
    $needs_sdk_bool = ($needs_sdk_str -ieq "Yes")
    [Console]::Error.WriteLine("Processing issue #$issue_number.")

    $sync_success = $false
    try {
      & (Join-Path $PSScriptRoot "Sync-ProductOnboardingStatus.ps1") `
        -ProductID        "$product_id" `
        -ProductName      "$product_name" `
        -ProductType      "$product_type" `
        -ProductLifecycle "$product_lifecycle" `
        -ServiceID        "$service_id" `
        -ServiceName      "$service_name" `
        -NeedsSDK          $needs_sdk_bool `
        -DataPlane        "$data_plane" `
        -MgmtPlane        "$mgmt_plane" `
        -Submitter        "$submitter"

      $sync_success = $true
    } catch {
      [Console]::Error.WriteLine("Error syncing product onboarding status: $_")
      [Console]::Error.WriteLine($_.Exception.StackTrace)
    }

    if ($sync_success) {
      gh issue comment $issue_number --body "Product onboarding status synced successfully." --repo "$issues_repo"
      gh issue close $issue_number --repo "$issues_repo"
    }
  } else {
    [Console]::Error.WriteLine("Error parsing issue #$issue_number.")
  }
}
