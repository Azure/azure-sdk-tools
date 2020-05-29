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
    $ymlObj = ConvertFrom-Yaml (Get-Content $file.FullName -Raw) -Ordered
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

            $Resources = ConvertTo-Yaml $resources
            Write-Host ($Resources | Format-Table | Out-String)
        }
    }
}