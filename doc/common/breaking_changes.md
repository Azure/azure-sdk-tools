This document contains a list of known breaking changes in the engineering system tools (mainly `eng/common` directories) and available workarounds. Typically this takes the form of infrastructure changes that are not backwards compatible with older versions of the engineering system configs/scripts/tools, mainly due to dependency changes in our test agent images.

Table of Contents
=================

* [Credscan - You must install or update .NET to run this application.](#credscan---you-must-install-or-update-net-to-run-this-application)
    * [Workaround](#workaround)
* [fatal: specify directories rather than patterns](#fatal-specify-directories-rather-than-patterns)
    * [Workaround](#workaround-1)


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
