Import-Module Pester

BeforeAll {
    $ExitOnError = 0
    . $PSScriptRoot/../common/scripts/copy-docs-to-blobstorage.ps1 -DocLocation "/tmp" -ExitOnError 0
    # common.ps1 sets $Language = "Unknown" in this scope. Override it to "python"
    # so AzureEngSemanticVersion uses the Python regex (with post-release support).
    $Language = "python"
}

Describe "ToSemVer - standard versions" {
    It "Should parse GA version '1.0.0'" {
        $v = ToSemVer "1.0.0"
        $v | Should -Not -BeNullOrEmpty
        $v.Major | Should -Be 1
        $v.Minor | Should -Be 0
        $v.Patch | Should -Be 0
        $v.IsPrerelease | Should -BeFalse
        $v.IsPostRelease | Should -BeFalse
    }

    It "Should parse prerelease version '1.0.0-beta.1'" {
        $v = ToSemVer "1.0.0-beta.1"
        $v | Should -Not -BeNullOrEmpty
        $v.Major | Should -Be 1
        $v.PrereleaseLabel | Should -Be "beta"
        $v.PrereleaseNumber | Should -Be 1
        $v.IsPrerelease | Should -BeTrue
        $v.IsPostRelease | Should -BeFalse
    }

    It "Should return null for invalid version" {
        $v = ToSemVer "notaversion"
        $v | Should -BeNullOrEmpty
    }
}

Describe "ToSemVer - Python post-release versions" {
    It "Should parse Python beta version '1.0.0b2'" {
        
        $v = ToSemVer "1.0.0b2"
        $v | Should -Not -BeNullOrEmpty
        $v.PrereleaseLabel | Should -Be "b"
        $v.PrereleaseNumber | Should -Be 2
        $v.IsPrerelease | Should -BeTrue
        $v.IsPostRelease | Should -BeFalse
    }

    It "Should parse GA post-release '1.0.0.post1' as non-prerelease" {
        
        $v = ToSemVer "1.0.0.post1"
        $v | Should -Not -BeNullOrEmpty
        $v.Major | Should -Be 1
        $v.Minor | Should -Be 0
        $v.Patch | Should -Be 0
        $v.IsPrerelease | Should -BeFalse
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 1
    }

    It "Should parse beta post-release '1.0.0b2.post1' as prerelease" {
        
        $v = ToSemVer "1.0.0b2.post1"
        $v | Should -Not -BeNullOrEmpty
        $v.PrereleaseLabel | Should -Be "b"
        $v.PrereleaseNumber | Should -Be 2
        $v.IsPrerelease | Should -BeTrue
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 1
    }

    It "Should normalize hyphen-separated '1.0.0-post1' as post-release" {
        
        $v = ToSemVer "1.0.0-post1"
        $v | Should -Not -BeNullOrEmpty
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 1
        $v.IsPrerelease | Should -BeFalse
    }

    It "Should normalize underscore-separated '1.0.0_post1' as post-release" {
        
        $v = ToSemVer "1.0.0_post1"
        $v | Should -Not -BeNullOrEmpty
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 1
        $v.IsPrerelease | Should -BeFalse
    }

    It "Should normalize no-separator '1.0.0post1' as post-release" {
        
        $v = ToSemVer "1.0.0post1"
        $v | Should -Not -BeNullOrEmpty
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 1
        $v.IsPrerelease | Should -BeFalse
    }

    It "Should parse post-release with higher number '1.0.0.post15'" {
        
        $v = ToSemVer "1.0.0.post15"
        $v | Should -Not -BeNullOrEmpty
        $v.IsPostRelease | Should -BeTrue
        $v.PostReleaseNumber | Should -Be 15
    }
}

Describe "ToSemVer - non-Python '1.0.0-post1' is prerelease, not post-release" {
    It "Should treat '-post' as a prerelease label for non-Python languages" {
        $Language = ""
        $v = ToSemVer "1.0.0-post1"
        $v | Should -Not -BeNullOrEmpty
        $v.PrereleaseLabel | Should -Be "post"
        $v.IsPrerelease | Should -BeTrue
        $v.IsPostRelease | Should -BeFalse
    }
}

Describe "Sort-Versions - Python post-release sorting" {
    It "Should sort post-release after base GA version" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0", "1.0.0.post1")
        $sorted.RawVersionsList[0] | Should -Be "1.0.0.post1"
        $sorted.RawVersionsList[1] | Should -Be "1.0.0"
    }

    It "Should sort multiple post-releases in descending order" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0", "1.0.0.post1", "1.0.0.post2")
        $sorted.RawVersionsList[0] | Should -Be "1.0.0.post2"
        $sorted.RawVersionsList[1] | Should -Be "1.0.0.post1"
        $sorted.RawVersionsList[2] | Should -Be "1.0.0"
    }

    It "Should sort post-release between base version and next version" {
        
        $sorted = Sort-Versions -VersionArray @("2.0.0", "1.0.0.post1", "1.0.0")
        $sorted.RawVersionsList[0] | Should -Be "2.0.0"
        $sorted.RawVersionsList[1] | Should -Be "1.0.0.post1"
        $sorted.RawVersionsList[2] | Should -Be "1.0.0"
    }

    It "Should sort mixed versions with post-releases and prereleases" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0", "1.0.0.post1", "1.0.0b1", "2.0.0")
        $sorted.RawVersionsList[0] | Should -Be "2.0.0"
        $sorted.RawVersionsList[1] | Should -Be "1.0.0.post1"
        $sorted.RawVersionsList[2] | Should -Be "1.0.0"
        $sorted.RawVersionsList[3] | Should -Be "1.0.0b1"
    }
}

Describe "Sort-Versions - Python post-release LatestGA/LatestPreview classification" {
    It "Should classify GA post-release as LatestGA, not LatestPreview" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0", "1.0.0.post1")
        $sorted.LatestGAPackage | Should -Be "1.0.0.post1"
        $sorted.LatestPreviewPackage | Should -Be ""
    }

    It "Should not set LatestPreview for GA post-release when newer GA exists" {
        
        $sorted = Sort-Versions -VersionArray @("2.0.0", "1.0.0.post1", "1.0.0")
        $sorted.LatestGAPackage | Should -Be "2.0.0"
        $sorted.LatestPreviewPackage | Should -Be ""
    }

    It "Should set LatestPreview when prerelease is newer than GA post-release" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0", "1.0.0.post1", "2.0.0b1")
        $sorted.LatestGAPackage | Should -Be "1.0.0.post1"
        $sorted.LatestPreviewPackage | Should -Be "2.0.0b1"
    }

    It "Should treat beta post-release as prerelease for LatestGA/LatestPreview" {
        
        $sorted = Sort-Versions -VersionArray @("1.0.0b2", "1.0.0b2.post1")
        $sorted.LatestGAPackage | Should -Be ""
        $sorted.LatestPreviewPackage | Should -Be "1.0.0b2.post1"
    }
}
