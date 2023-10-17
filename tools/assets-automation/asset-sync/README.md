# Azure SDK Assets Relocation -- "move recordings out of repos"

- [Azure SDK Assets Relocation -- "move recordings out of repos"](#azure-sdk-assets-relocation----move-recordings-out-of-repos)
  - [What is the problem? Why?](#what-is-the-problem-why)
  - [How the test-proxy can ease transition of external recordings](#how-the-test-proxy-can-ease-transition-of-external-recordings)
    - [Old](#old)
    - [New](#new)
  - [Which external storage mediums were considered?](#which-external-storage-mediums-were-considered)
    - [`Git SubModules`](#git-submodules)
      - [Advantages of `Git SubModules`](#advantages-of-git-submodules)
      - [Disadvantages of `Git SubModules`](#disadvantages-of-git-submodules)
    - [`Git Subtrees`](#git-subtrees)
    - [`Git lfs`](#git-lfs)
      - [Advantages of `Git lfs`](#advantages-of-git-lfs)
      - [Disadvantages of `Git lfs`](#disadvantages-of-git-lfs)
    - [`External Git Repo`](#external-git-repo)
      - [Advantages of `Git Repo`](#advantages-of-git-repo)
      - [Disadvantages of `Git Repo`](#disadvantages-of-git-repo)
      - [But if we already HAVE a problem with ever expanding git repos, why does an external repo help us?](#but-if-we-already-have-a-problem-with-ever-expanding-git-repos-why-does-an-external-repo-help-us)
    - [Blob Storage](#blob-storage)
      - [Advantages of `Blob Storage`](#advantages-of-blob-storage)
      - [Disadvantages of `Blob Storage`](#disadvantages-of-blob-storage)
    - [`Pulling a zipfile of the repository`](#pulling-a-zipfile-of-the-repository)
      - [Overall evaluation of `Pulling a zipfile of the repository`](#overall-evaluation-of-pulling-a-zipfile-of-the-repository)
    - [Conclusion - External Git Repo](#conclusion---external-git-repo)
  - [Exploring An External Git Repo](#exploring-an-external-git-repo)
    - [assets.json discovery](#assetsjson-discovery)
      - [When establishing recordings for a new service](#when-establishing-recordings-for-a-new-service)
    - [Repo Organization](#repo-organization)
    - [Auto-commits and merges to `main`](#auto-commits-and-merges-to-main)
    - [Drawbacks](#drawbacks)
  - [Scenario Walkthroughs](#scenario-walkthroughs)
    - [Single Dev: Create a new services's recordings](#single-dev-create-a-new-servicess-recordings)
    - [Single Dev: Update single service's recordings](#single-dev-update-single-services-recordings)
    - [Single Dev: Update recordings for a hotfix release](#single-dev-update-recordings-for-a-hotfix-release)
    - [Multiple Devs: Update the same pull request](#multiple-devs-update-the-same-pull-request)
    - [Multiple Devs: Update the same recordings, same service/package multiple different PRs](#multiple-devs-update-the-same-recordings-same-servicepackage-multiple-different-prs)
    - [Multiple Devs: Update recordings for two packages under same service in parallel](#multiple-devs-update-recordings-for-two-packages-under-same-service-in-parallel)
  - [Asset sync script implementation](#asset-sync-script-implementation)
    - [Cross-platform capabilities](#cross-platform-capabilities)
    - [Implementation of Sync Script](#implementation-of-sync-script)
    - [Sync operations description and listing](#sync-operations-description-and-listing)
    - [Sync Operation triggers](#sync-operation-triggers)
    - [Sync Operation details - pull](#sync-operation-details---pull)
    - [Sync Operation details - push](#sync-operation-details---push)
    - [Integrating the sync script w/ language frameworks](#integrating-the-sync-script-w-language-frameworks)
    - [Test Run](#test-run)
    - [A note regarding cross-plat usage](#a-note-regarding-cross-plat-usage)
  - [Integration Checklist](#integration-checklist)
  - [Post-Asset-Move space optimizations](#post-asset-move-space-optimizations)
    - [Test-Proxy creates seeded body content at playback time](#test-proxy-creates-seeded-body-content-at-playback-time)
  - [Follow-Ups](#follow-ups)

## What is the problem? Why?

The Azure SDK team has a problem that has been been growing in the background for the past few years. Our repos are getting big! The biggest contributor to this issue are **recordings**. Yes, they compress well, but when bugfixes can result in entire rewrites of multiple recordings files, the compression ratio becomes immaterial.

We need to get these recordings _out_ of the main repos without impacting the day-to-day dev experience significantly.

```text
sdk-for-python/                            sdk-for-python-assets/         
  sdk/                                       sdk/
    tables/                                    tables/ 
      azure-data-tables/                         azure-data-tables/   
        tests/                                     tests/
          |-<recordings>--------|                    |--<moved recordings>-----|
          |   <delete>          |    relocate        |   recording_1           |
          |   <delete>          |      -->           |   recording_2           |
          |   <delete>          |                    |   recording_N           |
          |---------------------|                    |-------------------------|
    
```

The unfortunate fact of the matter is that an update like this _will_ impede users. The only thing we can do is mitigate the worst of these impacts.

Thankfully, the integration of the test-proxy actually provides a great opportunity for upheavel in the test areas! Not only would we be making big changes in the test area already, but the `storage-location` feature of the test-proxy really lends itself well to this effort as well!

## How the test-proxy can ease transition of external recordings

With language-specific record/playback solutions, there must be an abstraction layer that retrieves the recording files from an expected `recordings` folder. Where these default locations are is usually predicated on the tech being used. It's not super terrible, but custom save/load would need to be implemented for each recording stack, with varying degrees of complexity depending on how opinionated a given framework is.

Contrast this with the the test-proxy, which starts with a **storage context**. This context is then used when **saving** and **loading** a recording file.

Users of the test proxy are required to provide a `relative path to test file from the root of the repo` as their recording file name. A great example of this would be...

```text
sdk/tables/azure-data-tables/recordings/test_retry.pyTestStorageRetrytest_no_retry.json
[----------------test path--------------------------][--------------test name----------]
```

[What this looks like in source](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_no_retry.json)

Given that the test-proxy is storing/retrieving data independent of the client code (other than the key), client side changes for a wholesale shift of the recordings location is actually simple. The source code for the test _won't need to change at all_. From the perspective of the client test code, nothing has functionally changed.

All that needs to happen is to:

0. Test framework calls sync script, ensures recordings are present in a cloned assets repo locally.
1. Start the test proxy with storage context set to cloned assets repo (details forthcoming)
2. Adjust the provided "file path" to the recording within the asset repo if necesssary.
3. Profit.

If you were invoking the test-proxy as a docker image, the difference in initialization is as easy as:

### Old

`docker run -v C:/repo/sdk-for-python/:/etc/testproxy azsdkengsys.azurecr.io/engsys/testproxy-lin:latest`

### New

`docker run -v C:/repo/sdk-for-python-assets/:/etc/testproxy azsdkengsys.azurecr.io/engsys/testproxy-lin:latest`

Given the same relative path in the assets repo, zero changes to test code are necessary.

## Which external storage mediums were considered?

Prior to ScottB starting on this project, JimS was the one leading the charge. As part of that work, [Jim explored a few potentional storage solutions](https://microsoft.sharepoint.com/:w:/t/AzureDeveloperExperience/EZ8CA-UTsENIoORsOxekfG8BzwoNV4xhVOIzTGmdk8j4rA?e=RKAkkc). He did not evaluate these strictly from a `usability` standpoint.

- **external git repo**
- git modules
- git lfs
- blob **storage**
- pulling a zipfile of the repository

He also checked other measures, like `download speed` and `integration requirements`. Blob storage especially has a good story for "public read, private write", and _does_ support snapshotting. However, the cost is low bulk download speed when NOT being run on a tool like `azcopy`.

[These and other observations are recorded in his original document available here.](https://microsoft.sharepoint.com/:w:/t/AzureDeveloperExperience/EZ8CA-UTsENIoORsOxekfG8BzwoNV4xhVOIzTGmdk8j4rA?e=DFkiII)

### `Git SubModules`

#### Advantages of `Git SubModules`

- Publically browsable in a coherent fashion through the github UI

#### Disadvantages of `Git SubModules`

- Submodules can’t do part of a repository. The only way one could effectively trim a submodule would be to clone with depth which would leave things in a detached head state. The goal here is to minimize clone and sync times without negating the ability, or requiring the entire enlistment, to do updates.
- Extremely unwieldy. The method for updating them locally is quite manual, and there is as far as I can tell no way to directly tie a submodule's commits to the main repos commits. They're still totally separate repositories. [As reviewable in their docs](https://git-scm.com/book/en/v2/Git-Tools-Submodules), git submodules aren't quite intended for large collaboration or projects. Especially not with multiple moving parts under the submodule. Not only this, but with submodules in place,

### `Git Subtrees`

TODO: Read about this option.

### `Git lfs`

#### Advantages of `Git lfs`

- We remain in the same repository. `git lfs` just defers the download of specific sets of files.

#### Disadvantages of `Git lfs`

- There’s no transport level compression - Obviously sending uncompressed files over the wire isn’t going to help checkout times.
- The physical drive the enlistment is on seems to matter more when using LFS -  For example: two machines, both physically in Redmond  and on corpnet, one was a single spinning drive and the other was an array, the clone time difference was negligible (30 vs 22 seconds) but the checkout times were wildly variant (18 seconds vs 2 minutes and 58 seconds)
- Distance matters – Fetching from Redmond produced very different numbers than fetching from Australia. On average clone times were double of what we’d see on corpnet and still around a minute, but the checkout times were horrendous. Non-LFS checkout had an average of 32 seconds where the LFS checkouts were taking an average of 8 minutes. Note: This was on an SSD, not a spinning disk.
- Git LFS files are pulled individually - There’s no way to bulk pull everything. The files are pulled over https with a default concurrency of 3 and while this is something that could be tweaked in the lfs config it would potentially help checkout times on an SSD but make the times on a spinning disk worse.
- The size on disk size, for us, would initially get worse and then eventually taper off – Because we wouldn’t be rewriting history we’re still going to have space in .git/objects from the previous versions of these files but with the way LFS works, you’re also going to have .git/lfs/objects. Unlike .git/objects, the .git/lfs/objects will only contain the version that’s been pulled local. If you checkout a version you don’t have, it’ll update the .git/lfs/object with that and the other version will simply be a file with pointer. In the case of the .NET repo, which has 6029 recording json files taking up about 671MB, this means that the size on disk would grow by that amount. The reason for this bloat is that there’s no compression on the LFS folder.

### `External Git Repo`

Current prototype visible [here.](./assets.ps1).

#### Advantages of `Git Repo`

- Publically browsable in a coherent fashion through the github UI
- History
- Geo-replicated throughout the world
- The level of customization necessary is purely on the language shims for automatic playback purposes

#### Disadvantages of `Git Repo`

- Need to deal with the fact that it's a git repo, and not a direct storage solution. We will need PRs, branches, and cleanup tasks running on the assets repo instead of a storage solution with no real sense of commit history.
- Our local tooling will need to do a bunch of heavy lifting to keep the process simple for users. Even with that, we are going to be seeing a bunch of finicky issues.
- What do we do for the `-pr` repositories? We will _have_ to maintain a copy of the assets repo for those as well. What would this look like to migrate a PR from `-pr` to public?

#### But if we already HAVE a problem with ever expanding git repos, why does an external repo help us?

Because the automation interacting with this repository should only ever clone down _a single commit_ at one time.

Yes, commit histories do add _some_ weight to the git database, but it's definitely not a super impactful difference.

### Blob Storage

`scbedd` [did a quick proto of what this _could_ look like.](https://github.com/semick-dev/shipwreck) We'd probably swap to a thin `go` client instead of this python package, but the concept definitely works.

#### Advantages of `Blob Storage`

- Size on disk only consists of the files that were pulled. There is no .git folder becoming more and more bloated as versions are added.
- Pulling files piecemeal for an area or areas is relatively easy. The main benefit is how easy it would be to pull resources for a given area, like batch or keyvault. Most areas took under 5 seconds to pull but there were outliers. For example, Storage was the worst offender taking about 7 seconds to pull on corpnet, 15 seconds from Seattle and 50 seconds from Australia.
- Extremely easy to avoid conflicts
- Accessible through basic REST API.
- Much fewer moving parts. It's possible to get into a conflict with the git repo, but not when individually uploading blobs.
- Given rest API nature of this, possibly implement thin client in `go` to generate platform specific runners.

#### Disadvantages of `Blob Storage`

- Distance matters – Pulling everything from a machine on corpnet was relatively cheap but pulling the same files from Australia was 2-3x longer pulling from Redmond.
- Permissions would be a problem - Git on one side, corpnet on the other. For internal developers this is less problematic but external contributors I have no idea how’d we get around this.
- A single azcopy call isn’t terrible, even if you’re pulling an entire Assets repository (in the US), however the more azcopy calls, the longer it would take to sync. For example, pulling all the assets on my home connection took 50 seconds but if I pulled them an sdk/<area> at a time it took 4 minutes and 13 seconds.
- Extensive local scripting necessary to handle upload/download.
  - We would need to locally zip and upload. Individual file download is _far_ too enefficient, but a single snapshotted blob could definitely work.
  - Virtually the same conflicts for download and unzip
- No publically available UI to browse recordings at rest and recorded
- A native level unzip/zip operation is extremely heavy for larger recordings folders
  - An example of this is the storage service, where we have upwards of half a gigabyte of data present. This is an enormous tax to zip and unzip with each push/pull.
  - This is somewhat mitigated when swapping to per-package `assets.json` (which we would do with this approach)
- The storage is not geolocated normally, though geo replication be set for as many as _all_ regions supported by azure.
- There is no concept of "history". It's all point in time.
- Cost _may_ be a problem, git is free as far as we are concerned.

### `Pulling a zipfile of the repository`

#### Overall evaluation of `Pulling a zipfile of the repository`

While it is possible to download a zipfile of a GIT repository it’s not very practical. Downloading the zip is only slightly faster than just syncing the repository but the unpacking of the zip that makes this a non-starter. For example: On my Surface Laptop 2, taking zipfile of azure-sdk-for-net and using powershell’s Expand-Archive took 8:07 to unpack. Using System.IO.Compression.ZipFile’s ExtractToDirectory took 5:06 to unpack. The decompression times alone were enough to end this investigation but even if that could be rectified the result would effectively be a read-only copy of the repository.

### Conclusion - External Git Repo

[After a discussion with the Engineering System team, the external git repo option was selected.](https://microsoft-my.sharepoint.com/:v:/p/scbedd/EXiLEz0k7H5Pqr4rWNkjb7cBcVRxJBOX8iPOLZatZnOEFQ?e=ZAkJbb&isSPOFile=1). We will finish a prototype in powershell and revisit the technology choice if necessary.

## Exploring An External Git Repo

This is where the story gets complicated. Now that recordings are no longer stored directly alongside the code that they support, the process to _initially retrieve_ and _update_ the recordings gets a bit more stilted.

In a previous section, we established that another git repo is the most obvious solution.

It makes sense to start with a single, common assets repo.

- [Azure/azure-sdk-assets](https://github.com/Azure/azure-sdk-assets)

However, if that pans out to be extremely messy, we have also established these `assets` repos for the four main languages.

- [Azure/azure-sdk-for-python-assets](https://github.com/Azure/azure-sdk-for-python-assets)
- [Azure/azure-sdk-for-js-assets](https://github.com/Azure/azure-sdk-for-js-assets)
- [Azure/azure-sdk-for-net-assets](https://github.com/Azure/azure-sdk-for-net-assets)
- [Azure/azure-sdk-for-java-assets](https://github.com/Azure/azure-sdk-for-java-assets)

So for a given language, we will have the following resources at play:

```bash
Azure/azure-sdk-for-<language>
Azure/azure-sdk-assets

# if necessary
Azure/azure-sdk-for-<language>-assets
```

Given the split nature of the above, We need to talk about how the test-proxy knows **which** recordings to grab. We can't simply default to `latest main`, as that will _not_ work if we need to run tests from an earlier released version of the SDK.

To get around this, we will embed a reference to an assets repo SHA into the language repository. As part of the test playback, local implementations must _retrieve_ these referenced assets at runtime. After retrieving, there will also need to be a process to sync any updated recordings _back_ to the recordings repo without an extremely large amount of manual effort.

As of now, it seems the best place to locate this assets SHA is in a new file in each `sdk/<service>` directory. For most of our packages this is a safe bet. Only one team member will be updating this SHA at a time, and as such it will be easy to add onto a commit one at a time. There is no parallelization! For others, like `azure-communication` in python or `spring` in Java land, this will be complex. We will revisit these topics in the [Scenarios Walkthroughs](#scenario-walkthroughs) section.

```text
<repo-root>
  /sdk
    /<service>
      assets.json
```

And within a sample assets.json file...

```jsonc
{
    // Within the assets repo, should we prefix anything before the recording path being written?
    "AssetsRepoPrefixPath": "recordings/",

    // We are not intending on making a azure-sdk-assets-pr repository.
    "AssetsRepo": "Azure/azure-sdk-assets",

    // We will design this to support multiple identical assets repos at the same time. Will be mostly unused.
    "AssetsRepoId": "",

    // By default, will fall back to main. this will ONLY be used if there is no auto/<servicename> branch in the repo.
    "AssetsRepoFallbackBranch": "main",

    // By default, will be resolved auto/<service>.
    "AssetsRepoBranch": "auto/tables",

    // this json file will eventually need additional metadata, the below key just illustrates that we can put whatever we want here.
    "metadatakey1": "metadatavalue1",

    // no default for this value.
    "SHA": "4e8e976b7839c1e9c6903f48106e48be76868a5d"
}
```

While this works really well for local playback, it does not work for submitting a PR with your code changes. Why? Because the PR checks won't _have_ your updated assets repo that you may have created by recording your tests locally!

This necessitates some local action that can be run **against a local branch or PR** that will push a commit to the `assets` repo and then update the **local reference** within a `assets.json` to consume it.

You will note that the above JSON configuration lends itself well to more individual solutions, while allowing space for more _targeted_ overrides later.

### assets.json discovery

Any interactors with the asset script must be able to parse a `assets.json` given only a `current working directory`. The `assets.ps1` implementation as it exists now allows a user to pass a target directory as well. Whatever the chosen method, given a _start_ directory, the algo should traverse _up_ the file tree until it discovers a file named exactly `assets.json` OR hits `root`.

`root` can be either a folder with `.git` present within it OR the actual current-disk root `/`.

#### When establishing recordings for a new service

One must _create_ a assets.json. The recording framework should base a _new_ assets.json off of one further up the directory tree. Everything is safe to use _other than_ the `AssetsRepoBranch` property.

### Repo Organization

```text
<azure-sdk-for-language-root>/
    .git
    .assets/

+------> azure2/
|
|  +-------> recordings/
|  |
|  |             <TestPath>
|  |
|  | assets.json
|  +---AssetsRepoPrefixPath:"recordings"
|      AssetsRepo:"azure/azure-sdk-for-python"
+------AssetsRepoId:"azure2"
       SHA:"ABC"
```

- All assets repositories will be cloned to a folder under the `.assets` folder at root.
- If `AssetsRepoId` is provided, that will be the name of the folder under `.assets`.
  - If not provided, the folder under `.assets` will be named `AssetRepo.Replace("/", ".)`
  - AssetRepoId is used in weird edge cases, and we're keeping it around for future proofing only at this time
- `<TestPath>` is the _current_ path to recording that the language-repos will provide to the test-proxy in the `Test-File` argument.

### Auto-commits and merges to `main`

We need to add nightly automation that squashes down these auto-commits into the `main` branch on a nightly cadence.

Let's walk through a scenario to show what this would look like.

When PRs are submitted, the SHAs referencing the assets repo will be _different_. This is due to the fact that each user will have pushed their new recordings to the assets repo separately form each other. They have no comprehension of "update from main". All individual SHAs remember!

```text
+---------------------------------------------------+---------------------------------------------------------------+
| azure-sdk-assets                                  | assets-repo/                                                  |
|                                                   |                                                               |
|                                                   |                                                               |
|   sdk/core/assets.json----------------------------+>auto-commit/core@SHA1                                         |
|      ...         |                                |   /recordings/sdk/core/azure-core/recordings/YYY.json         |
|      SHA: "SHA1" |                                |                                                               |
|      ...                               +----------+>auto-commit/storage@SHA2                                      |
|                                        |          |   /recordings/sdk/core/azure-storage-blob/recordings/XXX.json |
|   sdk/storage/assets.json              |          |                                                               |
|      ...         +---------------------+          | hotfix-commit/storage@SHA3                                    |
|      SHA: "SHA2" |                                | ^ /recordings/sdk/core/azure-storage-blob/recordings/YYY.json |
|      ...                                          | |                                                             |
|                                                   | |                                                             |
|   sdk/storage/assets.json (from release tag)      | |                                                             |
|      ...                                          | |                                                             |
|      SHA: "SHA3" ---------------------------------+-+                                                             |
|      ...                                          |                                                               |
|                                                   |                                                               |
+---------------------------------------------------+---------------------------------------------------------------+
```

After nightly automation has copied commits into `main`, we will update the current assets.json files in `main` to reflect the newly merged _common_ commit.

- auto-commit/core@SHA1 -> commits merge-commit to main
- auto-commit/core@SHA2 -> commits merge-commit to main
- hotfix-commit/storage@SHA3 -> Stays around forever, like our hotfix branches do.

```text
+---------------------------------------------------+---------------------------------------------------------------+
| azure-sdk-assets                                  | assets-repo/                                                  |
|                                                   |                                                               |
|                                                   |                                                               |
|   sdk/core/record      +---------------------------+>auto-commit/core@NewMainSHA                                  |
|      ...               |                          |   /recordings/sdk/core/azure-core/recordings/YYY.json         |
|      SHA: "NewMainSHA" |                          |                                                               |
|      ...                               +----------+>auto-commit/storage@NewMainSHA                                |
|                                        |          |   /recordings/sdk/core/azure-storage-blob/recordings/XXX.json |
|   sdk/storage/assets.json              |          |                                                               |
|      ...               +---------------+          | hotfix-commit/storage@SHA3                                    |
|      SHA: "NewMainSHA" |                          | ^ /recordings/sdk/core/azure-storage-blob/recordings/YYY.json |
|      ...                                          | |                                                             |
|                                                   | |                                                             |
|   sdk/storage/assets.json (from release tag)      | |                                                             |
|      ...                                          | |                                                             |
|      SHA: "SHA3" ---------------------------------+-+                                                             |
|      ...                                          |                                                               |
|                                                   |                                                               |
+---------------------------------------------------+---------------------------------------------------------------+
```

In this way, a concept of `main` can still exist, and will be used where possible, but daily progress will not be held up.

### Drawbacks

The `auto` commit branches will need to stick around. At least as far as we need to keep the commits in them that are referenced from the `azure-sdk-for-python` repo. Any commits present in the `azure-sdk-assets` and referenced anywhere from the `azure-sdk-for-<language>` repo MUST continue to exist. They cannot be automatically trimmed. For example, there is nothing stopping a dev from releasing a package when the assets repo SHA is set to the intial "auto" commit. We'd prefer that folks wait for the merge to `main` to release the package, but to enforce that will cause a whole acre of headache.

One alternative to hard SHAs is to create tags that point to these SHAs. This will allow us to rewrite a tag when combining recordings resulting in a different SHAs.

## Scenario Walkthroughs

For all of these, any supplementary functionality is laid out in `asset.ps1`. Just `.` include it in your local powershell environment for the commands to work.

### Single Dev: Create a new services's recordings

This is the easiest case, there is no existing place to start.

```text
  <user> Invoke Record Tests - logic in the assets.json discover needs to _create_ a service-level assets.json.
  <tooling-assets.ps1> Initialize-Assets-Repo - Function from psm1. Clone down with 0 blobs, init in a known location.
  <tooling-assets.ps1> Check for SHA/auto branch in the repo - The "Sync" operation
    <tooling-prompt> If changes are being abandoned in a different directory, prompt user before discarding changes.
  <tooling-assets.ps1> If no remote references, create branch under format of auto/<servicename> push empty branch.
  <tooling-assets.ps1> After above invocation, assets.json values of the following fields will be updated as per main, since  there is no existing auto/<service> branch.
            AssetsRepo
            AssetsRepoPrefixPath
            Branch
            TargetSHA
  <tooling-assets.ps1> Return the root of the initialized repo to the framework  -- for example <path-to-your-language>/.assets/<azpythonassets>/recordings/<storage path>
  <tooling-assets.ps1> Start Test-Proxy in context of returned root
  <user> git status -- see a new assets.json in the area's root. sdk/<servicearea>
  <user> runs tests -- recording mode
  <user> runs assets.ps1 push -- implementation detail
    <tooling> updates assets.json with new SHA from a newly created assets repo auto/<servicearea> branch.
  <user commits their other changes, including assets.json updates, submits for PR>
```

### Single Dev: Update single service's recordings

This situation is the "normal" use case. Merely adding an additional commit to their auto/<service> branch.

```text
  <user> runs tests -- recording mode
  <tooling-assets.ps1> Check for SHA/auto branch in the repo - The "Sync" operation via existing assets.json
    <tooling-prompt> If changes are being abandoned in a different directory, prompt user before discarding changes.
  <tooling-assets.ps1> Return the root of the initialized repo to the framework  -- for example <path-to-your-language>/.assets/<azpythonassets>/recordings/<storage path>
  <tooling-assets.ps1> Start Test-Proxy in context of returned root
  <tooling-assets.ps1> Invoke push. A "recording" push can only work against the latest commit of the auto/<service> branch.
    <tooling-assets.ps1> Need to "stash" new recordings, pull "latest" commit from auto/<service> branch, unstash new changes to to the cloned commit.
  <user> runs assets.ps1 push -- implementation detail
    <tooling> updates assets.json with new SHA from a newly created assets repo auto/<servicearea> branch.
  <user commits their other changes, including assets.json updates, submits for PR>
```

### Single Dev: Update recordings for a hotfix release

Given above scenario plays out how we expect, this is identical to a normal service update, just on a branch in the language repo.

### Multiple Devs: Update the same pull request

Given above scenario plays out how we expect, this is identical to a normal service update, just on a branch in the language repo.

### Multiple Devs: Update the same recordings, same service/package multiple different PRs

First one in wins. The way we have the above scenario laid out.

When the second person attempts to merge, they'll hit conflicts on `assets.json`. They will need to update, then re-record their tests with the latest from the PR branch, to ensure they have the changes THEY added as well as the changes that were checked in by their co-dev.

### Multiple Devs: Update recordings for two packages under same service in parallel

Same above.

## Asset sync script implementation

Alright, so we know how we want to structure the `assets.json`, and we know WHAT needs to happen. Now we need to delve into the HOW this needs to happen. Colloqially, anything referred to as a `sync` operation should be understood to be part of these abstraction scripts handling git pull and push operations.

They key mention here is that **regardless** of what storage methodology is used, we need to describe some integration points for each language's proxy-shim.

Specifically, we need to do the following:

- Resolve the "context" of an operation given a target directory or CWD
  - Current  implementation is in [Resolve-RecordingJson](https://github.com/Azure/azure-sdk-tools/blob/240426ec98a62606bf1c9d99991e31eadd1b22f5/eng/common/asset-sync/assets.ps1#L63) 
  - The key here is we will resolve a lot of the the relative paths here.
  - This "context" will be passed around internally when referring to various operations.
  - The relative path to the target assets.json in the language repo will be used to _focus sparse checkouts_.
- Clone the assets repo if it is not yet initialized
- Figure out which auto-branch to go after
- Grab "the assets" from the storage medium given an `Target Asset Identifier` (given git storage it would be a SHA) in the assets.json, restore into the target directory
- Return the **root** of the cloned directory to the tooling for use when starting the test-proxy)

### Cross-platform capabilities

Basic interactions will be provided by a powershell script.

- Initialize local assets repo
- Checkout at SHA
- Push update to auto branch

Each language framework can of course implement the above as well, and should at the very least shell out to the assets script to begin with.

### Implementation of Sync Script

To remain as out of the way as possible, it would be rational to support two versions of these `sync` scripts. `pwsh` and `sh`. However, it is easy to see a world where we have bugs in one version or the other of the sync library.

Given that, we should probably just target `powershell`.

One of the azsdk repos may wish to integrate these `sync` scripts themselves rather than taking a direct dependency on powershell. This is totally fine, but the implementation will have to be maintained by that team. The EngSys team is signing up to deliver the generic abstraction, and will code to ensure that other abstractions are allowed.

### Sync operations description and listing

The external repo will probably be a _git_ repo, so it's not like devs won't be able to `cd` into it and resolve any conflicts themselves. However, that should not be the _norm_. To avoid this, we need the following functionality into these scripts.

| Operation | Description |
|---|---|
| Sync | When one first checks out the repo, one must initialize the recordings repo so that we can run in `playback` mode.  |
| Push | Submits a PR to the assets repo, updates local `assets.json` with new SHA. |
| Reset | Return assets repo and `assets.json` to "just-cloned" state. |
| Checkout | Abandon Any Pending Changes (prompt the user!), then Sync to the targeted SHA |

One benefit of building on top of a git repository is that we have a possible no-op checkout posible when switching the target assets repo SHA. EG: a dev is working a `storage` PR and needs to check a possible issue in `keyvault`. That dev will need to probably need to retarget their assets repo to point at the SHA defined in the `storage` assets.json sync-script. However, the assets.jsons probably don't have the same SHA!

```text
Commits in assets repository on main
(A) -> (B) -> (C) -> (D) -> (E)

sdk/storage/assets.json
  SHA: D

sdk/keyvault/assets.json
  SHA: E
```

If we regularly `merge-commit` from each `auto/<service>` branch into `main`, we will be able to keep the `auto` commit branches clean and with short commit histories. The commits will never go anywhere! They'll just be available on `main` instead of each specific branch.

A side-benefit of this is that we can run into the situation above. If we are swapping from a _newer_ merged commit to an older one, we can ascertain whether that commit is _already encapsulated_ by our current. In best-case, our scripting should recognize that `D` is supplanted by `E`, and we don't need to do any checkout actions at all!

- One major concern with this process. We are watching `checkenforcer` deal with checks that are fired from CI builds that are the result of a manual large merge. `scbedd` has concerns  with the amount of churn we will be forcing on the repo (just for the PR builds) that would result from the constant updating of these SHAs.

### Sync Operation triggers

At the outset, this will need to be manually run. How far down this manual path do we want to go?

Options:

- `Pre-commit` hook ala typescript linting
  - This works for _changes_, but how about for a fresh repo? Initialize has gotta happen at some point. Automagic stuff could also result in erraneous PRs to the  
- Scripted invocation as part of a test run. For `ts/js`, this is actually simple, as `npm <x>` are just commands defined in the packages.json file. For others, this may be a bit closer to manual process.
  - Given that the testing frameworks _already_ have emplaced a "before the tests, start test-proxy" shim that, we have a great opportunity to place our script in this same location.

### Sync Operation details - pull

The initialization of the assets repo locally should be a simple clone w/ 0 blobs.

```text
.git
<files at root>
sdk/
  <targetservice>/
   ...
```

As `playback` sessions are started, the repo should:

1. Discard pending changes, reset to empty
2. If there is no `assets.json`, create it. It should populate rational defaults for the repo. (maybe a recording json in root?)
   1. Check out assets repo `sdk/<service>` directory with the targeted SHA from `assets.json`.
   2. If there is no existing `auto/<servicename>` branch, initialize from `main`. If there _is_ an existing `auto/<servicename>`, check grab the latest commit and use as base.
3. Add `sparse-checkout` for the service folder needed by the current playback request.
   1. If changing targeted service, this means removing the previous `sdk/<service>` directory from the local git config before adding the current target
4. `checkout` exact SHA specified in `assets.json`
   1. If the SHA isn't supplanted by currently checked out SHA, leave the current SHA checked out and no-op.
5. `pull`

Given the context advantages discussed earlier, one most only start the proxy at the root of the `assets/recordings` directory. Everything else should shake out from there.

### Sync Operation details - push

The `start` point here will be defined by what we settle on for the "main" branch OR the _latest commit on the `auto/<servicename>` branch.

1. If there is no `assets.json`, create it. It should populate rational defaults for the repo. (maybe a recording json in root?)
   1. Check out assets repo `sdk/<service>` directory with the targeted SHA from `assets.json`.
   2. If there is no existing `auto/<servicename>` branch, initialize from `main`. If there _is_ an existing `auto/<servicename>`, check grab the latest commit and use as base.
2. Create a new commit to the branch `auto/<servicename>`. Push.
3. Update assets.json with new SHA from assets repo push.

### Integrating the sync script w/ language frameworks

Each repo has its own language-specific method to start the test-proxy. The _same method_ that starts that test-proxy needs to resolve these commit SHAs and leverage the assets script to checkout the local assets copy to the appropriate target version.

### Test Run

`scbedd` has created a [a test branch](https://github.com/scbedd/azure-sdk-for-python/tree/feature/move-recordings) that has a hacked up local version of everything we talk about above. The scripts are no where near complete and are merely proxies to ensure everything still works as we expect.

That custom script is present in `eng/common/testproxy/assets.ps1`.

Invoke it like:

- `assets.ps1 reset <directory>`
- `assets.ps1 playback <directory>`

So to locally repro this experience:

1. git checkout the branch linked to above.
2. `.\eng\common\TestResources\New-TestResources.ps1 'tables'` -> set environment variables
3. `assets.ps1 playback sdk/tables`
4. `cd sdk/tables/azure-data-tables/`
5. `pip install .`
6. `pip install -r dev_requirements.txt`
7. `pytest`

### A note regarding cross-plat usage

The test-proxy utilizes the `git` of the system running it to retrieve recordings from the assets repository. This means that when loading a recording, the running file system **matters**. When passing a recording path to the test-proxy, ensure that from client side, capitalization is **consistent** across platforms. Let's work through an example.

A test is recorded on `windows`. It writes to relative recording path `a/path/to/recording.json`. On `windows` and `mac`, if a user attempts to start playback for `a/path/To/recording.json`, this would **succeed** at the attempt to load the recording from disk. This is due to the act that the OS is not case-sensitive. On a **linux** system, attempting to load `a/path/To/recording.json` will **fail**.

This is extremely easy to overlook when diagnosing `File not found for playback` issues, as capitalization differences can be difficult to see in context.

If a dev ends up with an asset tag in this situation, the process to resolve it is fairly straightforward.

1. Delete the recording in local `.assets` directory.
2. `push` the asset, getting a new tag _without_ the problematic tag being present.
3. Run recordings, ensuring that the capitalization of the file is correct.
4. `push`. Tests will pass in CI now.

## Integration Checklist

What needs to be done on each of the repos to utilize this the sync script?

To utilize the _base_ version of the script, the necessary steps are fairly simple.

- [ ] Add base assets.json
- [ ] Update test-proxy `shim`s to call asset-sync scripts to prepare the test directory prior to tests invoking.

Where the difficulty _really_ lies is in the weird situations that folks will run into. To get further assistance beyond what is documented here, there are a couple options. Internal MS users, refer to the [teams channel](https://teams.microsoft.com/l/channel/19%3ab7c3eda7e0864d059721517174502bdb%40thread.skype/Test-Proxy%2520-%2520Questions%252C%2520Help%252C%2520and%2520Discussion?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47). For external users, file an issue against this repository and tag label `Asset-Sync`.

## Post-Asset-Move space optimizations

### Test-Proxy creates seeded body content at playback time

For more than a few of the storage recordings, some request bodies are obviously made up of generated content. We can significantly reduce the amount of data that is actually needed to be stored by allowing the test-proxy to _fill in_ these request bodies based on a known seed value (maybe from the original request body?).

## Follow-Ups

- [x] Initial Prototype version of the scripts
- [ ] Tackle Integration and Cleanup issues
  - [ ] Can we target commits instead of branches/tags? That would allow us to move things around under the hood and actually squash if we needed to
  - [ ] Do we make these tags bidirectional? As in they can also contain the local SHA of the language repo at the time of push?
  - [ ] Use `.bat`/`.sh` shims to the powershell script. That's what we do with the New-TestResources script
  - [ ] Can we add telemetry to the tooling?
- [ ] Should the tool be re-written in `go`?

The plan is to tackle .NET first, then move through the other languages that currently support test proxy, tackling any integration issues along the way.
