Import-Module Pester

# Pester 5 builds the test list during discovery, before BeforeAll runs, so test
# case data has to live at script scope rather than inside BeforeAll.

# The pull request path matches on the GitHub login of the pull request author.
$pullRequestCases = @(
    @{ Case = 'an allowed alias skips';                     AuthorLogin = 'danieljurek'; Extra = @{};                                        Expected = 'true' }
    @{ Case = 'alias matching is case insensitive';         AuthorLogin = 'DanielJurek'; Extra = @{};                                        Expected = 'true' }
    @{ Case = 'surrounding whitespace is ignored';          AuthorLogin = "  benbp`t";   Extra = @{};                                        Expected = 'true' }
    @{ Case = 'an alias not on the list runs';              AuthorLogin = 'someone-else'; Extra = @{};                                       Expected = 'false' }
    @{ Case = 'a coding agent author runs';                 AuthorLogin = 'Copilot';     Extra = @{};                                        Expected = 'false' }
    @{ Case = 'the agent push identity runs';               AuthorLogin = 'copilot-swe-agent'; Extra = @{};                                  Expected = 'false' }
    @{ Case = 'an allowed email is not a valid alias';      AuthorLogin = 'djurek@microsoft.com'; Extra = @{};                               Expected = 'false' }
    @{ Case = 'AdditionalSkipAliases is honored';           AuthorLogin = 'extra-person'; Extra = @{ AdditionalSkipAliases = 'extra-person' }; Expected = 'true' }
    @{ Case = 'AdditionalSkipAliases splits on commas';     AuthorLogin = 'third';       Extra = @{ AdditionalSkipAliases = 'first,second,third' }; Expected = 'true' }
    @{ Case = 'AdditionalSkipAliases splits on semicolons'; AuthorLogin = 'third';       Extra = @{ AdditionalSkipAliases = 'first;second;third' }; Expected = 'true' }
    @{ Case = 'AdditionalSkipAliases splits on whitespace'; AuthorLogin = 'third';       Extra = @{ AdditionalSkipAliases = 'first second third' }; Expected = 'true' }
    @{ Case = 'AdditionalSkipEmails does not grant a skip'; AuthorLogin = 'extra-person'; Extra = @{ AdditionalSkipEmails = 'extra-person' }; Expected = 'false' }
    # Skip.VerifyCodeowners is a queue time variable that cannot be supplied on a
    # pull request build, so it must not influence the result there.
    @{ Case = 'Skip.VerifyCodeowners cannot grant a skip';  AuthorLogin = 'someone-else'; Extra = @{ SkipVerifyCodeowners = 'true' };         Expected = 'false' }
)

# Builds that are not pull requests match on the requesting Microsoft email, and
# additionally require Skip.VerifyCodeowners to have been set at queue time.
$nonPullRequestCases = @(
    @{ Case = 'allowed email with the skip variable';    Reason = 'Manual';       Email = 'djurek@microsoft.com';  Skip = 'true';  Extra = @{}; Expected = 'true' }
    @{ Case = 'email matching is case insensitive';      Reason = 'Manual';       Email = 'DJurek@Microsoft.com';  Skip = 'true';  Extra = @{}; Expected = 'true' }
    @{ Case = 'allowed email without the skip variable'; Reason = 'Manual';       Email = 'djurek@microsoft.com';  Skip = '';      Extra = @{}; Expected = 'false' }
    @{ Case = 'the skip variable set to false';          Reason = 'Manual';       Email = 'djurek@microsoft.com';  Skip = 'false'; Extra = @{}; Expected = 'false' }
    @{ Case = 'an email not on the list';                Reason = 'Manual';       Email = 'someone@microsoft.com'; Skip = 'true';  Extra = @{}; Expected = 'false' }
    @{ Case = 'an empty email';                          Reason = 'Manual';       Email = '';                      Skip = 'true';  Extra = @{}; Expected = 'false' }
    @{ Case = 'an alias supplied in place of an email';  Reason = 'Manual';       Email = 'danieljurek';           Skip = 'true';  Extra = @{}; Expected = 'false' }
    @{ Case = 'AdditionalSkipEmails is honored';         Reason = 'Manual';       Email = 'extra@microsoft.com';   Skip = 'true';  Extra = @{ AdditionalSkipEmails = 'extra@microsoft.com' }; Expected = 'true' }
    @{ Case = 'AdditionalSkipAliases does not apply';    Reason = 'Manual';       Email = 'extra@microsoft.com';   Skip = 'true';  Extra = @{ AdditionalSkipAliases = 'extra@microsoft.com' }; Expected = 'false' }
)

# Behavior of the GitHub lookup used when the author is not supplied directly.
$authorLookupCases = @(
    @{
        Case     = 'an allowed author is resolved and skips'
        Response = { [pscustomobject]@{ user = [pscustomobject]@{ login = 'danieljurek'; type = 'User' } } }
        Expected = 'true'
        Log      = 'Pull request author: danieljurek'
    }
    @{
        Case     = 'an author that is not allowed runs'
        Response = { [pscustomobject]@{ user = [pscustomobject]@{ login = 'someone-else'; type = 'User' } } }
        Expected = 'false'
        Log      = 'Pull request author: someone-else'
    }
    @{
        Case     = 'a failed request fails closed'
        Response = { throw 'Response status code does not indicate success: 404 (Not Found).' }
        Expected = 'false'
        Log      = "Failed to resolve the author of '\S+'"
    }
    @{
        Case     = 'a rate limited request fails closed'
        Response = { throw 'Response status code does not indicate success: 403 (rate limit exceeded).' }
        Expected = 'false'
        Log      = 'Failed to resolve the author'
    }
    @{
        Case     = 'a response with no author fails closed'
        Response = { [pscustomobject]@{ } }
        Expected = 'false'
        Log      = 'Failed to resolve the author'
    }
    # Agent opened pull requests are attributed to the app, which is never on the
    # allow list. Confirmed against the pulls endpoint: agent pull requests report
    # login 'Copilot' with type 'Bot', humans report type 'User'.
    @{
        Case     = 'an agent opened pull request is called out'
        Response = { [pscustomobject]@{ user = [pscustomobject]@{ login = 'Copilot'; type = 'Bot' } } }
        Expected = 'false'
        Log      = "opened by the 'Copilot' app rather than by a person"
    }
    @{
        Case     = 'the app note is informational and does not change the result'
        Response = { [pscustomobject]@{ user = [pscustomobject]@{ login = 'danieljurek'; type = 'Bot' } } }
        Expected = 'true'
        Log      = "opened by the 'danieljurek' app rather than by a person"
    }
)

# The lookup costs an anonymous GitHub request, which is a limited per-IP budget,
# so it must only happen on pull request builds.
$lookupAvoidanceCases = @(
    @{ Case = 'the build is manual';    Parameters = @{ BuildReason = 'Manual'; RequestedForEmail = 'djurek@microsoft.com'; SkipVerifyCodeowners = 'true' } }
    @{ Case = 'the build is CI';        Parameters = @{ BuildReason = 'IndividualCI'; RequestedForEmail = 'djurek@microsoft.com' } }
    @{ Case = 'the build is batched';   Parameters = @{ BuildReason = 'BatchedCI'; RequestedForEmail = 'djurek@microsoft.com' } }
    @{ Case = 'the build is scheduled'; Parameters = @{ BuildReason = 'Schedule'; RequestedForEmail = 'djurek@microsoft.com' } }
)

BeforeAll {
    $script:ScriptPath = "$PSScriptRoot/../../common/scripts/Set-VerifyCodeownersSkip.ps1"

    # The script is invoked rather than dot sourced, because it runs its decision
    # logic at the top level rather than exposing it as a function.
    #
    # Every parameter that defaults to an environment variable is passed
    # explicitly. Azure DevOps sets BUILD_REASON, BUILD_REPOSITORY_NAME and
    # SYSTEM_PULLREQUEST_PULLREQUESTNUMBER on the agent, so relying on the
    # defaults would let the surrounding build leak in and produce different
    # results locally and in CI.
    function Invoke-Script {
        param (
            [hashtable] $Parameters
        )

        $arguments = @{
            BuildReason          = 'Manual'
            SkipVerifyCodeowners = ''
            RequestedForEmail    = ''
            Repo                 = ''
            PullRequestNumber    = ''
        }

        foreach ($key in $Parameters.Keys) {
            $arguments[$key] = $Parameters[$key]
        }

        # Write-Host writes to the information stream, so every stream is merged
        # to capture logging alongside the result.
        return (& $script:ScriptPath @arguments *>&1 | Out-String)
    }

    # The author is always resolved from GitHub, so pull request scenarios are
    # driven by mocking that request rather than by a parameter that production
    # never supplies.
    function Invoke-PullRequestScript {
        param (
            [hashtable] $Parameters = @{}
        )

        $arguments = @{
            BuildReason       = 'PullRequest'
            Repo              = 'Azure/azure-sdk-tools'
            PullRequestNumber = '1234'
        }

        foreach ($key in $Parameters.Keys) {
            $arguments[$key] = $Parameters[$key]
        }

        return (Invoke-Script $arguments)
    }

    function Get-SkipResult {
        param (
            [string] $Output
        )

        if ($Output -match '##vso\[task\.setvariable variable=ShouldSkipVerifyCodeowners\](\w+)') {
            return $Matches[1]
        }

        return '<not set>'
    }
}

Describe "Set-VerifyCodeownersSkip" -Tag "UnitTest", "Set-VerifyCodeownersSkip" {

    Context "pull request builds" {
        It "<Case>" -ForEach $pullRequestCases {
            # GetNewClosure binds AuthorLogin at creation time. Without it the mock
            # body resolves variables in the scope of the script under test, where
            # a similarly named local would silently shadow the test value.
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = $AuthorLogin; type = 'User' } } }.GetNewClosure()

            Get-SkipResult (Invoke-PullRequestScript $Extra) | Should -Be $Expected
        }
    }

    Context "builds that are not pull requests" {
        It "<Case>" -ForEach $nonPullRequestCases {
            $parameters = @{
                BuildReason          = $Reason
                RequestedForEmail    = $Email
                SkipVerifyCodeowners = $Skip
            } + $Extra

            Get-SkipResult (Invoke-Script $parameters) | Should -Be $Expected
        }
    }

    Context "resolving the author from GitHub" {
        It "<Case>" -ForEach $authorLookupCases {
            Mock Invoke-RestMethod -MockWith $Response

            $output = Invoke-PullRequestScript

            Get-SkipResult $output | Should -Be $Expected
            $output | Should -Match $Log
        }

        It "requests the pull request from the expected endpoint" {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = 'octocat'; type = 'User' } } }

            Invoke-PullRequestScript | Out-Null

            Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
                $Uri -eq 'https://api.github.com/repos/Azure/azure-sdk-tools/pulls/1234'
            }
        }

        It "bounds retries, because every attempt spends rate limit budget" {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = 'octocat'; type = 'User' } } }

            Invoke-PullRequestScript | Out-Null

            Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
                $MaximumRetryCount -le 3
            }
        }

        It "does not call a human authored pull request an app" {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = 'danieljurek'; type = 'User' } } }

            Invoke-PullRequestScript | Should -Not -Match 'rather than by a person'
        }

        It "does not fail the build when GitHub is unavailable" {
            Mock Invoke-RestMethod { throw 'the remote name could not be resolved' }

            Invoke-PullRequestScript | Out-Null

            $? | Should -BeTrue
        }
    }

    Context "avoiding unnecessary GitHub requests" {
        It "does not call GitHub when <Case>" -ForEach $lookupAvoidanceCases {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = 'danieljurek'; type = 'User' } } }

            $parameters = $Parameters + @{
                Repo              = 'Azure/azure-sdk-tools'
                PullRequestNumber = '1234'
            }

            Invoke-Script $parameters | Out-Null

            Should -Invoke Invoke-RestMethod -Times 0 -Exactly
        }
    }

    Context "the output variable" {
        It "is set when <Case>" -ForEach @(
            @{ Case = 'skipping'; AuthorLogin = 'danieljurek';  Expected = 'true' }
            @{ Case = 'running';  AuthorLogin = 'someone-else'; Expected = 'false' }
        ) {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = $AuthorLogin; type = 'User' } } }.GetNewClosure()

            Get-SkipResult (Invoke-PullRequestScript) | Should -Be $Expected
        }

        It "honors OutputVariableName" {
            Mock Invoke-RestMethod { [pscustomobject]@{ user = [pscustomobject]@{ login = 'danieljurek'; type = 'User' } } }

            $output = Invoke-PullRequestScript @{ OutputVariableName = 'CustomVariable' }

            $output | Should -Match '##vso\[task\.setvariable variable=CustomVariable\]true'
        }
    }
}
