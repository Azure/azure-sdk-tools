# Helper functions for retireving useful information from azure-sdk-for-* repo
# Example Use : Import-Module .\eng\common\scripts\modules\Package-Properties\Package-Properties.psm1 -ArgumentList 'JavaScript','C:\Git\azure-sdk-for-js'
Param
(
    [Parameter(Position=0, Mandatory=$true)]
    [ValidateSet("net","java","js","python")]
    [string]
    $Language,
    [Parameter(Position=1, Mandatory=$true)]
    [string]
    $RepoRoot
)

function Extract-PkgProps ($pkgPath, $pkgName)
{
    if ($Language -eq "net") { Extract-DotNetPkgProps -pkgPath $pkgPath -pkgName $pkgName }
    if ($Language -eq "java") { Extract-JavaPkgProps -pkgPath $pkgPath -pkgName $pkgName }
    if ($Language -eq "js") { Extract-JsPkgProps -pkgPath $pkgPath -pkgName $pkgName }
    if ($Language -eq "python") { Extract-PythonPkgProps -pkgPath $pkgPath -pkgName $pkgName }
}

function Extract-DotNetPkgProps ($pkgPath, $pkgName)
{
    $projectPath = Join-Path $pkgPath "src" "$pkgName.csproj"
    if (Test-Path $projectPath)
    {
        $projectData = New-Object -TypeName XML
        $projectData.load($projectPath)

        $pkgVersion = Select-XML -Xml $projectData -XPath '/Project/PropertyGroup/Version'
        $pkgReadMePath = Join-Path $pkgPath "README.md"
        $pkgReadMePath = If (Test-Path ($pkgReadMePath)) {  $pkgReadMePath } Else { $null }
        $pkgChangeLogPath = Join-Path $pkgPath "CHANGELOG.md"
        $pkgChangeLogPath = If (Test-Path ($pkgChangeLogPath)) {  $pkgChangeLogPath } Else { $null }
        
        return @{
            pkgName = $pkgName;
            pkgVersion = $pkgVersion;
            pkgDirPath = $pkgPath;
            pkgReadMePath = $pkgReadMePath;
            pkgChangeLogPath = $pkgChangeLogPath;
        }
    } else 
    {
        return $null
    }
}

function Extract-JsPkgProps ($pkgPath, $pkgName)
{
    $projectPath = Join-Path $pkgPath "package.json"
    if (Test-Path $projectPath)
    {
        $projectJson = Get-Content $projectPath | Out-String | ConvertFrom-Json
        $jsStylePkgName = $pkgName.replace("azure-", "@azure/")
        if ($projectJson.name -eq "$jsStylePkgName")
        {
            $pkgReadMePath = Join-Path $pkgPath "README.md"
            $pkgReadMePath = If (Test-Path ($pkgReadMePath)) {  $pkgReadMePath } Else { $null }
            $pkgChangeLogPath = Join-Path $pkgPath "CHANGELOG.md"
            $pkgChangeLogPath = If (Test-Path ($pkgChangeLogPath)) {  $pkgChangeLogPath } Else { $null }

            return @{
                pkgName = $projectJson.name;
                pkgVersion = $projectJson.version;
                pkgDirPath = $pkgPath;
                pkgReadMePath = $pkgReadMePath;
                pkgChangeLogPath = $pkgChangeLogPath;
            }
        }
    }
    return $null
}

function Extract-PythonPkgProps ($pkgPath, $pkgName)
{
    $pkgName = $pkgName.Replace('_', '-')

    if (Test-Path (Join-Path $pkgPath "setup.py"))
    {
        $setupLocation = $pkgPath.Replace('\','/')
        pushd $RepoRoot
        $setupProps = (python -c "import scripts.devops_tasks.common_tasks; obj=scripts.devops_tasks.common_tasks.parse_setup('$setupLocation'); print('{0},{1}'.format(obj[0], obj[1]));") -split ","
        Write-Host $setupProps
        popd
        if (($setupProps -ne $null) -and ($setupProps[0] -eq $pkgName))
        {
            $pkgReadMePath = Join-Path $pkgPath "README.md"
            $pkgReadMePath = If (Test-Path ($pkgReadMePath)) {  $pkgReadMePath } Else { $null }
            $pkgChangeLogPath = Join-Path $pkgPath "CHANGELOG.md"
            $pkgChangeLogPath = If (Test-Path ($pkgChangeLogPath)) {  $pkgChangeLogPath } Else { $null }

            return @{
                pkgName = $setupProps[0];
                pkgVersion = $setupProps[1];
                pkgDirPath = $pkgPath;
                pkgReadMePath = $pkgReadMePath;
                pkgChangeLogPath = $pkgChangeLogPath;
            }
        }
    }
    return $null
}

function Extract-JavaPkgProps ($pkgPath, $pkgName)
{
    $projectPath = Join-Path $pkgPath "pom.xml"

    if (Test-Path $projectPath)
    {
        $projectData = New-Object -TypeName XML
        $projectData.load($projectPath)
        $projectPkgName = Select-XML -Xml $projectData -XPath '/*[local-name()="project"]/*[local-name()="artifactId"]/text()'
        $pkgVersion = Select-XML -Xml $projectData -XPath '/*[local-name()="project"]/*[local-name()="version"]/text()'

        $pkgReadMePath = Join-Path $pkgPath "README.md"
        $pkgReadMePath = If (Test-Path ($pkgReadMePath)) {  $pkgReadMePath } Else { $null }
        $pkgChangeLogPath = Join-Path $pkgPath "CHANGELOG.md"
        $pkgChangeLogPath = If (Test-Path ($pkgChangeLogPath)) {  $pkgChangeLogPath } Else { $null }
        
        if ($projectPkgName.ToString() -eq $pkgName)
        {
            return @{
                pkgName = $pkgName;
                pkgVersion = $pkgVersion.ToString();
                pkgDirPath = $pkgPath;
                pkgReadMePath = $pkgReadMePath;
                pkgChangeLogPath = $pkgChangeLogPath;
            }
        }
    }
    return $null
}

# Takes package name and service Name
# Returns important properties of the package as related to the language repo
# Returns a HashTable with Keys @ { pkgName, pkgVersion, pkgDirPath, pkgReadMePath, pkgChangeLogPath }
function Get-PkgProperties {
    Param
    (
        [Parameter(Mandatory=$true)]
        [string]$pkgName,
        [Parameter(Mandatory=$true)]
        [string]$serviceName
    )

    $pkgDirectoryPath = $null
    $serviceDirectoryPath = Join-Path $RepoRoot "sdk" $serviceName
    if (!(Test-Path $serviceDirectoryPath))
    {
        Write-Error "Service Directory $serviceName does not exist"
        exit 1
    }

    $directoriesPresent = Get-ChildItem $serviceDirectoryPath -Directory

    foreach ($directory in $directoriesPresent)
    {
        $dirName = $directory.Name

        $pkgDirectoryPath = Join-Path $serviceDirectoryPath $dirName
        $pkgProps = Extract-PkgProps -pkgPath $pkgDirectoryPath -pkgName $pkgName
        if ($pkgProps -ne $null)
        {
            return $pkgProps
        }
    }
    Write-Error "Package Directory for $pkgName Path not Found"
    exit 1
}

Export-ModuleMember -Function 'Get-PkgProperties'