Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/SemVer.ps1
}

Describe "Post-release version parsing - Default convention (negative tests for non-Python languages)" {
    BeforeEach {
        $global:Language = ""
    }

    It "Should parse '1.0.0-post.1' as prerelease label 'post', NOT as post-release" {
        $ver = [AzureEngSemanticVersion]::ParseVersionString("1.0.0-post.1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.PrereleaseLabel | Should -Be "post"
        $ver.PrereleaseNumber | Should -Be 1
        $ver.IsPrerelease | Should -BeTrue
        $ver.IsPostRelease | Should -BeFalse
    }

    It "Should fail to parse '2.0.0-beta.1-post.1' (regex doesn't match)" {
        $ver = [AzureEngSemanticVersion]::ParseVersionString("2.0.0-beta.1-post.1")
        $ver | Should -BeNullOrEmpty
    }

    It "Should fail to parse '1.2.3-alpha.20200828.9-post.3' (regex doesn't match)" {
        $ver = [AzureEngSemanticVersion]::ParseVersionString("1.2.3-alpha.20200828.9-post.3")
        $ver | Should -BeNullOrEmpty
    }
}

Describe "Post-release version parsing - Python convention" {
    It "Should parse GA post-release '1.0.0.post1'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 1
        $ver.Minor | Should -Be 0
        $ver.Patch | Should -Be 0
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.PrereleaseLabel | Should -BeNullOrEmpty
        $ver.IsPrerelease | Should -BeFalse
        $ver.VersionType | Should -Be "GA"
    }

    It "Should parse patch version post-release '1.2.3.post5'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.2.3.post5")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 1
        $ver.Minor | Should -Be 2
        $ver.Patch | Should -Be 3
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 5
        $ver.PrereleaseLabel | Should -BeNullOrEmpty
        $ver.IsPrerelease | Should -BeFalse
        $ver.VersionType | Should -Be "Patch"
    }

    It "Should parse beta prerelease post-release '1.0.0b2.post1'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2.post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 1
        $ver.Minor | Should -Be 0
        $ver.Patch | Should -Be 0
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.IsPrerelease | Should -BeTrue
        $ver.VersionType | Should -Be "Beta"
    }

    It "Should parse alpha prerelease post-release '2.0.0a20201208001.post2'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("2.0.0a20201208001.post2")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 2
        $ver.Minor | Should -Be 0
        $ver.Patch | Should -Be 0
        $ver.PrereleaseLabel | Should -Be "a"
        $ver.PrereleaseNumber | Should -Be 20201208
        $ver.BuildNumber | Should -Be "001"
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 2
        $ver.IsPrerelease | Should -BeTrue
    }

    It "Should parse zero-major post-release '0.1.0.post1'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("0.1.0.post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 0
        $ver.Minor | Should -Be 1
        $ver.Patch | Should -Be 0
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.IsPrerelease | Should -BeTrue
        $ver.VersionType | Should -Be "Beta"
    }

    It "Should parse implicit post-release number '1.0.0.post' as post0" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.Major | Should -Be 1
        $ver.Minor | Should -Be 0
        $ver.Patch | Should -Be 0
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.PrereleaseLabel | Should -BeNullOrEmpty
        $ver.IsPrerelease | Should -BeFalse
        $ver.VersionType | Should -Be "GA"
    }

    It "Should parse implicit prerelease post-release '1.0.0b2.post' as post0" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2.post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsSemVerFormat | Should -BeTrue
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.IsPrerelease | Should -BeTrue
    }
}

Describe "Post-release version ToString round-trip - Default convention (non-Python languages don't support post-release, so should round-trip as prerelease)" {
    BeforeEach {
        $global:Language = ""
    }

    It "'1.0.0-post.1' round-trips as prerelease (label 'post'), not post-release" {
        $ver = [AzureEngSemanticVersion]::ParseVersionString("1.0.0-post.1")
        $ver.ToString() | Should -Be "1.0.0-post.1"
        $ver.IsPostRelease | Should -BeFalse
        $ver.PrereleaseLabel | Should -Be "post"
    }
}

Describe "PEP 440 alternate post-release format normalization - Python convention" {
    It "Should normalize hyphen separator '1.0.0-post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0-post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should normalize underscore separator '1.0.0_post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0_post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should normalize no-separator '1.0.0post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should normalize dot-number separator '1.0.0.post.1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post.1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should normalize uppercase '1.0.0.POST1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.POST1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should normalize implicit post number '1.0.0.post' to '1.0.0.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.ToString() | Should -Be "1.0.0.post0"
    }

    It "Should normalize implicit post number with hyphen '1.0.0-post' to '1.0.0.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0-post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.ToString() | Should -Be "1.0.0.post0"
    }

    It "Should normalize implicit post number with underscore '1.0.0_post' to '1.0.0.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0_post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.ToString() | Should -Be "1.0.0.post0"
    }

    It "Should normalize implicit post number with no separator '1.0.0post' to '1.0.0.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.ToString() | Should -Be "1.0.0.post0"
    }

    It "Should normalize implicit prerelease post number '1.0.0b2.post' to '1.0.0b2.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2.post")
        $ver | Should -Not -BeNullOrEmpty
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 0
        $ver.ToString() | Should -Be "1.0.0b2.post0"
    }

    It "Should normalize prerelease hyphen separator '1.0.0b2-post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2-post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0b2.post1"
    }

    It "Should normalize prerelease underscore separator '1.0.0b2_post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2_post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0b2.post1"
    }

    It "Should normalize prerelease no-separator '1.0.0b2post1' to canonical form" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2post1")
        $ver | Should -Not -BeNullOrEmpty
        $ver.PrereleaseLabel | Should -Be "b"
        $ver.PrereleaseNumber | Should -Be 2
        $ver.IsPostRelease | Should -BeTrue
        $ver.PostReleaseNumber | Should -Be 1
        $ver.ToString() | Should -Be "1.0.0b2.post1"
    }
}

Describe "Post-release version ToString round-trip - Python convention" {
    It "Should round-trip GA post-release '1.0.0.post1'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post1")
        $ver.ToString() | Should -Be "1.0.0.post1"
    }

    It "Should round-trip patch version post-release '1.2.3.post5'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.2.3.post5")
        $ver.ToString() | Should -Be "1.2.3.post5"
    }

    It "Should round-trip beta prerelease post-release '1.0.0b2.post1'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2.post1")
        $ver.ToString() | Should -Be "1.0.0b2.post1"
    }

    It "Should round-trip alpha prerelease post-release '2.0.0a20201208001.post2'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("2.0.0a20201208001.post2")
        $ver.ToString() | Should -Be "2.0.0a20201208001.post2"
    }

    It "Should normalize implicit post-release '1.0.0.post' to '1.0.0.post0'" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post")
        $ver.ToString() | Should -Be "1.0.0.post0"
    }
}

Describe "Post-release version sorting - Python convention" {
    BeforeAll {
        $global:Language = "python"
    }

    AfterAll {
        $global:Language = ""
    }

    It "Should sort GA post-releases after GA and before next patch" {
        $versions = @(
            "1.0.1",
            "1.0.0.post2",
            "1.0.0",
            "1.0.0.post1"
        )
        $expectedSort = @(
            "1.0.1",
            "1.0.0.post2",
            "1.0.0.post1",
            "1.0.0"
        )
        $sort = [AzureEngSemanticVersion]::SortVersionStrings($versions)
        for ($i = 0; $i -lt $expectedSort.Count; $i++) {
            $sort[$i] | Should -Be $expectedSort[$i]
        }
    }

    It "Should sort prerelease post-releases after prerelease and before next prerelease" {
        $versions = @(
            "1.0.0",
            "1.0.0b2",
            "1.0.0b1.post1",
            "1.0.0b1"
        )
        $expectedSort = @(
            "1.0.0",
            "1.0.0b2",
            "1.0.0b1.post1",
            "1.0.0b1"
        )
        $sort = [AzureEngSemanticVersion]::SortVersionStrings($versions)
        for ($i = 0; $i -lt $expectedSort.Count; $i++) {
            $sort[$i] | Should -Be $expectedSort[$i]
        }
    }

    It "Should sort mixed versions with post-releases correctly" {
        $versions = @(
            "2.0.0",
            "1.0.0.post1",
            "2.0.0b1",
            "1.0.0",
            "2.0.0b1.post1",
            "2.0.0.post1",
            "1.0.1"
        )
        $expectedSort = @(
            "2.0.0.post1",
            "2.0.0",
            "2.0.0b1.post1",
            "2.0.0b1",
            "1.0.1",
            "1.0.0.post1",
            "1.0.0"
        )
        $sort = [AzureEngSemanticVersion]::SortVersionStrings($versions)
        for ($i = 0; $i -lt $expectedSort.Count; $i++) {
            $sort[$i] | Should -Be $expectedSort[$i]
        }
    }

    It "Should sort implicit post-release (post0) equivalently to explicit post0" {
        $versions = @(
            "1.0.0.post1",
            "1.0.0",
            "1.0.0.post0"
        )
        $expectedSort = @(
            "1.0.0.post1",
            "1.0.0.post0",
            "1.0.0"
        )
        $sort = [AzureEngSemanticVersion]::SortVersionStrings($versions)
        for ($i = 0; $i -lt $expectedSort.Count; $i++) {
            $sort[$i] | Should -Be $expectedSort[$i]
        }
    }
}

Describe "Post-release version increment - Python convention" {
    It "Should increment GA post-release '1.0.0.post1' to next prerelease" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0.post1")
        $ver.IncrementAndSetToPrerelease()
        $ver.ToString() | Should -Be "1.1.0b1"
    }

    It "Should increment beta post-release '1.0.0b2.post1' to next prerelease number" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("1.0.0b2.post1")
        $ver.IncrementAndSetToPrerelease()
        $ver.ToString() | Should -Be "1.0.0b3"
    }

    It "Should increment zero-major post-release '0.1.0.post1' to next minor" {
        $ver = [AzureEngSemanticVersion]::ParsePythonVersionString("0.1.0.post1")
        $ver.IncrementAndSetToPrerelease()
        $ver.ToString() | Should -Be "0.2.0"
    }
}
