Import-Module Pester

Set-StrictMode -Version 3

BeforeAll {
    . (Join-Path $PSScriptRoot ".." ".." "common" "scripts" "SemVer.ps1")

    # The script has a mandatory param block and a try/catch that runs on dot-source,
    # so we extract just the function definitions and invoke them as a script block.
    $scriptContent = Get-Content (Join-Path $PSScriptRoot ".." "Increment-dotnet-package.ps1") -Raw
    if ($scriptContent -match '(?s)(function\s+IncrementPackageVersion.+?)(?=\r?\ntry\s*\{)') {
        $functionBlock = $Matches[1]
        $scriptBlock = [scriptblock]::Create($functionBlock)
        . $scriptBlock
    } else {
        throw "Could not extract function definitions from Increment-dotnet-package.ps1"
    }
}

# --------------------- Get-PackageName ---------------------
Describe "Get-PackageName" {
    Context "when PackageId is defined" {
        It "returns PackageId" {
            $dir = Join-Path $TestDrive "pkgid"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "MyProject.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>My.Custom.PackageId</PackageId>
    <AssemblyName>MyAssembly</AssemblyName>
    <VersionPrefix>1.0.0</VersionPrefix>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "My.Custom.PackageId"
        }
    }

    Context "when only AssemblyName is defined" {
        It "returns AssemblyName" {
            $dir = Join-Path $TestDrive "asmname"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "MyProject.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>My.Assembly.Name</AssemblyName>
    <VersionPrefix>1.0.0</VersionPrefix>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "My.Assembly.Name"
        }
    }

    Context "when neither PackageId nor AssemblyName is defined" {
        It "returns the csproj filename without extension" {
            $dir = Join-Path $TestDrive "noname"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "Azure.Sdk.Tools.Something.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <VersionPrefix>1.0.0</VersionPrefix>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "Azure.Sdk.Tools.Something"
        }
    }

    Context "when PackageId is empty" {
        It "falls back to AssemblyName" {
            $dir = Join-Path $TestDrive "emptypkgid"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "MyProject.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId></PackageId>
    <AssemblyName>Fallback.Assembly</AssemblyName>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "Fallback.Assembly"
        }
    }

    Context "when PackageId is whitespace-only" {
        It "falls back to filename" {
            $dir = Join-Path $TestDrive "wspkgid"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "Azure.Something.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>   </PackageId>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "Azure.Something"
        }
    }

    Context "when PackageId is in a second PropertyGroup" {
        It "still finds it" {
            $dir = Join-Path $TestDrive "secondpg"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $csproj = Join-Path $dir "MyProject.csproj"
            @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <VersionPrefix>1.0.0</VersionPrefix>
  </PropertyGroup>
  <PropertyGroup>
    <PackageId>Second.Group.Package</PackageId>
  </PropertyGroup>
</Project>
"@ | Set-Content $csproj

            Get-PackageName -CsprojPath $csproj | Should -Be "Second.Group.Package"
        }
    }
}

# --------------------- Find-RepoRoot ---------------------
Describe "Find-RepoRoot" {
    Context "when .git exists in parent" {
        It "returns the directory containing .git" {
            $root = Join-Path $TestDrive "repo"
            $sub = Join-Path $root "tools" "mytool"
            New-Item -ItemType Directory -Path $sub -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $root ".git") -Force | Out-Null

            Find-RepoRoot -StartDirectory $sub | Should -Be (Get-Item $root).FullName
        }
    }

    Context "when .git is in the start directory itself" {
        It "returns the start directory" {
            $root = Join-Path $TestDrive "repoself"
            New-Item -ItemType Directory -Path $root -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $root ".git") -Force | Out-Null

            Find-RepoRoot -StartDirectory $root | Should -Be (Get-Item $root).FullName
        }
    }

    Context "when .git is several levels up" {
        It "returns the directory containing .git" {
            $root = Join-Path $TestDrive "repodeep"
            $deep = Join-Path $root "a" "b" "c" "d"
            New-Item -ItemType Directory -Path $deep -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $root ".git") -Force | Out-Null

            Find-RepoRoot -StartDirectory $deep | Should -Be (Get-Item $root).FullName
        }
    }

    Context "when .git does not exist" {
        It "returns null" {
            $dir = Join-Path $TestDrive "norepo" "deep" "path"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null

            Find-RepoRoot -StartDirectory $dir | Should -BeNullOrEmpty
        }
    }
}

# --------------------- Get-CentralPackageManagementFiles ---------------------
Describe "Get-CentralPackageManagementFiles" {
    Context "with new CPM structure only" {
        It "returns files from centralpackagemanagement but not overrides" {
            $root = Join-Path $TestDrive "newcpm"
            $cpmDir = Join-Path $root "eng" "centralpackagemanagement"
            $overridesDir = Join-Path $cpmDir "overrides"
            New-Item -ItemType Directory -Path $overridesDir -Force | Out-Null

            # Central files
            "main" | Set-Content (Join-Path $cpmDir "Directory.Packages.props")
            "support" | Set-Content (Join-Path $cpmDir "Directory.Support.Packages.props")
            "readme" | Set-Content (Join-Path $cpmDir "README.md")

            # Override file (should NOT be returned)
            "override" | Set-Content (Join-Path $overridesDir "Azure.Core.Packages.props")

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $names = $files | ForEach-Object { $_.Name } | Sort-Object

            $names | Should -Contain "Directory.Packages.props"
            $names | Should -Contain "Directory.Support.Packages.props"
            $names | Should -Not -Contain "README.md"
            $names | Should -Not -Contain "Azure.Core.Packages.props"
        }
    }

    Context "with all six new CPM files" {
        It "returns all central files" {
            $root = Join-Path $TestDrive "allnewcpm"
            $cpmDir = Join-Path $root "eng" "centralpackagemanagement"
            New-Item -ItemType Directory -Path $cpmDir -Force | Out-Null

            $centralFiles = @(
                "Directory.Packages.props",
                "Directory.Support.Packages.props",
                "Directory.Extensions.Packages.props",
                "Directory.Generation.Packages.props",
                "Directory.Integration.Packages.props",
                "Directory.Legacy.Packages.props"
            )
            foreach ($f in $centralFiles) {
                "content" | Set-Content (Join-Path $cpmDir $f)
            }

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $files.Count | Should -Be 6
        }
    }

    Context "with old Packages.Data.props only" {
        It "returns the old file" {
            $root = Join-Path $TestDrive "oldcpm"
            $engDir = Join-Path $root "eng"
            New-Item -ItemType Directory -Path $engDir -Force | Out-Null

            "old" | Set-Content (Join-Path $engDir "Packages.Data.props")

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $files.Count | Should -Be 1
            $files[0].Name | Should -Be "Packages.Data.props"
        }
    }

    Context "with both new and old CPM files" {
        It "returns files from both locations" {
            $root = Join-Path $TestDrive "bothcpm"
            $cpmDir = Join-Path $root "eng" "centralpackagemanagement"
            New-Item -ItemType Directory -Path $cpmDir -Force | Out-Null

            "new" | Set-Content (Join-Path $cpmDir "Directory.Packages.props")
            "old" | Set-Content (Join-Path $root "eng" "Packages.Data.props")

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $names = $files | ForEach-Object { $_.Name }

            $names | Should -Contain "Directory.Packages.props"
            $names | Should -Contain "Packages.Data.props"
        }
    }

    Context "with no CPM files" {
        It "returns empty array" {
            $root = Join-Path $TestDrive "nocpm"
            New-Item -ItemType Directory -Path (Join-Path $root "eng") -Force | Out-Null

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $files.Count | Should -Be 0
        }
    }

    Context "when eng directory does not exist" {
        It "returns empty array" {
            $root = Join-Path $TestDrive "noeng"
            New-Item -ItemType Directory -Path $root -Force | Out-Null

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $files.Count | Should -Be 0
        }
    }

    Context "when centralpackagemanagement dir exists but has no .Packages.props files" {
        It "returns empty array" {
            $root = Join-Path $TestDrive "emptycpm"
            $cpmDir = Join-Path $root "eng" "centralpackagemanagement"
            New-Item -ItemType Directory -Path $cpmDir -Force | Out-Null
            "readme" | Set-Content (Join-Path $cpmDir "README.md")

            $files = Get-CentralPackageManagementFiles -RepoRoot $root
            $files.Count | Should -Be 0
        }
    }
}

# --------------------- Update-CentralPackageVersions ---------------------
Describe "Update-CentralPackageVersions" {
    Context "with new CPM format (PackageVersion Include)" {
        It "updates the version" {
            $dir = Join-Path $TestDrive "update-new"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
    <PackageVersion Include="Azure.Identity" Version="1.12.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            $content | Should -Match 'Include="Azure.Core" Version="1.44.0"'
            $content | Should -Match 'Include="Azure.Identity" Version="1.12.0"'
        }
    }

    Context "with old CPM format (PackageReference Update)" {
        It "updates the version" {
            $dir = Join-Path $TestDrive "update-old"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Packages.Data.props"
            @"
<Project>
  <ItemGroup>
    <PackageReference Update="Azure.Core" Version="1.43.0" />
    <PackageReference Update="Azure.Identity" Version="1.12.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            $content | Should -Match 'Update="Azure.Core" Version="1.44.0"'
            $content | Should -Match 'Update="Azure.Identity" Version="1.12.0"'
        }
    }

    Context "with property reference version" {
        It "does not update property references" {
            $dir = Join-Path $TestDrive "update-propref"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Packages.Data.props"
            @"
<Project>
  <ItemGroup>
    <PackageReference Update="Microsoft.Rest.ClientRuntime" Version="`$(MicrosoftRestClientRuntimeVersion)" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Microsoft.Rest.ClientRuntime" -NewVersion "3.0.0" -Files $files

            $count | Should -Be 0
        }
    }

    Context "with version range" {
        It "does not update version ranges starting with bracket" {
            $dir = Join-Path $TestDrive "update-range"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Packages.Data.props"
            @"
<Project>
  <ItemGroup>
    <PackageReference Update="Microsoft.Rest.ClientRuntime.Azure" Version="[3.3.19, 4.0.0)" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Microsoft.Rest.ClientRuntime.Azure" -NewVersion "3.4.0" -Files $files

            $count | Should -Be 0
            (Get-Content $file -Raw) | Should -Match 'Version="\[3\.3\.19, 4\.0\.0\)"'
        }
    }

    Context "when package is not found in file" {
        It "returns 0 and does not modify file" {
            $dir = Join-Path $TestDrive "update-notfound"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            $original = @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Identity" Version="1.12.0" />
  </ItemGroup>
</Project>
"@
            $original | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 0
            (Get-Content $file -Raw) | Should -Be $original
        }
    }

    Context "with package in multiple files" {
        It "updates all files" {
            $dir = Join-Path $TestDrive "update-multi"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null

            $file1 = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file1 -NoNewline

            $file2 = Join-Path $dir "Directory.Support.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file2 -NoNewline

            $files = @(Get-Item $file1) + @(Get-Item $file2)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 2
            (Get-Content $file1 -Raw) | Should -Match 'Version="1.44.0"'
            (Get-Content $file2 -Raw) | Should -Match 'Version="1.44.0"'
        }
    }

    Context "with multiple entries of same package in one file (different ItemGroups)" {
        It "updates all occurrences" {
            $dir = Join-Path $TestDrive "update-samefilemulti"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Packages.Data.props"
            @"
<Project>
  <ItemGroup Condition="'`$(IsClientLibrary)' == 'true'">
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
  </ItemGroup>
  <ItemGroup Condition="'`$(IsTestProject)' == 'true'">
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            # Both occurrences should be updated
            $matches = [regex]::Matches($content, 'Version="1\.44\.0"')
            $matches.Count | Should -Be 2
            # No old version should remain
            $content | Should -Not -Match 'Version="1\.43\.0"'
        }
    }

    Context "with PrivateAssets attribute after Version" {
        It "updates the version preserving other attributes" {
            $dir = Join-Path $TestDrive "update-privateassets"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.ClientSdk.Analyzers" Version="0.1.0" PrivateAssets="All" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.ClientSdk.Analyzers" -NewVersion "0.1.1" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            $content | Should -Match 'Version="0.1.1" PrivateAssets="All"'
        }
    }

    Context "with prerelease version" {
        It "updates prerelease versions" {
            $dir = Join-Path $TestDrive "update-prerelease"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Generator" Version="1.0.0-alpha.20260219.1" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Generator" -NewVersion "1.0.0-alpha.20260225.1" -Files $files

            $count | Should -Be 1
            (Get-Content $file -Raw) | Should -Match 'Version="1.0.0-alpha.20260225.1"'
        }
    }

    Context "does not match a package that is a prefix of another package name" {
        It "Azure.Core does not match Azure.Core.Amqp" {
            $dir = Join-Path $TestDrive "update-prefix"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
    <PackageVersion Include="Azure.Core.Amqp" Version="1.3.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            $content | Should -Match 'Include="Azure.Core" Version="1.44.0"'
            $content | Should -Match 'Include="Azure.Core.Amqp" Version="1.3.0"'
        }
    }

    Context "dots in package name are literal, not regex wildcards" {
        It "Azure.Core does not match AzureXCore" {
            $dir = Join-Path $TestDrive "update-dots"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="AzureXCore" Version="1.0.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 0
            (Get-Content $file -Raw) | Should -Match 'Version="1.0.0"'
        }
    }

    Context "with extra whitespace between attributes" {
        It "handles varied spacing" {
            $dir = Join-Path $TestDrive "update-spacing"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion  Include="Azure.Core"   Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            (Get-Content $file -Raw) | Should -Match 'Version="1.44.0"'
        }
    }

    Context "with tab indentation" {
        It "handles tab-indented files" {
            $dir = Join-Path $TestDrive "update-tabs"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            "<Project>`n`t<ItemGroup>`n`t`t<PackageVersion Include=`"Azure.Core`" Version=`"1.43.0`" />`n`t</ItemGroup>`n</Project>" | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            (Get-Content $file -Raw) | Should -Match 'Version="1.44.0"'
        }
    }

    Context "with version already matching target" {
        It "returns 0 (no changes needed)" {
            $dir = Join-Path $TestDrive "update-noop"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.44.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 0
        }
    }

    Context "with empty file" {
        It "returns 0 and does not crash" {
            $dir = Join-Path $TestDrive "update-empty"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            "" | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 0
        }
    }

    Context "with PackageVersion Update (override style in central file)" {
        It "updates the version" {
            $dir = Join-Path $TestDrive "update-pkgver-update"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Directory.Packages.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Update="Azure.Core" Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            (Get-Content $file -Raw) | Should -Match 'Update="Azure.Core" Version="1.44.0"'
        }
    }

    Context "with mixed formats in same file" {
        It "updates both PackageVersion Include and PackageReference Update" {
            $dir = Join-Path $TestDrive "update-mixed"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = Join-Path $dir "Packages.Data.props"
            @"
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Core" Version="1.43.0" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Update="Azure.Core" Version="1.43.0" />
  </ItemGroup>
</Project>
"@ | Set-Content $file -NoNewline

            $files = @(Get-Item $file)
            $count = Update-CentralPackageVersions -PackageName "Azure.Core" -NewVersion "1.44.0" -Files $files

            $count | Should -Be 1
            $content = Get-Content $file -Raw
            $matches = [regex]::Matches($content, 'Version="1\.44\.0"')
            $matches.Count | Should -Be 2
            $content | Should -Not -Match 'Version="1\.43\.0"'
        }
    }
}
