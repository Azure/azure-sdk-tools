# Updates the Ref in all Yml files in the Repository
param (
    [String]$BuildSourcesDirectory
)

Install-Module -Name powershell-yaml -RequiredVersion 0.4.2 -Force -Scope CurrentUser
$Repos = @('azure-sdk-for-net') #, 'azure-sdk-for-js', 'azure-sdk-for-java', 'azure-sdk-for-python')

# Get Latest tag
$CurrentDate = Get-Date -Format "yyyyMMdd"
pushd "$BuildSourcesDirectory\azure-sdk-tools"
$toolsRepoTags = @(git tag -l "azure-sdk-tools_20200530.*" | Sort-Object -Descending)
Write-Host ($toolsRepoTags | Format-Table | Out-String)
$toolsRepoLatestTag = $toolsRepoTags[0]
popd
pushd "$BuildSourcesDirectory\azure-sdk-build-tools"
$buildToolsRepoTags = @(git tag -l "azure-sdk-build-tools_20200530.*" | Sort-Object -Descending)
Write-Host ($buildToolsRepoTags | Format-Table | Out-String)
$buildToolsRepoLatestTag = $buildToolsRepoTags[0]
popd

Write-Host "Tools tag $toolsRepoLatestTag"
Write-Host "Build Tools tag $buildToolsRepoLatestTag"

foreach ($repo in $Repos)
{
    pushd "$BuildSourcesDirectory\$repo"
    $ymlFiles = Get-ChildItem -Path . -File -Include *.yml -Recurse

    foreach ($file in $ymlFiles)
    {
        $ymlObj = ConvertFrom-Yaml (Get-Content $file.FullName -Raw) -Ordered
        if ($ymlObj.Contains("resources"))
        {
            $resources = $ymlObj["resources"]
            if ($resources.Contains("repositories"))
            {

                $repositories = $resources["repositories"]
                foreach ($repository in $repositories)
                {
                    if ($repository["repository"] -eq "azure-sdk-tools")
                    {
                        $repository["ref"] = "refs/tags/$toolsRepoLatestTag"
                    }
                    if ($repository["repository"] -eq "azure-sdk-build-tools")
                    {
                        $repository["ref"] = "refs/tags/$buildToolsRepoLatestTag"
                    }
                }

                ConvertTo-Yaml $ymlObj -OutFile $file.FullName -Force
            }
        }
    }

    popd
}