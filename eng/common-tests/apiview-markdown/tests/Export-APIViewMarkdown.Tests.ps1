# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

<#
.SYNOPSIS
Pester tests for Export-APIViewMarkdown.ps1
#>

BeforeAll {
    $scriptPath = (Resolve-Path "$PSScriptRoot/../../../common/scripts/Export-APIViewMarkdown.ps1").Path
    $testFilesDir = (Resolve-Path "$PSScriptRoot/../testfiles/Export-APIViewMarkdown").Path
    $simpleTokensJson = Join-Path $testFilesDir "simple_tokens.json"
}

Describe "Export-APIViewMarkdown" {
    Context "Basic rendering" {
        It "Renders a fenced code block using the language from the JSON" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $content = Get-Content $outFile -Raw
            $content | Should -Match "^``````py"
            $content | Should -Match "``````$"
        }

        It "Renders top-level lines at zero indentation" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $namespaceLine = $lines | Where-Object { $_ -match "^namespace " }
            $namespaceLine | Should -Be "namespace azure.core"
        }

        It "Renders child lines with one level of indentation (4 spaces)" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $classLine = $lines | Where-Object { $_ -match "class MyClass" }
            $classLine | Should -Be "    class MyClass:"
        }

        It "Renders grandchild lines with two levels of indentation (8 spaces)" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $methodLine = $lines | Where-Object { $_ -match "def my_method" }
            $methodLine | Should -Be "        def my_method(self): ..."
        }

        It "Renders blank lines (empty Tokens array) as empty strings" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $blankIndex = -1
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -eq "" -and $i -gt 0 -and $lines[$i - 1] -match "def my_method") {
                    $blankIndex = $i
                    break
                }
            }
            $blankIndex | Should -BeGreaterThan -1
        }

        It "Renders standalone top-level function at zero indentation after blank line" {
            $outFile = Join-Path $TestDrive "output.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $funcLine = $lines | Where-Object { $_ -match "^def standalone_func" }
            $funcLine | Should -Be "def standalone_func(): ..."
        }
    }

    Context "HasSuffixSpace and HasPrefixSpace rendering" {
        It "Applies HasSuffixSpace by appending a space after the token value" {
            $tokenJson = @{
                ReviewLines = @(
                    @{
                        Tokens = @(
                            @{ Kind = 2; Value = "class"; HasPrefixSpace = $false; HasSuffixSpace = $true },
                            @{ Kind = 3; Value = "Foo"; HasPrefixSpace = $false; HasSuffixSpace = $false }
                        )
                        Children = @()
                    }
                )
            } | ConvertTo-Json -Depth 10

            $jsonFile = Join-Path $TestDrive "spacing_suffix.json"
            $outFile = Join-Path $TestDrive "spacing_suffix.md"
            Set-Content $jsonFile $tokenJson

            & $scriptPath -TokenJsonPath $jsonFile -OutputPath $outFile
            $lines = Get-Content $outFile
            $codeLine = $lines | Where-Object { $_ -match "class" }
            # "class" with HasSuffixSpace=true → "class ", then "Foo" → "class Foo"
            $codeLine | Should -Be "class Foo"
        }

        It "Applies HasPrefixSpace by prepending a space before the token value" {
            $tokenJson = @{
                ReviewLines = @(
                    @{
                        Tokens = @(
                            @{ Kind = 0; Value = "x"; HasPrefixSpace = $false; HasSuffixSpace = $false },
                            @{ Kind = 1; Value = "="; HasPrefixSpace = $true; HasSuffixSpace = $true },
                            @{ Kind = 0; Value = "1"; HasPrefixSpace = $false; HasSuffixSpace = $false }
                        )
                        Children = @()
                    }
                )
            } | ConvertTo-Json -Depth 10

            $jsonFile = Join-Path $TestDrive "spacing_prefix.json"
            $outFile = Join-Path $TestDrive "spacing_prefix.md"
            Set-Content $jsonFile $tokenJson

            & $scriptPath -TokenJsonPath $jsonFile -OutputPath $outFile
            $lines = Get-Content $outFile
            $codeLine = $lines | Where-Object { $_ -match "=" }
            # "x" + " =" (prefix) + "= " (suffix) + "1" → "x = 1"
            $codeLine | Should -Be "x = 1"
        }
    }

    Context "Language auto-detection from JSON" {
        It "Reads Language from the JSON and uses it (lowercased) in the fence tag" {
            $outFile = Join-Path $TestDrive "lang.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[0] | Should -Be "``````py"
        }

        It "Uses an empty fence tag when the JSON has no Language field" {
            $tokenJson = @{ ReviewLines = @(@{ Tokens = @(@{ Kind = 0; Value = "x"; HasPrefixSpace = $false; HasSuffixSpace = $false }) }) } | ConvertTo-Json -Depth 10
            $jsonFile = Join-Path $TestDrive "nolang.json"
            $outFile = Join-Path $TestDrive "nolang.md"
            Set-Content $jsonFile $tokenJson

            & $scriptPath -TokenJsonPath $jsonFile -OutputPath $outFile
            $lines = Get-Content $outFile
            $lines[0] | Should -Be "``````"
        }

        It "Closes the fence with a plain triple-backtick" {
            $outFile = Join-Path $TestDrive "close.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[-1] | Should -Be "``````"
        }
    }

    Context "OutputPath defaulting" {
        It "Writes api.md into the directory when OutputPath is an existing directory" {
            $dir = Join-Path $TestDrive "outdir"
            New-Item -ItemType Directory -Path $dir | Out-Null
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $dir

            (Test-Path (Join-Path $dir "api.md")) | Should -Be $true
        }

        It "Writes api.md when OutputPath has no file extension" {
            $noExt = Join-Path $TestDrive "newdir"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $noExt

            (Test-Path (Join-Path $noExt "api.md")) | Should -Be $true
        }

        It "Uses the exact filename when OutputPath includes a .md extension" {
            $outFile = Join-Path $TestDrive "custom.md"
            & $scriptPath -TokenJsonPath $simpleTokensJson -OutputPath $outFile

            (Test-Path $outFile) | Should -Be $true
        }
    }

    Context "Real-world golden file (azure-core Python)" {
        BeforeAll {
            $pythonTokenJson = Join-Path $testFilesDir "azure-core_python.json"
            $pythonGoldenMd   = Join-Path $testFilesDir "azure-core_python.md"
        }

        It "Produces output that exactly matches the golden api.md for azure-core Python" {
            $outFile = Join-Path $TestDrive "azure-core_python_out.md"
            & $scriptPath -TokenJsonPath $pythonTokenJson -OutputPath $outFile

            $expected = Get-Content $pythonGoldenMd -Raw
            $actual   = Get-Content $outFile -Raw
            $actual | Should -Be $expected
        }

        It "Uses the 'py' fence tag for Python (alias mapping)" {
            $outFile = Join-Path $TestDrive "azure-core_python_lang.md"
            & $scriptPath -TokenJsonPath $pythonTokenJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[0] | Should -Be '```py'
        }
    }

    Context "Real-world golden file (azure-core JavaScript)" {
        BeforeAll {
            $jsTokenJson = Join-Path $testFilesDir "azure-core_js.json"
            $jsGoldenMd   = Join-Path $testFilesDir "azure-core_js.md"
        }

        It "Produces output that exactly matches the golden api.md for azure-core JavaScript" {
            $outFile = Join-Path $TestDrive "azure-core_js_out.md"
            & $scriptPath -TokenJsonPath $jsTokenJson -OutputPath $outFile

            $expected = Get-Content $jsGoldenMd -Raw
            $actual   = Get-Content $outFile -Raw
            $actual | Should -Be $expected
        }

        It "Uses the 'js' fence tag for JavaScript (alias mapping)" {
            $outFile = Join-Path $TestDrive "azure-core_js_lang.md"
            & $scriptPath -TokenJsonPath $jsTokenJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[0] | Should -Be '```js'
        }
    }

    Context "Real-world golden file (azure-core Java)" {
        BeforeAll {
            $javaTokenJson = Join-Path $testFilesDir "azure-core_java.json"
            $javaGoldenMd   = Join-Path $testFilesDir "azure-core_java.md"
        }

        It "Produces output that exactly matches the golden api.md for azure-core Java" {
            $outFile = Join-Path $TestDrive "azure-core_java_out.md"
            & $scriptPath -TokenJsonPath $javaTokenJson -OutputPath $outFile

            $expected = Get-Content $javaGoldenMd -Raw
            $actual   = Get-Content $outFile -Raw
            $actual | Should -Be $expected
        }

        It "Uses the 'java' fence tag (no alias mapping for Java)" {
            $outFile = Join-Path $TestDrive "azure-core_java_lang.md"
            & $scriptPath -TokenJsonPath $javaTokenJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[0] | Should -Be '```java'
        }
    }

    Context "Real-world golden file (Azure.Core C#)" {
        BeforeAll {
            $csharpTokenJson = Join-Path $testFilesDir "azure-core_csharp.json"
            $csharpGoldenMd   = Join-Path $testFilesDir "azure-core_csharp.md"
        }

        It "Produces output that exactly matches the golden api.md for Azure.Core C#" {
            $outFile = Join-Path $TestDrive "azure-core_csharp_out.md"
            & $scriptPath -TokenJsonPath $csharpTokenJson -OutputPath $outFile

            $expected = Get-Content $csharpGoldenMd -Raw
            $actual   = Get-Content $outFile -Raw
            $actual | Should -Be $expected
        }

        It "Uses the 'c#' fence tag (no alias mapping for C#)" {
            $outFile = Join-Path $TestDrive "azure-core_csharp_lang.md"
            & $scriptPath -TokenJsonPath $csharpTokenJson -OutputPath $outFile

            $lines = Get-Content $outFile
            $lines[0] | Should -Be '```c#'
        }
    }

    Context "Error handling" {
        It "Exits with an error when the token JSON file does not exist" {
            $outFile = Join-Path $TestDrive "err.md"
            { & $scriptPath -TokenJsonPath "nonexistent.json" -OutputPath $outFile } | Should -Throw
        }

        It "Exits with an error when ReviewLines is missing from the JSON" {
            $badJson = '{ "SomeOtherProperty": [] }'
            $jsonFile = Join-Path $TestDrive "bad.json"
            $outFile = Join-Path $TestDrive "bad.md"
            Set-Content $jsonFile $badJson

            { & $scriptPath -TokenJsonPath $jsonFile -OutputPath $outFile } | Should -Throw
        }
    }
}
