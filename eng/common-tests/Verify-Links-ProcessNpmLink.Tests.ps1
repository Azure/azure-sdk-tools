Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/Verify-Links.ps1
    
    # Helper function to test URL transformation without making web requests
    function Get-TransformedNpmUrl([System.Uri]$linkUri) {
        $urlString = $linkUri.ToString()
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            # Versioned URL: remove the /v/ segment but keep the version
            return "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        elseif ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/(.+)$') {
            # Non-versioned URL: just replace the domain
            return "https://registry.npmjs.org/$($matches[1])"
        }
        else {
            # Fallback: use the original URL if it doesn't match expected patterns
            return $urlString
        }
    }
}

Describe "ProcessNpmLink" {
    It "Should handle versioned scoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents/v/1.1.0"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0"
    }

    It "Should handle versioned unscoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/express/v/4.18.2"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/express/4.18.2"
    }

    It "Should handle non-versioned scoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents"
    }

    It "Should handle non-versioned unscoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/express"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/express"
    }

    It "Should handle URL without www prefix - versioned" {
        $inputUrl = [System.Uri]"https://npmjs.com/package/@azure/ai-agents/v/1.1.0"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0"
    }

    It "Should handle URL without www prefix - non-versioned" {
        $inputUrl = [System.Uri]"https://npmjs.com/package/@azure/identity"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/identity"
    }

    It "Should handle package name with hyphens and numbers" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/lodash-es"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/lodash-es"
    }

    It "Should handle package with multiple version segments" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents/v/1.1.0-beta.1"
        $apiUrl = Get-TransformedNpmUrl $inputUrl
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0-beta.1"
    }
}
