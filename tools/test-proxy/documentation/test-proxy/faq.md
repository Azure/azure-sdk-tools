# `Test-Proxy` Frequently Asked Questions

## My `push` failed with a `403` error

This is likely due to either:

- The dev not having `write` permission to `Azure/azure-sdk-assets`
- No cached microsoft `git` credentials in the shell the dev runs `test-proxy push` from.
- Being in `codespaces` where the dev's git user isn't available in the default session.

### How to check the `git` user is properly logged in

On the same shell that the dev will `test-proxy push` from:

```bash
git clone -c core.longpaths=true --no-checkout --filter=tree:0 https://github.com/Azure/azure-sdk-assets.git
git checkout -b a-test-branch
git push -u origin a-test-branch # users should clean up after pushing a test branch, but in the case they don't devs may need to select a different test branch name
```

If this _fails_, credentials are either not cached in the `credential manager` OR the user doesn't have write access to the repo.

If this _succeeds_, then the `test-proxy push` run from the same shell session _should succeed as well_.

> Devs should ensure they navigate to `Azure/azure-sdk-assets` and clean up the test branch they just successfully pushed.

### How to get write access to `azure-sdk-assets`

Visit [aka.ms/azsdk/access](https://aka.ms/azsdk/access) and ensure the selected git user has **WRITE** access.

### How to set environment variables to enable codespaces push

- Obtain a PAT for your user from `github.com`.
 - Ensure that the fine-grained token has `Read and Write access to code` in either all repositories or just `Azure/azure-sdk-assets`.
- Set the following environment variables in your codespaces

| Environment Variable | Description |
|---|---|
|`GIT_TOKEN`| Set the value from the PAT from `github.com` obtained above. |
|`GIT_COMMIT_OWNER`| This is the full name of the user. This should be the same as the git variable `GIT_COMMITTER_NAME`. |
|`GIT_COMMIT_EMAIL`| This is the email of the user. This should be same as the git variable `GIT_COMMITTER_EMAIL`.|

## I successfully recorded, but when I push nothing happens and my `assets.json` doesn't get updated

This is caused by one of two issues:

- The user didn't properly re-record tests
- A `push` was _successful_ but somehow the user `assets.json` wasn't updated
  - The most likely case here is that the update to the tag in `assets.json` WAS updated, but the user accidentally the updated `assets.json` tag change via `git checkout`. Due to nonpresence of _pending_ recording changes, further `test-proxy push` calls aren't pushing a new tag or updating `assets.json`.

### The user didn't record new tests

The `test-proxy` does _not_ push a new tag when no recording changes are detected. To verify that the tests are actually recording properly, first devs should invoke their tests in `RECORD` mode. How that should be done for `azure-sdk` devs is documented in each language repo's `CONTRIBUTING.md`. After running the tests, locate the `assets.json` associated with the package. This is normally located at the root of the package code.

Once the dev knows which `assets.json` is associated with their package, use that `assets.json` to interrogate which `.assets` folder is the one in question.

> For a primer on `asset-sync` concepts, feel free to read [this document](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/documentation/asset-sync/README.md). For the purposes of this FAQ, just understand that every `assets.json`, once `restored`, will have a folder under `.assets` folder at the root of the language repo containing all the recordings for _just that `assets.json`_.

```bash
C:/repo/azure-sdk-for-java|>test-proxy config locate -a ./sdk/tables/azure-data-tables/assets.json
Running proxy version is Azure.Sdk.Tools.TestProxy 1.0.0-dev.20250226.1
git --version
C:/repo/azure-sdk-for-java/.assets/v5O3LEDhWR/java # <-- this is the location of the cloned assets
C:/repo/azure-sdk-for-java|>cd C:/repo/azure-sdk-for-java/.assets/v5O3LEDhWR/java
C:/repo/azure-sdk-for-java/.assets/v5O3LEDhWR/java|>git status
HEAD detached at java/tables/azure-data-tables_a4db4b064a
You are in a sparse checkout with 98% of tracked files present.

Changes not staged for commit:
  (use "git add <file>..." to update what will be committed)
  (use "git restore <file>..." to discard changes in working directory)
        modified:   sdk/tables/azure-data-tables/src/test/resources/session-records/TableAsyncClientTest.submitTransaction.json
        modified:   sdk/tables/azure-data-tables/src/test/resources/session-records/TableAsyncClientTest.submitTransactionAllActions.json
        modified:   sdk/tables/azure-data-tables/src/test/resources/session-records/TableAsyncClientTest.submitTransactionAllActionsForEntitiesWithSingleQuotesInPartitionKey.json
...
```

In the example above, one can visibly see that the `recordings` have been updated by the record action.

**Ensure return to a folder within the language repo before invoking `test-proxy push path/to/assets.json`.** The test-proxy is not intended to be run from within an `.assets` directory.

### I successfully pushed, but accidentally discarded `assets.json` tag updates

There are a couple ways to go about this:

- First, devs could check the `Azure/azure-sdk-assets` repo for the tag just pushed, then manually update their `assets.json` to reference the new tag.
- Second, devs "touch" a file within an .assets repo. This puts the .assets slice in a pushable state where `test-proxy push` will actually push and update the target tag.

For option two, devs should use the `test-proxy config locate -a path/to/assets.json`, then:

- `cd` to that directory
- Choose a file randomly, add a newline or something similarly meaningless within a recording file
- Check `git status` from within the `.assets` path to ensure their change is detected by git.
- `cd` back to language repo root, then `test-proxy push -a path/to/assets.json`. The touched file will cause the proxy to push a new tag and re-update the `assets.json`.

## It looks like trying to check out a recording fails with 500 and some weird git error

In this case, the issue is likely due to an unhandled edge case when resolving a `git checkout`. While the proxy has handled a bunch of these error cases explicitly, sometimes an error slips through.

- Zip up your `.assets` folder at the root of your repo
- Share the folder with `scbedd` over teams along with any details on the proxy-side error that you've found in your logs.
- Delete your existing `.assets`
- Re-run your tests. The proxy will download a fresh copy of the tag from your `assets.json` and re-init the `.assets` folder automatically.

### I've been successfully using the test-proxy for a while, but I'm suddenly getting SSL cert validation errors and my tests won't start

The test-proxy enables SSL by meeting in the middle with each language's test framework. The test-framework adds the test-proxy dev cert to its trusted cert bundle, and then the proxy is _started_ using that same certificate to secure its HTTPS port. The combination of running the proxy using the dev cert and trusting that same dev cert allows any localhost to communicate over HTTPS with the test-proxy.

While it is very possible that local users may encounter errors due to misconfiguration of SSL variables or other minutae, users must ensure that the dev-cert being utilized is valid for their current date!

To ensure that the appropriate dev-cert is being used:

- Check validity of current certificate. The dev cert will always be located at `eng/common/testproxy/dotnet-devcert.pfx` in each language repo.
  - On `windows`: `certutil -dump eng/common/testproxy/dotnet-devcert.pfx`. Enter `password` for the password when asked.
  - On `linux/mac`, you must have `openssl` installed, then: `openssl pkcs12 -in eng/common/testproxy/dotnet-devcert.pfx -nokeys -clcerts -passin pass:password | openssl x509 -noout -enddate`
  - If the current date DOES NOT fall within the date range of the cert, **expiration is the problem**.
  - Otherwise the issue causing your SSL verification errors is most likely system configuration related, and not specifically related to the utilized dev-cert.

If the current date is outside of the valid date range of the certificate, then you need to do the following:

- Pull latest `main` of your repo.
- Check the validity of the certificate as you did in previous step. If not up to date, you do not have latest main.
- Shut down **any** running proxy instances. They will be using the previous SSL certificate, which is invalid.
  - `windows` check task manager
  - `linux/mac` use `ps aux | grep TestProxy`. Kill any processes that match.
- Delete any local copies of the certificate.
- Follow any specific `import` directions in [trusting-cert-per-language](trusting-cert-per-language.md) for your current `language`.
