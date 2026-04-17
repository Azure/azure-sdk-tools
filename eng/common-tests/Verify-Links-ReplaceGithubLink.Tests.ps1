Import-Module Pester

BeforeAll {
    # Default shared regex pattern with optional .git suffix
    $script:defaultRegex = '^(https://github\.com/Azure/azure-sdk-for-java(?:\.git)?/(?:blob|tree)/)main(/.*)$'
    
    # Helper function to test link replacement logic directly
    # This mirrors the logic in ReplaceGithubLink function from Verify-Links.ps1
    function Test-ReplaceGithubLink {
        param(
            [string]$originLink,
            [string]$branchReplaceRegex,
            [string]$branchReplacementName
        )
        
        if (!$branchReplacementName -or !$branchReplaceRegex) {
            return $originLink
        }
        $ReplacementPattern = "`${1}$branchReplacementName`$2"
        return $originLink -replace $branchReplaceRegex, $ReplacementPattern
    }
}

Describe "ReplaceGithubLink" {
    Context "When repository URI does not end with .git" {
        It "Should replace branch name in GitHub link without .git suffix" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java/blob/main/README.md"
            $expectedLink = "https://github.com/Azure/azure-sdk-for-java/blob/abc123def/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex '^(https://github\.com/Azure/azure-sdk-for-java/(?:blob|tree)/)main(/.*)$' -branchReplacementName "abc123def"
            $result | Should -Be $expectedLink
        }

        It "Should replace branch name with tree path" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java/tree/main/sdk/core"
            $expectedLink = "https://github.com/Azure/azure-sdk-for-java/tree/abc123def/sdk/core"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex '^(https://github\.com/Azure/azure-sdk-for-java/(?:blob|tree)/)main(/.*)$' -branchReplacementName "abc123def"
            $result | Should -Be $expectedLink
        }
    }

    Context "When repository URI ends with .git" {
        It "Should replace branch name in GitHub link with .git suffix" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java.git/blob/main/README.md"
            $expectedLink = "https://github.com/Azure/azure-sdk-for-java.git/blob/abc123def/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex '^(https://github\.com/Azure/azure-sdk-for-java\.git/(?:blob|tree)/)main(/.*)$' -branchReplacementName "abc123def"
            $result | Should -Be $expectedLink
        }

        It "Should replace branch name with tree path and .git suffix" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java.git/tree/main/sdk/core"
            $expectedLink = "https://github.com/Azure/azure-sdk-for-java.git/tree/abc123def/sdk/core"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex '^(https://github\.com/Azure/azure-sdk-for-java\.git/(?:blob|tree)/)main(/.*)$' -branchReplacementName "abc123def"
            $result | Should -Be $expectedLink
        }
    }

    Context "When regex handles optional .git suffix" {
        It "Should replace branch name whether .git is present or not" {
            # Test without .git
            $originalLinkWithoutGit = "https://github.com/Azure/azure-sdk-for-java/blob/main/README.md"
            $expectedLink = "https://github.com/Azure/azure-sdk-for-java/blob/abc123def/README.md"
            
            $resultWithoutGit = Test-ReplaceGithubLink -originLink $originalLinkWithoutGit -branchReplaceRegex $defaultRegex -branchReplacementName "abc123def"
            $resultWithoutGit | Should -Be $expectedLink
            
            # Test with .git
            $originalLinkWithGit = "https://github.com/Azure/azure-sdk-for-java.git/blob/main/README.md"
            $expectedLinkWithGit = "https://github.com/Azure/azure-sdk-for-java.git/blob/abc123def/README.md"
            
            $resultWithGit = Test-ReplaceGithubLink -originLink $originalLinkWithGit -branchReplaceRegex $defaultRegex -branchReplacementName "abc123def"
            $resultWithGit | Should -Be $expectedLinkWithGit
        }
    }

    Context "When link does not match regex pattern" {
        It "Should return original link unchanged" {
            $originalLink = "https://github.com/SomeOrg/SomeRepo/blob/main/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex $defaultRegex -branchReplacementName "abc123def"
            $result | Should -Be $originalLink
        }

        It "Should return original link when branch name doesn't match" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java/blob/feature-branch/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex $defaultRegex -branchReplacementName "abc123def"
            $result | Should -Be $originalLink
        }
    }

    Context "When replacement parameters are not set" {
        It "Should return original link when branchReplacementName is empty" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java/blob/main/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex $defaultRegex -branchReplacementName ""
            $result | Should -Be $originalLink
        }

        It "Should return original link when branchReplaceRegex is empty" {
            $originalLink = "https://github.com/Azure/azure-sdk-for-java/blob/main/README.md"
            
            $result = Test-ReplaceGithubLink -originLink $originalLink -branchReplaceRegex "" -branchReplacementName "abc123def"
            $result | Should -Be $originalLink
        }
    }
}
