#Requires -Version 7.0
#Requires -Modules Pester

# Pester tests for Split-EvalSuite.ps1.
# Run from this directory:  Invoke-Pester

BeforeAll {
    $script:scriptPath = Join-Path $PSScriptRoot '..' 'Split-EvalSuite.ps1'

    # Build a throwaway eval tree so the tests do not depend on real eval content.
    $script:root = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-matrix-test-" + [Guid]::NewGuid())
    New-Item -ItemType Directory -Path (Join-Path $root 'evals/tools') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'evals/workflow-scenarios/mock') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'evals/workflow-scenarios/live') -Force | Out-Null

    'x' | Set-Content (Join-Path $root 'evals/tools/prompt-to-tool-github.eval.yaml')
    'x' | Set-Content (Join-Path $root 'evals/tools/add-arm-resource.eval.yaml')
    'x' | Set-Content (Join-Path $root 'evals/workflow-scenarios/mock/rename-client-property.eval.yaml')
    'x' | Set-Content (Join-Path $root 'evals/workflow-scenarios/live/release-planner.eval.yaml')

    # Two of the hermetic files share an area tag so the area-shard grouping is exercised.
    "tags:`n  area: github" | Set-Content (Join-Path $root 'evals/tools/prompt-to-tool-github.eval.yaml')
    "tags:`n  area: typespec" | Set-Content (Join-Path $root 'evals/tools/add-arm-resource.eval.yaml')
    "tags:`n  area: typespec" | Set-Content (Join-Path $root 'evals/workflow-scenarios/mock/rename-client-property.eval.yaml')
}

AfterAll {
    if (Test-Path $script:root) {
        Remove-Item -Path $script:root -Recurse -Force
    }
}

Describe 'Split-EvalSuite.ps1' {
    Context 'ShardBy file (default)' {
        It 'discovers the hermetic mock-vertical files by default' {
            $matrix = & $script:scriptPath -EvalRoot $script:root
            $matrix.Count | Should -Be 3
        }

        It 'excludes the live tier from the default pattern' {
            $matrix = & $script:scriptPath -EvalRoot $script:root
            ($matrix.Values.evalArgs -match 'live/') | Should -BeNullOrEmpty
        }

        It 'emits one forward-slashed `-e` arg per file' {
            $matrix = & $script:scriptPath -EvalRoot $script:root
            foreach ($entry in $matrix.Values) {
                $entry.evalArgs | Should -Not -Match '\\'
                $entry.evalArgs | Should -Match '^-e evals/'
            }
        }

        It 'produces filesystem-safe, parent-prefixed shard names' {
            $matrix = & $script:scriptPath -EvalRoot $script:root
            $matrix.Keys | Should -Contain 'tools_prompt_to_tool_github'
            $matrix.Keys | Should -Contain 'mock_rename_client_property'
            foreach ($key in $matrix.Keys) {
                $key | Should -Match '^[A-Za-z0-9_]+$'
            }
        }

        It 'throws when no eval files match' {
            { & $script:scriptPath -EvalRoot $script:root -Pattern 'evals/none/*.eval.yaml' } |
                Should -Throw
        }
    }

    Context 'ShardBy area' {
        It 'collapses files into one shard per area tag' {
            $matrix = & $script:scriptPath -EvalRoot $script:root -ShardBy area
            # github (1 file) + typespec (2 files) = 2 shards from 3 files.
            $matrix.Count | Should -Be 2
            $matrix.Keys | Should -Contain 'area_github'
            $matrix.Keys | Should -Contain 'area_typespec'
        }

        It 'groups every file of an area into one shard via repeated -e flags' {
            $matrix = & $script:scriptPath -EvalRoot $script:root -ShardBy area
            ([regex]::Matches($matrix['area_typespec'].evalArgs, '-e ')).Count | Should -Be 2
        }

        It 'keeps the live tier out of area shards' {
            $matrix = & $script:scriptPath -EvalRoot $script:root -ShardBy area
            ($matrix.Values.evalArgs -match 'live/') | Should -BeNullOrEmpty
        }
    }

    Context 'ShardBy area with an untagged eval' {
        BeforeAll {
            $script:utRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-matrix-ut-" + [Guid]::NewGuid())
            New-Item -ItemType Directory -Path (Join-Path $utRoot 'evals/tools') -Force | Out-Null

            # One tagged file and one with no `area` tag in the same folder.
            "tags:`n  area: github" | Set-Content (Join-Path $utRoot 'evals/tools/tagged.eval.yaml')
            'no tags here' | Set-Content (Join-Path $utRoot 'evals/tools/untagged.eval.yaml')
        }

        AfterAll {
            if (Test-Path $script:utRoot) {
                Remove-Item -Path $script:utRoot -Recurse -Force
            }
        }

        It 'falls back to the parent folder name as the area' {
            $matrix = & $script:scriptPath -EvalRoot $script:utRoot -ShardBy area `
                -Pattern 'evals/tools/*.eval.yaml' -WarningAction SilentlyContinue
            $matrix.Keys | Should -Contain 'area_github'
            $matrix.Keys | Should -Contain 'area_tools'
            $matrix['area_tools'].evalArgs | Should -Match 'untagged\.eval\.yaml'
        }

        It 'does not lump untagged files into a single untagged bucket' {
            $matrix = & $script:scriptPath -EvalRoot $script:utRoot -ShardBy area `
                -Pattern 'evals/tools/*.eval.yaml' -WarningAction SilentlyContinue
            $matrix.Keys | Should -Not -Contain 'area_untagged'
        }

        It 'warns when an eval has no area tag' {
            $warnings = & $script:scriptPath -EvalRoot $script:utRoot -ShardBy area `
                -Pattern 'evals/tools/*.eval.yaml' 3>&1 |
                Where-Object { $_ -is [System.Management.Automation.WarningRecord] }
            ($warnings -join "`n") | Should -Match 'untagged\.eval\.yaml'
        }
    }
}
