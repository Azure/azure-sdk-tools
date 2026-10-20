# Exercises the real publisher with fake Azure transports and loopback HTTP, never evaluations.
BeforeAll {
    $script:publisher = Join-Path $PSScriptRoot '../../common/scripts/eval/publisher/Publish-EvalResults.ps1'
    # Keep the cmdlet itself: Pester can intercept module-qualified calls too.
    $script:realWebRequest = Get-Command Microsoft.PowerShell.Utility\Invoke-WebRequest -CommandType Cmdlet
    $script:nodePath = (Get-Command node -CommandType Application | Select-Object -First 1).Source
    $script:realAzPath = (Get-Command az -CommandType Application | Select-Object -First 1).Source
    function az {
        param([Parameter(ValueFromRemainingArguments)] [string[]] $Arguments)
        throw 'Real Azure CLI calls are forbidden in these tests.'
    }
    $claims = @{ tid = '11111111-1111-1111-1111-111111111111'; oid = '22222222-2222-2222-2222-222222222222' }
    $payload = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress))).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    $script:identityToken = "header.$payload.signature"
    $script:owner = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("$($claims.tid):$($claims.oid)"))).ToLowerInvariant()
}
AfterAll { Remove-Variable -Name EvalPublicationFixture -Scope Global -ErrorAction SilentlyContinue }

Describe 'Azure CLI evaluation publication' -Tag 'UnitTest' {
    BeforeEach {
        # Mocks called from the publisher have another script scope; use one isolated fixture.
        $id = [Guid]::NewGuid().ToString()
        $script:f = $global:EvalPublicationFixture = @{
            bundle = (Join-Path $TestDrive "$id.zip"); logs = @(); stored = $false
            calls = @(); uploadFails = $false; showFails = $false; storageToken = $script:identityToken
            notifyTokenFails = $false; status = 200; headers = @{}; responseFailures = 0; httpCalls = 0
            realWebRequest = $script:realWebRequest
            nodePath = $script:nodePath
            realAzPath = $script:realAzPath
            dashboardUrl = 'https://synthetic-dashboard.test-region.azurewebsites.net/api/refresh'
            dashboardResource = 'api://00000000-0000-4000-8000-000000000042'
            name = 'v1/azure-sdk/internal/8255/1001/1/dashboard-bundle.zip'
        }
        $f.bytes = [Text.Encoding]::UTF8.GetBytes('SAVED_ARCHIVE_FIXTURE_NO_EVALUATIONS')
        [IO.File]::WriteAllBytes($f.bundle, $f.bytes)
        $f.hash = (Get-FileHash -LiteralPath $f.bundle -Algorithm SHA256).Hash.ToLowerInvariant()
        @{ blobName = $f.name; sha256 = $f.hash; bytes = $f.bytes.Length } | ConvertTo-Json | Set-Content -LiteralPath "$($f.bundle).json"
        $f.existing = @{ properties = @{ contentLength = $f.bytes.Length }; metadata = @{
            schema = '1'; sha256 = $f.hash; publisher = $script:owner; storedat = '2026-09-21T00:00:00.000Z'
        } }
        $f.arguments = @{ StorageAccountName = 'evaltestsummary'; BundlePath = $f.bundle;
            DashboardRefreshUrl = $f.dashboardUrl; DashboardResource = $f.dashboardResource }
        $script:publishArgs = $f.arguments

        Mock az {
            param([string[]] $Arguments)
            $f = $global:EvalPublicationFixture; $f.calls += ,@($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments[0] -eq 'account' -and $Arguments[1] -eq 'get-access-token') {
                if ($Arguments -contains 'https://storage.azure.com/') { return @{ accessToken = $f.storageToken } | ConvertTo-Json -Compress }
                if ($Arguments -notcontains $f.dashboardResource) { throw 'Unexpected token destination.' }
                if ($f.notifyTokenFails) { $global:LASTEXITCODE = 1; return 'SECRET_TOKEN_ERROR_MUST_NOT_APPEAR' }
                return '{"accessToken":"fake-notification-token"}'
            }
            if ($Arguments[0] -ne 'storage' -or $Arguments[1] -ne 'blob') { throw 'Unexpected CLI command.' }
            if ($Arguments[2] -eq 'upload') {
                if ($f.uploadFails) { $global:LASTEXITCODE = 1; return 'PRIVATE_UPLOAD_RESPONSE_MUST_NOT_APPEAR' }
                $f.stored = $true
                return '{"etag":"fake-etag"}'
            }
            if ($Arguments[2] -eq 'show') {
                if ($f.showFails) { $global:LASTEXITCODE = 1; return 'PRIVATE_STORAGE_RESPONSE_MUST_NOT_APPEAR' }
                $f.stored = $true
                return $f.existing | ConvertTo-Json -Depth 5 -Compress
            }
            throw 'Unexpected storage operation; delete/overwrite/container creation is forbidden.'
        }
        Mock Invoke-WebRequest {
            $f = $global:EvalPublicationFixture; $f.httpCalls++
            $f.stored | Should -BeTrue
            @($f.logs | Where-Object { $_ -like 'Result archive stored:*' }).Count | Should -Be 1
            $Uri.AbsoluteUri | Should -BeExactly $f.dashboardUrl
            $Headers.Authorization | Should -Be 'Bearer fake-notification-token'
            $MaximumRedirection | Should -Be 0; $ConnectionTimeoutSeconds | Should -Be 180; $SkipHttpErrorCheck | Should -BeTrue
            ([Text.Encoding]::UTF8.GetByteCount($Body) -le 2048) | Should -BeTrue
            $signal = $Body | ConvertFrom-Json -AsHashtable
            @($signal.Keys | Sort-Object) | Should -Be @('blobName', 'sha256')
            $signal.blobName | Should -Be $f.name; $signal.sha256 | Should -Be $f.hash
            return [pscustomobject]@{ StatusCode = $f.status; Headers = $f.headers; Content = (@{ status = 'succeeded'; failureCount = $f.responseFailures } | ConvertTo-Json -Compress) }
        }
        Mock Write-Host { $global:EvalPublicationFixture.logs += [string]$Object }
        Mock Start-Sleep { }
    }

    It 'uploads saved bytes through the logged-in CLI with create-only conditions and metadata' {
        & $script:publisher @script:publishArgs
        $upload = @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'upload' })
        $upload.Count | Should -Be 1
        $arguments = $upload[0]
        $arguments[$arguments.IndexOf('--account-name') + 1] | Should -Be 'evaltestsummary'
        $arguments[$arguments.IndexOf('--container-name') + 1] | Should -Be 'vally-results'
        $arguments[$arguments.IndexOf('--file') + 1] | Should -Be $f.bundle
        $arguments[$arguments.IndexOf('--auth-mode') + 1] | Should -Be 'login'
        $arguments[$arguments.IndexOf('--overwrite') + 1] | Should -Be 'false'
        $arguments[$arguments.IndexOf('--if-none-match') + 1] | Should -Be '*'
        $arguments | Should -Contain '--no-progress'
        $arguments | Should -Contain 'schema=1'; $arguments | Should -Contain "sha256=$($f.hash)"
        $arguments | Should -Contain "publisher=$script:owner"
        ($arguments | Where-Object { $_ -like 'storedat=*' }) | Should -Match 'Z$'
        $f.logs | Should -Contain "Result archive stored: $($f.name) (duplicate: False)."
        $f.logs | Should -Contain 'Dashboard refresh succeeded.'
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
        (Get-FileHash $f.bundle).Hash.ToLowerInvariant() | Should -Be $f.hash
    }

    It 'adopts an identical existing archive after collision or a lost upload response' {
        $f.uploadFails = $true
        & $script:publisher @script:publishArgs
        $f.logs | Should -Contain "Result archive stored: $($f.name) (duplicate: True)."
        $f.logs | Should -Contain 'Dashboard refresh succeeded.'
        $f.existing.metadata.storedat | Should -Be '2026-09-21T00:00:00.000Z'
        @($f.calls | Where-Object { $_[0] -eq 'storage' }).Count | Should -Be 2
    }

    It 'accepts canonical UTC metadata and preserves calendar/fractional precision' -ForEach @(
        @{ Value = '2024-02-29T23:59:59Z' }, @{ Value = '2026-01-01T00:00Z' },
        @{ Value = '2026-01-01T00:00:00.1Z' }, @{ Value = '2026-01-01T00:00:00.1234567Z' },
        @{ Value = '2026-01-01T00:00:00.123456789Z' }
    ) {
        $f.uploadFails = $true; $f.existing.metadata.storedat = $Value
        & $script:publisher @script:publishArgs
        $f.existing.metadata.storedat | Should -BeExactly $Value
        $f.logs | Should -Contain "Result archive stored: $($f.name) (duplicate: True)."
    }

    It 'never adopts a collision with different <Field>' -ForEach @(
        @{ Field = 'schema'; Value = '2' }, @{ Field = 'sha256'; Value = 'bad' }, @{ Field = 'publisher'; Value = 'other' },
        @{ Field = 'storedat'; Value = $null }, @{ Field = 'storedat'; Value = 'not a date' },
        @{ Field = 'storedat'; Value = '2026-02-30T00:00:00Z' }, @{ Field = 'storedat'; Value = '2026-01-01T24:00:00Z' },
        @{ Field = 'storedat'; Value = '2026-01-01T00:00:00+00:00' }, @{ Field = 'storedat'; Value = '2026-01-01T 01:00:00Z' },
        @{ Field = 'storedat'; Value = '2026-01-01T00:00:00 Z' }, @{ Field = 'storedat'; Value = "2026-01-01T00:00:00Z`n" }
    ) {
        $f.uploadFails = $true; $f.existing.metadata[$Field] = $Value
        { & $script:publisher @script:publishArgs } | Should -Throw '*submission_conflict*'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
        @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'upload' }).Count | Should -Be 1
    }

    It 'rejects a collision with different size or missing metadata' -ForEach @(@{ Missing = $false }, @{ Missing = $true }) {
        $f.uploadFails = $true
        if ($Missing) { $f.existing.Remove('metadata') } else { $f.existing.properties.contentLength++ }
        { & $script:publisher @script:publishArgs } | Should -Throw '*submission_conflict*'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
    }

    It 'fails unavailable storage without notifying or logging raw CLI output' {
        $f.uploadFails = $true; $f.showFails = $true
        $message = ''
        try { & $script:publisher @script:publishArgs } catch { $message = $_.Exception.Message }
        $message | Should -Match 'publication_failed'
        $message | Should -Not -Match 'PRIVATE_|SECRET_|Bearer'
        $f.logs | Should -BeNullOrEmpty
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
    }

    It 'keeps native stderr out of successful JSON, including token responses' {
        Mock az {
            param([string[]] $Arguments)
            $f = $global:EvalPublicationFixture; $f.calls += ,@($Arguments)
            $global:LASTEXITCODE = 0
            Write-Error 'SYNTHETIC_PRIVATE_STDERR_NOT_JSON' -ErrorAction Continue
            if ($Arguments[0] -eq 'account') {
                return @{ accessToken = $(if ($Arguments -contains 'https://storage.azure.com/') { $f.storageToken } else { 'fake-notification-token' }) } | ConvertTo-Json -Compress
            }
            $Arguments[2] | Should -Be 'upload'
            $f.stored = $true
            return '{"etag":"fake-etag"}'
        }
        & $script:publisher @script:publishArgs
        $f.logs | Should -Contain 'Dashboard refresh succeeded.'
        ($f.logs -join ' ') | Should -Not -Match 'SYNTHETIC_PRIVATE_STDERR'
        @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'show' }).Count | Should -Be 0
    }

    It 'reports only fixed upload and verification error codes, never raw CLI content' {
        Mock az {
            param([string[]] $Arguments)
            $f = $global:EvalPublicationFixture
            if ($Arguments[0] -eq 'account') {
                $global:LASTEXITCODE = 0
                return @{ accessToken = $f.storageToken } | ConvertTo-Json -Compress
            }
            $global:LASTEXITCODE = 1
            if ($Arguments[2] -eq 'upload') { return 'ERROR: (AuthorizationPermissionMismatch) PRIVATE_BEARER_SECRET https://private.example/?sig=SECRET' }
            return 'ERROR: (BlobNotFound) PRIVATE_RESPONSE_CONTENT'
        }
        $message = ''
        try { & $script:publisher @script:publishArgs } catch { $message = $_.Exception.Message }
        $message | Should -Match 'Upload: exit=1, code=AuthorizationPermissionMismatch'
        $message | Should -Match 'Current operation: exit=1, code=BlobNotFound'
        $message | Should -Not -Match 'PRIVATE_|SECRET|https://|Bearer'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
    }

    It 'sanitizes invalid successful CLI JSON without treating it as stored data' {
        Mock az {
            param([string[]] $Arguments)
            $f = $global:EvalPublicationFixture
            if ($Arguments[0] -eq 'account') {
                $global:LASTEXITCODE = 0
                return @{ accessToken = $f.storageToken } | ConvertTo-Json -Compress
            }
            if ($Arguments[2] -eq 'show') { $global:LASTEXITCODE = 1; return 'ERROR: (BlobNotFound) SECRET_SHOW_RESPONSE' }
            $global:LASTEXITCODE = 0
            return 'SECRET_INVALID_JSON'
        }
        $message = ''
        try { & $script:publisher @script:publishArgs } catch { $message = $_.Exception.Message }
        $message | Should -Match 'Upload: exit=0, code=invalid_cli_json'
        $message | Should -Not -Match 'SECRET_'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
    }

    It 'publishes through the real Azure CLI against loopback without a fallback read' -Tag 'IntegrationTest' {
        $f.arguments.StorageAccountName = 'loopbackfixture'
        $f.arguments.DashboardRefreshUrl = ''
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $f.nodePath
        $start.UseShellExecute = $false; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        $start.ArgumentList.Add('--input-type=module'); $start.ArgumentList.Add('-e')
        $start.ArgumentList.Add(@'
import {createServer} from 'node:http';
let size=0, metadata={};
const server=createServer((req,res)=>{
  console.log(req.method);
  if(req.method==='PUT'){
    metadata=Object.fromEntries(Object.entries(req.headers).filter(([key])=>key.startsWith('x-ms-meta-')));
    req.on('data',chunk=>size+=chunk.length);
    req.on('end',()=>{res.writeHead(201,{'x-ms-request-id':'fixture-request','x-ms-version':'2023-11-03',ETag:'"fixture-etag"','Last-Modified':new Date().toUTCString(),'x-ms-request-server-encrypted':'true'});res.end();});
  }else if(req.method==='HEAD'){
    res.writeHead(200,{...metadata,'Content-Length':size,'x-ms-blob-type':'BlockBlob','x-ms-request-id':'fixture-read',ETag:'"fixture-etag"','Last-Modified':new Date().toUTCString()});res.end();
  }else{res.writeHead(400);res.end();}
});
server.listen(0,'127.0.0.1',()=>console.log(server.address().port));
setTimeout(()=>{server.closeAllConnections();server.close();},60000).unref();
'@)
        $server = [Diagnostics.Process]::Start($start)
        $oldConfig = $env:AZURE_CONFIG_DIR; $oldTelemetry = $env:AZURE_CORE_COLLECT_TELEMETRY; $oldProxy = $env:NO_PROXY
        try {
            $line = $server.StandardOutput.ReadLineAsync()
            if (!$line.Wait(5000)) { throw 'CLI loopback fixture did not start.' }
            $f.storageEndpoint = "http://127.0.0.1:$([int]$line.Result)"
            $env:AZURE_CONFIG_DIR = Join-Path $TestDrive 'isolated-cli'
            $env:AZURE_CORE_COLLECT_TELEMETRY = '0'; $env:NO_PROXY = '127.0.0.1,localhost'
            Mock az {
                param([string[]] $Arguments)
                $f = $global:EvalPublicationFixture; $f.calls += ,@($Arguments)
                if ($Arguments[0] -eq 'account') {
                    $global:LASTEXITCODE = 0
                    return @{ accessToken = $f.storageToken } | ConvertTo-Json -Compress
                }
                ([uri]$f.storageEndpoint).Host | Should -Be '127.0.0.1'
                $Arguments[0] | Should -Be 'storage'; $Arguments[1] | Should -Be 'blob'
                $Arguments[2] | Should -BeIn @('upload', 'show')
                # No login, account keys or production endpoint: replace only the test transport.
                $native = [Collections.Generic.List[string]]::new()
                for ($i = 0; $i -lt $Arguments.Length; $i++) {
                    if ($Arguments[$i] -eq '--auth-mode') { $i++; continue }
                    $native.Add($Arguments[$i])
                }
                $native.AddRange([string[]]@('--blob-endpoint', $f.storageEndpoint, '--sas-token', 'sig=synthetic-not-a-credential'))
                $nativeOutput = & $f.realAzPath @native 2>&1
                $nativeExit = $LASTEXITCODE
                if ($nativeExit -ne 0) {
                    # This command has only synthetic input and a localhost destination.
                    # Show argument-parser messages only; never relax the upload assertion.
                    $parserError = @($nativeOutput | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^ERROR: (unrecognized arguments:|argument )' }) -join ' '
                    if ($parserError) { Write-Warning "Localhost CLI parser failure: $parserError" -WarningAction Continue }
                }
                $global:LASTEXITCODE = $nativeExit
                if ($nativeExit -eq 0 -and $Arguments[2] -eq 'upload') { $f.stored = $true }
                $nativeOutput
            }
            & $script:publisher @script:publishArgs -WarningAction SilentlyContinue
            $f.logs | Should -Contain "Result archive stored: $($f.name) (duplicate: False)."
            @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'show' }).Count | Should -Be 0
            $server.Kill($true); $server.WaitForExit()
            @($server.StandardOutput.ReadToEnd().Trim() -split '\r?\n') | Should -Be @('PUT')
            Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
        } finally {
            if (!$server.HasExited) { $server.Kill($true); $server.WaitForExit() }
            $server.Dispose()
            $env:AZURE_CONFIG_DIR = $oldConfig; $env:AZURE_CORE_COLLECT_TELEMETRY = $oldTelemetry; $env:NO_PROXY = $oldProxy
        }
    }

    It 'publishes without writing local status files or changing the saved archive and descriptor' {
        $before = @(Get-ChildItem -LiteralPath $TestDrive -Recurse -File | Select-Object -ExpandProperty FullName | Sort-Object)
        $descriptor = Get-Content -LiteralPath "$($f.bundle).json" -Raw
        & $script:publisher @script:publishArgs
        @(Get-ChildItem -LiteralPath $TestDrive -Recurse -File | Select-Object -ExpandProperty FullName | Sort-Object) | Should -Be $before
        Get-Content -LiteralPath "$($f.bundle).json" -Raw | Should -BeExactly $descriptor
        (Get-FileHash $f.bundle -Algorithm SHA256).Hash.ToLowerInvariant() | Should -Be $f.hash
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
    }

    It 'treats notification HTTP <Status> as permanent while retaining stored success' -ForEach @(@{ Status = 302 }, @{ Status = 401 }, @{ Status = 403 }) {
        $f.status = $Status
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        "$warnings" | Should -Match 'dashboard refresh failed'
        $f.stored | Should -BeTrue
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
        Assert-MockCalled Start-Sleep -Times 0 -Exactly
    }

    It 'recognizes the real redirect exception without a Response property' {
        Mock Invoke-WebRequest {
            $exception = [InvalidOperationException]::new('Synthetic MaximumRedirection failure')
            $record = [Management.Automation.ErrorRecord]::new($exception,
                'MaximumRedirectExceeded,Microsoft.PowerShell.Commands.InvokeWebRequestCommand',
                [Management.Automation.ErrorCategory]::InvalidOperation, $null)
            throw $record
        }
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        "$warnings" | Should -Match 'dashboard refresh failed'
        $f.stored | Should -BeTrue
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
        Assert-MockCalled Start-Sleep -Times 0 -Exactly
    }

    It 'does not retry or follow a real loopback HTTP <Status> redirect' -Tag 'IntegrationTest' -ForEach @(@{ Status = 302 }, @{ Status = 307 }) {
        # Hosted agents can expose both UseNode's executable and a preinstalled Node.
        # Reproduce that shape so the fixture never joins multiple paths into one filename.
        Mock Get-Command {
            $f = $global:EvalPublicationFixture
            return @([pscustomobject]@{ Source = $f.nodePath }, [pscustomobject]@{ Source = 'second-node-path-must-not-be-used' })
        } -ParameterFilter { $Name -eq 'node' -and $CommandType -eq 'Application' }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = (Get-Command node -CommandType Application | Select-Object -First 1).Source
        $start.UseShellExecute = $false; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        $start.ArgumentList.Add('--input-type=module'); $start.ArgumentList.Add('-e')
        $start.ArgumentList.Add(@"
import {createServer} from 'node:http';
const server=createServer((req,res)=>{
  console.log(req.url);
  res.writeHead($Status,{'Content-Type':'application/json',Location:'/must-not-follow'});res.end('{}');
});
server.listen(0,'127.0.0.1',()=>console.log(server.address().port));
setTimeout(()=>{server.closeAllConnections();server.close();},15000).unref();
"@)
        $server = [Diagnostics.Process]::Start($start)
        try {
            $line = $server.StandardOutput.ReadLineAsync()
            if (!$line.Wait(5000)) { throw 'Loopback fixture did not start.' }
            $port = [int]$line.Result
            $f.loopbackUri = "http://127.0.0.1:$port/probe"
            Mock Invoke-WebRequest {
                $f = $global:EvalPublicationFixture
                $Uri.AbsoluteUri | Should -BeExactly $f.dashboardUrl
                ([uri]$f.loopbackUri).Host | Should -Be '127.0.0.1'
                $cmdlet = $f.realWebRequest
                return & $cmdlet -Uri $f.loopbackUri -Method Post `
                    -ContentType $ContentType -Body $Body -Headers $Headers -MaximumRedirection $MaximumRedirection `
                    -ConnectionTimeoutSeconds 5 -OperationTimeoutSeconds 5 -SkipHttpErrorCheck
            }
            & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
            Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
            Assert-MockCalled Start-Sleep -Times 0 -Exactly
            "$warnings" | Should -Match 'dashboard refresh failed'
            $f.stored | Should -BeTrue
            $server.Kill($true); $server.WaitForExit()
            @($server.StandardOutput.ReadToEnd().Trim() -split '\r?\n') | Should -Be @('/probe')
        } finally {
            if (!$server.HasExited) { $server.Kill($true); $server.WaitForExit() }
            $server.Dispose()
        }
    }

    It 'bounds transient notification retries to four and honors capped backpressure' {
        $f.status = 429; $f.headers = @{ 'Retry-After' = '9999' }
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        Assert-MockCalled Invoke-WebRequest -Times 4 -Exactly
        Assert-MockCalled Start-Sleep -Times 3 -Exactly -ParameterFilter { $Milliseconds -eq 60000 }
        "$warnings" | Should -Match 'dashboard refresh failed'
        $f.stored | Should -BeTrue
    }

    It 'ignores non-finite Retry-After values without failing an already-stored build' -ForEach @(@{ Value = 'NaN' }, @{ Value = 'Infinity' }) {
        $f.status = 429; $f.headers = @{ 'Retry-After' = $Value }
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        Assert-MockCalled Invoke-WebRequest -Times 4 -Exactly
        Assert-MockCalled Start-Sleep -Times 3 -Exactly
        "$warnings" | Should -Match 'dashboard refresh failed'
        $f.stored | Should -BeTrue
    }

    It 'accepts a later successful signal without re-uploading' {
        Mock Invoke-WebRequest {
            $f = $global:EvalPublicationFixture; $f.httpCalls++
            return [pscustomobject]@{ StatusCode = $(if ($f.httpCalls -eq 1) { 503 } else { 200 }); Headers = @{}; Content = '{"status":"succeeded","failureCount":0}' }
        }
        & $script:publisher @script:publishArgs
        Assert-MockCalled Invoke-WebRequest -Times 2 -Exactly
        @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'upload' }).Count | Should -Be 1
        $f.logs | Should -Contain 'Dashboard refresh succeeded.'
    }

    It 'does not count an import-failure response as notification success' {
        $f.responseFailures = 1
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        Assert-MockCalled Invoke-WebRequest -Times 4 -Exactly
        "$warnings" | Should -Match 'dashboard refresh failed'
        $f.logs | Should -Not -Contain 'Dashboard refresh succeeded.'
    }

    It 'notification token failure cannot erase stored success or leak the token error' {
        $f.notifyTokenFails = $true
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
        $f.stored | Should -BeTrue
        "$warnings" | Should -Match 'dashboard refresh failed'
        ($f.logs -join ' ') + "$warnings" | Should -Not -Match 'SECRET_|fake-notification-token'
    }

    It 'uses the configured dashboard URL and resource instead of production constants' {
        $f.dashboardUrl = 'https://another-dashboard.azurewebsites.net/api/refresh'
        $f.dashboardResource = 'api://11111111-1111-4111-8111-111111111111'
        $f.arguments.DashboardRefreshUrl = $f.dashboardUrl
        $f.arguments.DashboardResource = $f.dashboardResource
        & $script:publisher @script:publishArgs
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly
        $tokenCalls = @($f.calls | Where-Object { $_[0] -eq 'account' -and $_ -contains $f.dashboardResource })
        $tokenCalls.Count | Should -Be 1
        $f.logs | Should -Contain 'Dashboard refresh succeeded.'
    }

    It 'retains uploaded results without requesting or sending a token for invalid <Field>' -ForEach @(
        @{ Field = 'DashboardRefreshUrl'; Value = '' },
        @{ Field = 'DashboardRefreshUrl'; Value = '$(EvalDashboardRefreshUrl)' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'http://synthetic-dashboard.azurewebsites.net/api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://attacker.example/api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net.attacker.example/api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://user:password@synthetic-dashboard.azurewebsites.net/api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net:444/api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net/api/refresh?url=other' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net/api/refresh#fragment' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net/not-refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net/extra/../api/refresh' },
        @{ Field = 'DashboardRefreshUrl'; Value = 'https://synthetic-dashboard.azurewebsites.net/api/refresh ' },
        @{ Field = 'DashboardResource'; Value = '' },
        @{ Field = 'DashboardResource'; Value = '$(EvalDashboardResource)' },
        @{ Field = 'DashboardResource'; Value = 'https://attacker.example/.default' },
        @{ Field = 'DashboardResource'; Value = 'api://not-a-guid' },
        @{ Field = 'DashboardResource'; Value = 'api://00000000-0000-4000-8000-000000000042/.default' }
    ) {
        $f.arguments[$Field] = $Value
        & $script:publisher @script:publishArgs -WarningVariable warnings -WarningAction SilentlyContinue
        $f.stored | Should -BeTrue
        "$warnings" | Should -Match 'configuration is missing or invalid'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly
        Assert-MockCalled Start-Sleep -Times 0 -Exactly
        @($f.calls | Where-Object { $_[0] -eq 'account' -and $_ -notcontains 'https://storage.azure.com/' }).Count | Should -Be 0
        @($f.calls | Where-Object { $_[0] -eq 'storage' -and $_[2] -eq 'upload' }).Count | Should -Be 1
    }

    It 'validates storage identity before upload' {
        $f.storageToken = 'bad-token'
        { & $script:publisher @script:publishArgs } | Should -Throw '*storage_identity*'
        @($f.calls | Where-Object { $_[0] -eq 'storage' }).Count | Should -Be 0
    }

    It 'rejects invalid account names before calling Azure CLI' -ForEach @(
        @{ Account = 'https://other.example' }, @{ Account = 'bad;command' }, @{ Account = 'a' }, @{ Account = 'name with spaces' }
    ) {
        $f.arguments.StorageAccountName = $Account
        { & $script:publisher @script:publishArgs } | Should -Throw
        Assert-MockCalled az -Times 0 -Exactly
    }

    It 'checks saved checksum, identity and size before token acquisition' -ForEach @(
        @{ Field = 'sha256'; Value = 'bad' }, @{ Field = 'blobName'; Value = '../escape' }, @{ Field = 'bytes'; Value = 0 }
    ) {
        $descriptor = Get-Content "$($f.bundle).json" -Raw | ConvertFrom-Json -AsHashtable
        $descriptor[$Field] = $Value
        $descriptor | ConvertTo-Json | Set-Content "$($f.bundle).json"
        { & $script:publisher @script:publishArgs } | Should -Throw '*invalid_bundle*'
        Assert-MockCalled az -Times 0 -Exactly
    }

}
