This document contains a list of known breaking changes in the engineering system tools (mainly `eng/common` directories) and available workarounds. Typically this takes the form of infrastructure changes that are not backwards compatible with older versions of the engineering system configs/scripts/tools, mainly due to dependency changes in our test agent images.

This document contains a list of errors that may be encountered when using older versions of files in the `eng/common` directory of the SDK repositories. These errors are mainly the result of changes in dependencies that the engsys team does not control and had to introduce fixes for (typically tooling updates on our pipeline VM images). If you encounter one of these errors on an older branch (such as a hotfix branch being updated for release), you must update your `eng/commmon` directory to its latest state, or find the relevant error on this page and cherry pick the commits which contain the fix for it.

First, try the following to pull `eng/common` forwards to latest:

```
cd <root of sdk git repository>
# Unstage any local changes in order to commit only eng/common updates
git restore --staged .

# If your upstream remote is not named origin, change it on all lines below
git fetch origin main
git checkout origin/main eng/common
git commit -m "Update eng/common directory to $(git rev-parse origin/main)"

# git push your changes when ready
```

If updating the entire `eng/common` directory does not work for compatibility reasons, you can try pulling in individual fixes listed below, based on the errors you are seeing.

Errors
======

Click the triangle to expand each error section.

<details>

<summary>
Docs.MS Release failing to run codeowners with 'You must install or update .NET to run this application.'
</summary>

## Docs.MS Release failing to run codeowners

**Error Message**

> You must install or update .NET to run this application. System.Management.Automation.RemoteException App: /tmp/codeowners-tool-path/retrieve-codeowners Architecture: x64 Framework: 'Microsoft.NETCore.App', version '5.0.0' (x64) .NET location: /usr/share/dotnet System.Management.Automation.RemoteException The following frameworks were found:   6.0.15 at [/usr/share/dotnet/shared/Microsoft.NETCore.App]   7.0.4 at [/usr/share/dotnet/shared/Microsoft.NETCore.App] System.Management.Automation.RemoteException Learn about framework resolution: https://aka.ms/dotnet/app-launch-failed System.Management.Automation.RemoteException To install missing framework, download: https://aka.ms/dotnet-core-applaunch?framework=Microsoft.NETCore.App&framework_version=5.0.0&arch=x64&rid=ubuntu.20.04-x64

Around 4/12/2023, dotnet 5 was removed from the pipeline agent images. The release scripts need to be updated to pull in the latest tooling built for dotnet 6.

### Workaround

Update made and synced to all repositories from https://github.com/Azure/azure-sdk-tools/pull/5608

To fix this issue, update your branch with the latest changes to `eng/common` in main, or cherry pick the following commit:

```
# azure-sdk-for-java
git cherry-pick 2a6151e8ce224cebaa8a66cabe3e5c333ee6a3de

# azure-sdk-for-js
git cherry-pick 07ac262e40bca08bf7c37ea232d2f0d071b10d52

# azure-sdk-for-net
git cherry-pick 575f7d6fdd44939587b6fbc65bdd1df3405208b6

# azure-sdk-for-python
git cherry-pick 9a48863fd05d4853ef29697fc1ae9dafcab789a5

# azure-sdk-for-go
git cherry-pick d0dc9e9a3622f649335474176cce7e7e40e5a138

# azure-sdk-for-android
git cherry-pick 4be23c2be728442a1f896f5a1df1897fb88db56d

# azure-sdk-for-ios
git cherry-pick cd7257ba053377879c7b918b9e072c4ae969ac7e

# azure-sdk-for-c
git cherry-pick 15a799358184578fa5ba868bcff30f6133ea21af

# azure-sdk-for-cpp
git cherry-pick 699957280939d4ba7d8c6e0606f0b71cd59f2970

# azure-sdk-tools
git cherry-pick 9d27383bfaf56d28e94f71537eb04d4f1ec38e3f
```

</details>


<details>

<summary>
Credscan - You must install or update .NET to run this application.
</summary>

## Credscan - You must install or update .NET to run this application.

This error comes from the "CredScan running" step in the Compliance stage:

```
##[error]You must install or update .NET to run this application.
```

On 4/5/2023, dotnet 3.1 was removed from the 1es managed images used for Azure Pipelines runs. Older versions of credscan required dotnet 3.1.

### Workaround

Fix made and synced to all repositories from https://github.com/Azure/azure-sdk-tools/pull/5918

To fix this issue, update your branch with the latest changes to `eng/common` in main, or cherry pick the following commit:

```
# azure-sdk-for-java
git cherry-pick 2f9cdd9319f1867707b5872f12d97d5cdbbb077a

# azure-sdk-for-js
git cherry-pick 35e20cecff34765bbb102377a9db6328d158104e

# azure-sdk-for-net
git cherry-pick 9f5d8f81b25cbec8a3b7d71dea71d7875843edd5

# azure-sdk-for-python
git cherry-pick 8506be4e8db37b197c77a557fb0a2b342c1074dc

# azure-sdk-for-go
git cherry-pick e9c77a5f47d5858f0b84d5965c9fa0db22548c6a

# azure-sdk-for-android
git cherry-pick eaa0814173137f897a1c28835a3cbe29705e221d

# azure-sdk-for-ios
git cherry-pick 0f0eab74bf3e30c5c591122ae6227274df376eef

# azure-sdk-for-c
git cherry-pick 20cb4f73ef885838938d45e142867151d3136356

# azure-sdk-for-cpp
git cherry-pick d55e2906077b6edcbb42d51ad2becd0492f1ffcf

# azure-sdk-tools
git cherry-pick 9d27383bfaf56d28e94f71537eb04d4f1ec38e3f
```

</details>

<details>

<summary>
fatal: specify directories rather than patterns
</summary>

## fatal: specify directories rather than patterns

Error messages like:
- `fatal: specify directories rather than patterns`
- `##[error]ENOENT: no such file or directory, stat '/mnt/vss/_work/1/s/eng/common/scripts/job-matrix/Create-JobMatrix.ps1`

This error may occur in pipelines that call `git sparse-checkout` without the `--no-cone` flag set. After git v2.37.0 was released the default behavior of sparse checkout was changed to `cone` mode (see [docs](https://github.blog/2022-06-27-highlights-from-git-2-37/#tidbits)). Git was auto-updated on our pipeline agent VM images, so any pipelines run with engsys source code from before 7/11/2022 (e.g. old release branches) may fail with this error.

### Workaround

The core fix that was synced to all repositories is located here: https://github.com/Azure/azure-sdk-tools/pull/3606

To fix this issue, cherry pick the commit that updates the sparse checkout command to use `--no-cone` mode.

```
# azure-sdk-for-java
git cherry-pick 853649891196aa9ac3080d69081901341cce4332

# azure-sdk-for-js
git cherry-pick f51e095156c0a6d45969b118f3c0367777742a8f

# azure-sdk-for-net
git cherry-pick ae4171f5d65223082406776be23482d4353629bd

# azure-sdk-for-python
git cherry-pick 7b206165e15e384c414aad4ade46df5716e0553b

# azure-sdk-for-go
git cherry-pick 9401c5ea4b6be25a7f7b2ef2ded53c097bca903b

# azure-sdk-for-android
git cherry-pick c800fc5bf1e84442eb5e6222a0c80b679d7d8241

# azure-sdk-for-ios
git cherry-pick 2908f149c075736de45e8eb6379381ebfbac334c

# azure-sdk-for-c
git cherry-pick 42c94ef95a363baf8d1d27cb84cae8e100dda2d5

# azure-sdk-for-cpp
git cherry-pick 07a56bc5e3e728fbbcf37a84bb9d37fec1ee7efc

# azure-sdk-tools
git cherry-pick ae4171f5d65223082406776be23482d4353629bd
```

</details>
