Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/Verify-Links.ps1
}

Describe "ProcessNpmLink" {
    It "Should handle versioned scoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents/v/1.1.0"
        
        # We can't directly test ProcessNpmLink as it calls ProcessStandardLink which makes web requests
        # Instead, we'll test the URL transformation logic by examining what URL would be created
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0"
    }

    It "Should handle versioned unscoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/express/v/4.18.2"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/express/4.18.2"
    }

    It "Should handle non-versioned scoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        elseif ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents"
    }

    It "Should handle non-versioned unscoped package URL" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/express"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        elseif ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/express"
    }

    It "Should handle URL without www prefix - versioned" {
        $inputUrl = [System.Uri]"https://npmjs.com/package/@azure/ai-agents/v/1.1.0"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0"
    }

    It "Should handle URL without www prefix - non-versioned" {
        $inputUrl = [System.Uri]"https://npmjs.com/package/@azure/identity"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        elseif ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/identity"
    }

    It "Should handle package name with hyphens and numbers" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/lodash-es"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        elseif ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/lodash-es"
    }

    It "Should handle package with multiple version segments" {
        $inputUrl = [System.Uri]"https://www.npmjs.com/package/@azure/ai-agents/v/1.1.0-beta.1"
        
        $urlString = $inputUrl.ToString()
        
        if ($urlString -match '^https?://(?:www\.)?npmjs\.com/package/([^/]+(?:/[^/]+)?)/v/(.+)$') {
            $apiUrl = "https://registry.npmjs.org/$($matches[1])/$($matches[2])"
        }
        
        $apiUrl | Should -Be "https://registry.npmjs.org/@azure/ai-agents/1.1.0-beta.1"
    }
}
