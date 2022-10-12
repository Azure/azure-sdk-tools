# Invoke-Pester .\CLIIntegration.Tests.ps1 -PassThru
BeforeAll {
    . $PSScriptRoot/assets.Tests.Helpers.ps1
    . $PSScriptRoot/../../../eng/common/scripts/common.ps1

    # Each machine installs test-proxy.exe and adds it to the path. Verify that
    # the it's on the path prior to trying to run tests
    $TestProxyExe = "test-proxy"
    $proxyInPath = Test-Exe-In-Path($TestProxyExe)
    if (-not $proxyInPath) {
        LogError "$TestProxyExe was not found in the path. Please ensure the install has been done prior to running tests."
        exit(1)
    }
    Set-StrictMode -Version 3
}

Describe "AssetsModuleTests" {
    Context "RestoreAssetsRepoTests" -Tag "Integration" {
        BeforeEach {
            $testFolder = $null
        }
        It "Should restore from original push of assets." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
        }
        It "Should restore from second push of assets." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_9e81fb"
            }

            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 4
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
        }
        It "Should restore from third push of files." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_bb2223"
            }

            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -ExpectedVersion 1
        }
        AfterEach {
            Remove-Test-Folder $testFolder
        }
    }
    Context "ResetAssetsRepoTests" -Tag "Integration" {
        BeforeEach {
            $testFolder = $null
        }
        It "It should call Reset without prompt if no files have changed" {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1

            $CommandArgs = "reset --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            # With no pending changes, the reset should leave everything alone
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
        }
        It "It should call Reset and prompt Yes to restore files" {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1

            # Create a new file and verify
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -Version 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            # Update a file and verify
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -Version 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
            # Delete a file
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file2.txt"
            Remove-Item -Path $fileToRemove

            # Reset answering Y and they should all go back to original restore
            $CommandArgs = "reset --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs -WriteOutput "Y"
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
        }
        It "It should call Reset and prompt No to restore files" {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "main"
                Tag                  = "language/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1

            # Create two new files and verify
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -Version 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -Version 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -ExpectedVersion 1
            # Update a file and verify
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -Version 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
            # Delete a file
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file2.txt"
            Remove-Item -Path $fileToRemove

            # Reset answering N and they should remain changed as per the previous changes
            $CommandArgs = "reset --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs -WriteOutput "N"
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 4
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -ExpectedVersion 1
        }
        AfterEach {
            Remove-Test-Folder $testFolder
        }
    }
    Context "PushAssetsRepoTests" -Tag "Integration" {
        BeforeEach {
            $updatedAssets = $null
            $testFolder = $null
        }
        It "Should push new, updated and deleted files, original restore from first push of assets." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "language/tables"
                Tag                  = "language/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )

            $originalTagPrefix = $recordingJson.TagPrefix
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $true
            # Ensure that the TagPrefix was updated for testing
            $originalTagPrefix | Should -not -Be $recordingJson.TagPrefix
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"

            # The initial restore/verification
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1

            # Create a new file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -Version 1
            # Update the version on an existing file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -Version 2
            # Delete a file
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file2.txt"
            Remove-Item -Path $fileToRemove
            $assetsFile = Join-Path $testFolder "assets.json"

            # Push the changes
            $CommandArgs = "push --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs

            # Verify that after the push the directory still contains our updated files
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1

            $updatedAssets = Update-AssetsFromFile -AssetsJsonContent $assetsFile
            Write-Host "updatedAssets.Tag=$($updatedAssets.Tag), originalAssets.Tag=$($recordingJson.Tag)"
            $updatedAssets.Tag | Should -not -Be $recordingJson.Tag

            $exists = Test-TagExists -AssetsJsonContent $updatedAssets -WorkingDirectory $localAssetsFilePath
            $exists | Should -Be $true
        }
        It "Should push new, updated and deleted files, original restore from second push of assets." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "language/tables"
                Tag                  = "language/tables_bb2223"
            }

            $files = @(
                "assets.json"
            )
            $originalTagPrefix = $recordingJson.TagPrefix
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $true
            # Ensure that the TagPrefix was updated for testing
            $originalTagPrefix | Should -not -Be $recordingJson.TagPrefix
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"

            # The initial restore/verification
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -ExpectedVersion 1

            # Create a new file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file6.txt" -Version 1
            # Update the version on an existing file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -Version 3
            # Delete a file
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file5.txt"
            Remove-Item -Path $fileToRemove
            $assetsFile = Join-Path $testFolder "assets.json"

            # Push the changes
            $CommandArgs = "push --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs

            # Verify that after the push the directory still contains our updated files
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file6.txt" -ExpectedVersion 1

            $updatedAssets = Update-AssetsFromFile -AssetsJsonContent $assetsFile
            Write-Host "updatedAssets.Tag=$($updatedAssets.Tag), originalAssets.Tag=$($recordingJson.Tag)"
            $updatedAssets.Tag | Should -not -Be $recordingJson.Tag

            $exists = Test-TagExists -AssetsJsonContent $updatedAssets -WorkingDirectory $localAssetsFilePath
            $exists | Should -Be $true
        }
        It "Should push new, updated and deleted files, original restore from third push of assets." {
            $recordingJson = [PSCustomObject]@{
                AssetsRepo           = "Azure/azure-sdk-assets-integration"
                AssetsRepoPrefixPath = "pull/scenarios"
                AssetsRepoId         = ""
                TagPrefix            = "language/tables"
                Tag                  = "language/tables_9e81fb"
            }

            $files = @(
                "assets.json"
            )
            $originalTagPrefix = $recordingJson.TagPrefix
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $true
            # Ensure that the TagPrefix was updated for testing
            $originalTagPrefix | Should -not -Be $recordingJson.TagPrefix
            $assetsFile = Join-Path $testFolder "assets.json"
            $CommandArgs = "restore --assets-json-path $assetsFile"

            # The initial restore/verification
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 4
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1

            # Create a new file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file6.txt" -Version 1
            # Update the version on an existing file
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -Version 2
            Edit-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -Version 3
            # Delete files 3 & 4
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file3.txt"
            Remove-Item -Path $fileToRemove
            $fileToRemove = Join-Path -Path $localAssetsFilePath -ChildPath "file4.txt"
            Remove-Item -Path $fileToRemove
            $assetsFile = Join-Path $testFolder "assets.json"

            # Push the changes
            $CommandArgs = "push --assets-json-path $assetsFile"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe $CommandArgs

            # Verify that after the push the directory still contains our updated files
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file6.txt" -ExpectedVersion 1

            $updatedAssets = Update-AssetsFromFile -AssetsJsonContent $assetsFile
            Write-Host "updatedAssets.Tag=$($updatedAssets.Tag), originalAssets.Tag=$($recordingJson.Tag)"
            $updatedAssets.Tag | Should -not -Be $recordingJson.Tag

            $exists = Test-TagExists -AssetsJsonContent $updatedAssets -WorkingDirectory $localAssetsFilePath
            $exists | Should -Be $true
        }
        AfterEach {
            Remove-Test-Folder $testFolder
            Remove-Integration-Tag $updatedAssets
        }
    }
}