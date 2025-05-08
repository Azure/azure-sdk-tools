# `Test-Proxy` Frequently Asked Questions

## My `push` push failed with a `403` error

This is likely due to either:

- The dev not having `write` permission to `Azure/azure-sdk-assets`
- No cached microsoft `git` credentials in the shell the dev runs `test-proxy push` from.
- Being in `codespaces` where the dev's git user isn't available in the default session.

### How to check the `git` user is properly logged in

On the same shell that the dev will `test-proxy push` from:

```bash
git clone -c core.longpaths=true --no-checkout --filter=tree:0 https://github.com/Azure/azure-sdk-assets.git
git checkout -b a-test-branch
git push -u origin a-test-branch
```

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
  - The most likely case here is that the update to the tag in `assets.json` WAS updated, but the user accidentally discarded via `git checkout`

### The user didn't record new tests

todo: steps to verify changed files in `assets` after re-record

### A successful push but accidentally discarded `assets.json`

todo: steps to add a change manually to enable the `push`.

## It looks like trying to check out a recording fails with 500 and some weird git error

In this case, the issue is likely due to an unhandled edge case when resolving a `git checkout`. While the proxy has handled a bunch of these error cases explicitly, sometimes an error slips through.

- Zip up your `.assets` folder at the root of your repo
- Share the folder with `scbedd` over teams along with any details on the proxy-side error that you've found in your logs.
- Delete your existing `.assets`
- Re-run your tests. The proxy will download a fresh copy of the tag from your `assets.json` re-init the `.assets` folder automatically.