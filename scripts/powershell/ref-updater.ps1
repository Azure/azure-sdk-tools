# Updates the Ref in all Yml files in the Repository
param (
    [String]$RepoRoot,
    [String]$Tag,
    [String]$ToolRepo
)

Install-Module -Name powershell-yaml -RequiredVersion 0.4.2 -Force -Scope CurrentUser

$ymlFiles = Get-ChildItem -Path $RepoRoot -File -Include *.yml -Recurse

foreach ($file in $ymlFiles)
{
    $ymlContent = Get-Content $file.FullName -Raw
    $ymlObj = ConvertFrom-Yaml $ymlContent -Ordered
    if ($ymlObj.Contains("resources"))
    {
        $resources = $ymlObj["resources"]
        if ($resources.Contains("repositories"))
        {

            $repositories = $resources["repositories"]
            foreach ($repository in $repositories)
            {
                if ($repository["repository"] -eq $ToolRepo)
                {
                    $repository["ref"] = "refs/tags/$Tag"
                }
            }

            $resourcesYml = ConvertTo-Yaml $resources
            $ymlContent = ($ymlContent -replace "(?ms)resources:.*repositories:.*repository.*?name:.*?\n|ref:.*?\n|endpoint:.*?\n|type:.*?\n", $resourcesYml)
            Set-Content -Path $file.FullName -Value $ymlContent -Force
        }
    }
}