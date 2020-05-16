# Helper functions for retireving useful information from azure-sdk-for-* repo
# Example Use : Import-Module .\eng\common\scripts\modules
class PackageProps
{
    [string]$pkgName
    [string]$pkgVersion
    [string]$pkgDirectoryPath
    [string]$pkgReadMePath
    [string]$pkgChangeLogPath

    PackageProps()
    {
    }
    PackageProps(
        [string]$pkgName,
        [string]$pkgVersion,
        [string]$pkgDirectoryPath
    )
    {
        $this.pkgName = $pkgName
        $this.pkgVersion = $pkgVersion
        $this.pkgDirectoryPath = $pkgDirectoryPath

        if (Test-Path (Join-Path $pkgDirectoryPath "README.md"))
        {
            $this.pkgReadMePath = Join-Path $pkgDirectoryPath "README.md"
        } else
        {
            $this.pkgReadMePath = $null
        }

        if (Test-Path (Join-Path $pkgDirectoryPath "CHANGELOG.md"))
        {
            $this.pkgChangeLogPath = Join-Path $pkgDirectoryPath "CHANGELOG.md"
        } else
        {
            $this.pkgChangeLogPath = $null
        }
    }
}

function Extract-PkgProps ($pkgPath, $pkgName)
{
    if ($Language -eq "net")
    { Extract-DotNetPkgProps -pkgPath $pkgPath -pkgName $pkgName 
    }
    if ($Language -eq "java")
    { Extract-JavaPkgProps -pkgPath $pkgPath -pkgName $pkgName 
    }
    if ($Language -eq "js")
    { Extract-JsPkgProps -pkgPath $pkgPath -pkgName $pkgName 
    }
    if ($Language -eq "python")
    { Extract-PythonPkgProps -pkgPath $pkgPath -pkgName $pkgName 
    }
}

function Extract-DotNetPkgProps ($pkgPath, $pkgName)
{
    $projectPath = Join-Path $pkgPath "src" "$pkgName.csproj"
    if (Test-Path $projectPath)
    {
        $projectData = New-Object -TypeName XML
        $projectData.load($projectPath)

        $pkgVersion = Select-XML -Xml $projectData -XPath '/Project/PropertyGroup/Version'
        
        return [PackageProps]::new($pkgName, $pkgVersion, $pkgPath)
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
            return [PackageProps]::new($projectJson.name, $projectJson.version, $pkgPath)
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
        if($setupProps.Length -ne 2) 
        {
            Write-Error ("Error parsing {0}" -f (Join-Path $pkgPath "setup.py"))
            exit 1
        }
        if (($setupProps -ne $null) -and ($setupProps[0] -eq $pkgName))
        {
            return [PackageProps]::new($setupProps[0], $setupProps[1], $pkgPath)
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

        if ($projectPkgName.ToString() -eq $pkgName)
        {
            return [PackageProps]::new($pkgName, $pkgVersion.ToString(), $pkgPath)
        }
    }
    return $null
}

# Takes package name and service Name
# Returns important properties of the package as related to the language repo
# Returns a HashTable with Keys @ { pkgName, pkgVersion, pkgDirPath, pkgReadMePath, pkgChangeLogPath }
function Get-PkgProperties
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [string]$PackageName,
        [Parameter(Mandatory=$true)]
        [string]$ServiceName,
        [Parameter(Mandatory=$true)]
        [ValidateSet("net","java","js","python")]
        [string]$Language,
        [string]$RepoRoot="${PSScriptRoot}/../../../.."
    )

    $pkgDirectoryPath = $null
    $serviceDirectoryPath = Join-Path $RepoRoot "sdk" $ServiceName
    if (!(Test-Path $serviceDirectoryPath))
    {
        Write-Error "Service Directory $ServiceName does not exist"
        exit 1
    }

    $directoriesPresent = Get-ChildItem $serviceDirectoryPath -Directory

    foreach ($directory in $directoriesPresent)
    {
        $dirName = $directory.Name

        $pkgDirectoryPath = Join-Path $serviceDirectoryPath $dirName
        $pkgProps = Extract-PkgProps -pkgPath $pkgDirectoryPath -pkgName $PackageName
        if ($pkgProps -ne $null)
        {
            return $pkgProps
        }
    }
    Write-Error "Package Directory for $PackageName Path not Found"
    exit 1
}

Export-ModuleMember -Function 'Get-PkgProperties'