# Invoke-Pester .\CLIIntegration.Tests.ps1 -PassThru
BeforeAll {
    . $PSScriptRoot/assets.Tests.Helpers.ps1
    . $PSScriptRoot/../../../../eng/common/scripts/common.ps1

    $TestProxyExe = "test-proxy"

    # By default, this test set runs against the `test-proxy` CLI tool
    # if the environment variable CLI_TEST_WITH_DOCKER is set to "true", run the tests in DOCKER mode.
    # this also means skipping a couple of the reset tests.
    if($env:CLI_TEST_WITH_DOCKER){
        $TestProxyExe = "docker"
    }
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
                Tag                  = "python/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)

            $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder

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
                Tag                  = "python/tables_9e81fb"
            }

            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
            $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
            $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
            $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
                Tag                  = "python/tables_fc54d0"
            }
            $files = @(
                "assets.json"
            )
            $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
            $assetsFile = Join-Path $testFolder "assets.json"
            $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
            $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
            $LASTEXITCODE | Should -Be 0
            $localAssetsFilePath = Get-AssetsFilePath -AssetsJsonContent $recordingJson -AssetsJsonFile $assetsFile
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1

            $CommandArgs = "reset --assets-json-path $assetsJsonRelativePath"
            Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
            # With no pending changes, the reset should leave everything alone
            Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
            Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
        }
        It "It should call Reset and prompt Yes to restore files" {
            # Write-Output doesn't cooperate with the docker run. Need to find a different method. Covered in #4374.
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $recordingJson = [PSCustomObject]@{
                    AssetsRepo           = "Azure/azure-sdk-assets-integration"
                    AssetsRepoPrefixPath = "pull/scenarios"
                    AssetsRepoId         = ""
                    TagPrefix            = "main"
                    Tag                  = "python/tables_fc54d0"
                }
                $files = @(
                    "assets.json"
                )
                $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
                $assetsFile = Join-Path $testFolder "assets.json"
                $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
                $CommandArgs = "reset --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder -WriteOutput "Y"
                Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 3
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 1
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file2.txt" -ExpectedVersion 1
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
            }
        }
        It "It should call Reset and prompt No to restore files" {
            # Write-Output doesn't cooperate with the docker run. Need to find a different method. Covered in #4374.
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $recordingJson = [PSCustomObject]@{
                    AssetsRepo           = "Azure/azure-sdk-assets-integration"
                    AssetsRepoPrefixPath = "pull/scenarios"
                    AssetsRepoId         = ""
                    TagPrefix            = "main"
                    Tag                  = "python/tables_fc54d0"
                }
                $files = @(
                    "assets.json"
                )
                $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files
                $assetsFile = Join-Path $testFolder "assets.json"
                $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
                $CommandArgs = "reset --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder -WriteOutput "N"
                Test-DirectoryFileCount -Directory $localAssetsFilePath -ExpectedNumberOfFiles 4
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file1.txt" -ExpectedVersion 2
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file3.txt" -ExpectedVersion 1
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file4.txt" -ExpectedVersion 1
                Test-FileVersion -FilePath $localAssetsFilePath -FileName "file5.txt" -ExpectedVersion 1
            }
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
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $recordingJson = [PSCustomObject]@{
                    AssetsRepo           = "Azure/azure-sdk-assets-integration"
                    AssetsRepoPrefixPath = "pull/scenarios"
                    AssetsRepoId         = ""
                    TagPrefix            = "language/tables"
                    Tag                  = "python/tables_fc54d0"
                }
                $files = @(
                    "assets.json"
                )

                $originalTagPrefix = $recordingJson.TagPrefix
                $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $true
                # Ensure that the TagPrefix was updated for testing
                $originalTagPrefix | Should -not -Be $recordingJson.TagPrefix
                $assetsFile = Join-Path $testFolder "assets.json"
                $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder

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
                $CommandArgs = "push --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder

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
        }
        It "Should push new, updated and deleted files, original restore from second push of assets." {
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
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
                $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"

                # The initial restore/verification
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
                $CommandArgs = "push --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder

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
        }
        It "Should push new, updated and deleted files, original restore from third push of assets." {
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $recordingJson = [PSCustomObject]@{
                    AssetsRepo           = "Azure/azure-sdk-assets-integration"
                    AssetsRepoPrefixPath = "pull/scenarios"
                    AssetsRepoId         = ""
                    TagPrefix            = "language/tables"
                    Tag                  = "python/tables_9e81fb"
                }

                $files = @(
                    "assets.json"
                )
                $originalTagPrefix = $recordingJson.TagPrefix
                $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $true
                # Ensure that the TagPrefix was updated for testing
                $originalTagPrefix | Should -not -Be $recordingJson.TagPrefix
                $assetsFile = Join-Path $testFolder "assets.json"
                $assetsJsonRelativePath = [System.IO.Path]::GetRelativePath($testFolder, $assetsFile)
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"

                # The initial restore/verification
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
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
                $CommandArgs = "push --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder

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
        }
        It "Should handle pushing an identical SHA twice, and properly update to the necessary tag without error state." {
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $newTestFolder = ""
                try {
                    $testGuid = [Guid]::NewGuid()
                    $created_tag_prefix = "test_$testGuid"
                    $creationPath = Join-Path "sdk" "keyvault" "azure-keyvault-keys" "tests" "recordings"
                    $file1 = Join-Path $creationPath "file1.txt"
                    $file2 = Join-Path $creationPath "file2.txt"
                    $file3 = Join-Path $creationPath "file3.txt"
                    $recordingJson = [PSCustomObject]@{
                        AssetsRepo           = "Azure/azure-sdk-assets-integration"
                        AssetsRepoPrefixPath = ""
                        AssetsRepoId         = ""
                        TagPrefix            = $created_tag_prefix
                        Tag                  = ""
                    }
    
                    $assetsJsonRelativePath = Join-Path "sdk" "keyvault" "azure-keyvault-keys" "assets.json"
    
                    $files = @(
                        $assetsJsonRelativePath
                    )
                    $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $false
    
                    $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
                    $LASTEXITCODE | Should -Be 0
    
                    $localAssetsFilePath = Join-Path $testFolder ".assets"
                    $assetsFolder = $(Get-ChildItem $localAssetsFilePath -Directory)[0].FullName
                    mkdir -p $(Join-Path $assetsFolder $creationPath)
    
                    # Create new files. These are in a predictable location with predicatable content so we can generate the same SHA twice in a row.
                    Edit-FileVersion -FilePath $assetsFolder -FileName $file1 -Version 1
                    Edit-FileVersion -FilePath $assetsFolder -FileName $file2 -Version 1
                    Edit-FileVersion -FilePath $assetsFolder -FileName $file3 -Version 1
                    
                    # Push the changes
                    $CommandArgs = "push --assets-json-path $assetsJsonRelativePath"
                    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
                    $LASTEXITCODE | Should -Be 0

                    # now, let's describe an _entirely different_ assets folder, attempt the same push.
                    # this should result in the SAME tag twice in the assets repo, and we should properly NOT do the full push action
                    $newTestFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $false
                    $newAssetsFile = Join-Path $newTestFolder $assetsJsonRelativePath

                    $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $newTestFolder
                    $LASTEXITCODE | Should -Be 0

                    $newlocalAssetsFilePath = Join-Path $newTestFolder ".assets"
                    $newAssetsFolder = $(Get-ChildItem $newlocalAssetsFilePath -Directory)[0].FullName
                    mkdir -p $(Join-Path $newAssetsFolder $creationPath)

                    # same file updates. we should have an identical sha!
                    Edit-FileVersion -FilePath $newAssetsFolder -FileName $file1 -Version 1
                    Edit-FileVersion -FilePath $newAssetsFolder -FileName $file2 -Version 1
                    Edit-FileVersion -FilePath $newAssetsFolder -FileName $file3 -Version 1
                    
                    $CommandArgs = "push --assets-json-path $assetsJsonRelativePath"
                    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $newTestFolder
                    $LASTEXITCODE | Should -Be 0
                    
                    $updatedAssets = Update-AssetsFromFile -AssetsJsonContent $newAssetsFile
    
                    $exists = Test-TagExists -AssetsJsonContent $updatedAssets -WorkingDirectory $localAssetsFilePath
                    $exists | Should -Be $true
                }
                finally {
                    if ($newTestFolder) {
                        Remove-Test-Folder $newTestFolder
                    }
                }
            }
        }
        It "Should restore, make a change, then restore again and maintain the changed files." {
            if ($env:CLI_TEST_WITH_DOCKER) {
                Set-ItResult -Skipped
            }
            else {
                $testGuid = [Guid]::NewGuid()
                $created_tag_prefix = "test_$testGuid"
                $creationPath = Join-Path "sdk" "keyvault" "azure-keyvault-keys" "tests" "recordings"
                $file1 = Join-Path $creationPath "file1.txt"
                $file2 = Join-Path $creationPath "file2.txt"
                $file3 = Join-Path $creationPath "file3.txt"
                $recordingJson = [PSCustomObject]@{
                    AssetsRepo           = "Azure/azure-sdk-assets-integration"
                    AssetsRepoPrefixPath = ""
                    AssetsRepoId         = ""
                    TagPrefix            = $created_tag_prefix
                    Tag                  = ""
                }

                $assetsJsonRelativePath = Join-Path "sdk" "keyvault" "azure-keyvault-keys" "assets.json"

                $files = @(
                    $assetsJsonRelativePath
                )
                $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IsPushTest $false

                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
                $LASTEXITCODE | Should -Be 0

                $localAssetsFilePath = Join-Path $testFolder ".assets"
                $assetsFolder = $(Get-ChildItem $localAssetsFilePath -Directory)[0].FullName
                mkdir -p $(Join-Path $assetsFolder $creationPath)

                # Create new files. These are in a predictable location with predicatable content so we can generate the same SHA twice in a row.
                Edit-FileVersion -FilePath $assetsFolder -FileName $file1 -Version 3
                Edit-FileVersion -FilePath $assetsFolder -FileName $file2 -Version 3
                Edit-FileVersion -FilePath $assetsFolder -FileName $file3 -Version 3
                
                # now lets restore again
                $CommandArgs = "restore --assets-json-path $assetsJsonRelativePath"
                Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs -MountDirectory $testFolder
                $LASTEXITCODE | Should -Be 0

                # same file updates. we should still have these same files around!
                Test-FileVersion -FilePath $assetsFolder -FileName $file1 -ExpectedVersion 3
                Test-FileVersion -FilePath $assetsFolder -FileName $file2 -ExpectedVersion 3
                Test-FileVersion -FilePath $assetsFolder -FileName $file3 -ExpectedVersion 3
            }
        }
        AfterEach {
            Remove-Test-Folder $testFolder
            Remove-Integration-Tag $updatedAssets
        }
    }
}