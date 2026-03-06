Import-Module Pester

Describe "Test-PageUriMatchesRelativeLinkPattern" {
    BeforeAll {
        # Set up a temp allow-relative-links file
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null

        $script:allowRelativeLinksFile = Join-Path $script:tempDir "allow-relative-links.txt"
        Set-Content -Path $script:allowRelativeLinksFile -Value @"
# Allow relative links in specs directories
**/specs/**
# Also allow in internal-docs
**/internal-docs/**
"@
        # Dot-source the script passing the config file so $allowRelativeLinkPatterns is populated
        . $PSScriptRoot/../common/scripts/Verify-Links.ps1 -allowRelativeLinksFile $script:allowRelativeLinksFile
    }

    AfterAll {
        if (Test-Path $script:tempDir) {
            Remove-Item -Recurse -Force $script:tempDir
        }
    }

    Context "When page URI matches a specs/ pattern" {
        It "Should return true for a file inside specs/ directory" {
            $uri = [System.Uri]("file:///home/user/repo/tools/myapp/specs/design.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeTrue
        }

        It "Should return true for a file in a nested specs/ directory" {
            $uri = [System.Uri]("file:///home/user/repo/tools/myapp/specs/sub/nested-spec.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeTrue
        }

        It "Should return true for a file in a deeply nested specs/ directory" {
            $uri = [System.Uri]("file:///home/user/repo/a/b/c/specs/d/e/doc.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeTrue
        }
    }

    Context "When page URI matches the internal-docs/ pattern" {
        It "Should return true for a file inside internal-docs/ directory" {
            $uri = [System.Uri]("file:///home/user/repo/internal-docs/guide.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeTrue
        }
    }

    Context "When page URI does not match any pattern" {
        It "Should return false for a regular README file" {
            $uri = [System.Uri]("file:///home/user/repo/README.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeFalse
        }

        It "Should return false for a file in a non-matching directory" {
            $uri = [System.Uri]("file:///home/user/repo/docs/guide.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeFalse
        }

        It "Should return false for a file whose name contains 'specs' but is not in a specs/ directory" {
            $uri = [System.Uri]("file:///home/user/repo/docs/my-specs-overview.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeFalse
        }
    }

    Context "When no allow-relative-links patterns are loaded" {
        BeforeAll {
            $script:savedPatterns = $allowRelativeLinkPatterns
            $allowRelativeLinkPatterns = @()
        }
        AfterAll {
            $allowRelativeLinkPatterns = $script:savedPatterns
        }
        It "Should return false for any URI when pattern list is empty" {
            $uri = [System.Uri]("file:///home/user/repo/specs/design.md")
            Test-PageUriMatchesRelativeLinkPattern $uri | Should -BeFalse
        }
    }
}
