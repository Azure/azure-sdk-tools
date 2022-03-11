# Moving Recordings out of the azure-sdk-for-X repos

## Setting Context

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

Thankfully, the integration of the test-proxy actually provides a great opportunity for upheavel in the test areas! Not only would we be making big changes in the test area already, but the `storage context` feature of the test-proxy really lends itself well to this effort as well!

## How the test-proxy can ease transition of external recordings

With language-specific record/playback solutions, there must be an abstraction layer that retrieves the recording files from an expected `recordings` folder. Where these default locations are is usually predicated on the tech being used. It's not super terrible, but custom save/load would need to be implemented for each recording stack, with varying degrees of complexity depending on how opinionated a given framework is.

Contrast this with the the test-proxy, which starts with a **storage context**. This context is then used when **saving** and **loading** a recording file.

Users of the test proxy are encouraged to provide a `relative path to test file from the root of the repo` as their recording file name. A great example of this would be...

```text
sdk/tables/azure-data-tables/recordings/test_retry.pyTestStorageRetrytest_no_retry.json
[----------------test path--------------------------][--------------test name----------]
```

[What this looks like in source](https://github.com/YalinLi0312/azure-sdk-for-python/blob/yall-tables-testproxy/sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_no_retry.json)

Given that the test-proxy is storing/retrieving data independent of the client code (other than the key), client side changes for a wholesale shift of the recordings location is actually simple. The source code for the test _won't need to change at all_. From the perspective of the client test code, nothing has functionally changed.

All that needs to happen is to start the test proxy under a different storage context!

If you were invoking the test-proxy as a docker image, the difference in initilization is as easy as:

#### Old

`docker run -v C:/repo/sdk-for-python/:/etc/testproxy azsdkengsys.azurecr.io/engsys/testproxy-lin:latest`

#### New

`docker run -v C:/repo/sdk-for-python-assets/:/etc/testproxy azsdkengsys.azurecr.io/engsys/testproxy-lin:latest`

Given the same relative path in the assets repo, 0 changes to test code are necessary.

## Evaluated  options for storage of these external recordings

Prior to ScottB starting on this project, JimS was the one leading the charge. As part of that work, Jim explored few potentional storage solutions. He did not evaluate these strictly from a `usability` standpoint.

- **external git repo**
- git modules
- git lfs
- blob storage

He also checked other measures, like `download speed` and `integration requirements`. Blob storage especially has a good story for "public read, private write", and _does_ support snapshotting. However, the cost is low bulk download speed when NOT being run on a tool like `azcopy`.

[These and other observations are recorded in his original document available here.](https://microsoft.sharepoint.com/:w:/t/AzureDeveloperExperience/EZ8CA-UTsENIoORsOxekfG8BzwoNV4xhVOIzTGmdk8j4rA?e=DFkiII)

## But if we already HAVE a problem with ever expanding git repos, why does an external repo help us?

Because the automation interacting with this repository should only ever clone down _a single commit_.

Yes, commit histories do add _some_ weight to the git database, but it's definitely not a super impactful difference.

## So what needs to happen if the test proxy is already so well suited to this task?

This is where the story gets complicated. Now that recordings are no longer stored directly alongside the code that they support, the process to _update_ the recordings gets a bit more stilted.

In a previous section, we established that another git repo is the most obvious solution.

Today, these `assets` repos exist for the four main languages.

- [Azure/azure-sdk-for-python-assets](https://github.com/Azure/azure-sdk-for-python-assets)
- [Azure/azure-sdk-for-js-assets](https://github.com/Azure/azure-sdk-for-js-assets)
- [Azure/azure-sdk-for-net-assets](https://github.com/Azure/azure-sdk-for-net-assets)
- [Azure/azure-sdk-for-java-assets](https://github.com/Azure/azure-sdk-for-java-assets)

We have the following resources at play

```bash
Azure/azure-sdk-for-python
Azure/azure-sdk-for-python-assets
```

Given the split nature of the above, We need to talk about how the test-proxy knows **which** recordings to grab. We can't simply default to `latest main`, as that will _not_ work if we need to run tests from an earlier released version of the SDK.

To get around this, we will embed a SHA into the language repository. When the test-proxy is manually or automatically started, we need to ensure that the local assets repo aligns to that SHA.

```nash
<repo-root>
   /recordings.json
```

And within the file...

```json
{
   "/": "4e8e976b7839c1e9c6903f48106e48be76868a5d"
   "sdk/tables/": "4e8e976b7839c1e9c6903f48106e48be76868a5d"
}
```

While this works really well for local playback, it does not work for submitting a PR with your code changes. Why? Because the PR checks won't _have_ your updated assets repo that you may have created by recording your tests locally!

This necessitates a script that can be queued **against a local branch or PR** that will push a commit to the `assets` repo and then update the **local reference** within `recordings.json` to consume it.

You will note that the above JSON configuration lends itself well to more individual solutions, while allowing space for more _targeted_ overrides later. (Note the catch all root path.

## How will we integrate these "sync" scripts?

Alright, so we know how we want to structure the `recordings.json`, and we know WHAT needs to happen. Now we need to delve into the HOW this needs to happen. Colloqially, anything referred to as a `sync` operation should be understood to be part of these abstraction scripts handling git pull and push operations.

### Implementation Language

To remain as out of the way as possible, it would be rational to support two versions of these `sync` scripts. `pwsh` and `sh`. However, it is easy to see a world where we have bugs in one version or the other of the sync library.

Given that, we should probably just target `powershell`.

### Sync operations description and listing

The external repo will just be a _git_ repo, so it's not like devs won't be able to `cd` into it and resolve any conflicts themselves. However, that should not be the _norm_. To avoid this, we need the following functionality into these scripts.

| Operation | Description |
|---|---|
| Sync | When one first checks out the repo, one must initialize the recordings repo so that we can run in `playback` mode.  |
| Push | Submits a PR to the assets repo, updates local `recordings.json` with new SHA. |
| Reset | Return assets repo and `recordings.json` to "just-cloned" state. |

### Sync Operation triggers

At the outset, this will need to be manually run. How far down this manual path do we want to go?

Options:

- `Pre-commit` hook ala typescript linting
  - This works for _changes_, but how about for a fresh repo? Initialize has gotta happen at some point. Automagic stuff could also result in erraneous PRs to the  
- Scripted invocation as part of a test run. For `ts/js`, this is actually simple, as `npm <x>` are just commands defined in the packages.json file. For others, this may be a bit closer to manual process.

## Sync Operation Details - Pull

The initialization of the assets repo locally should be a simple clone w/ 0 blobs.

```
.git
<files at root>
folder1/
folder2/
  folder3/
```

As `playback` sessions are started, the repo should:

1. Discard pending changes, reset to empty
2. Add `sparse-checkout` for the service folder needed by the current playback request.
3. `checkout` exact SHA specified in `recordings.json`
4. `pull`

Given the context advantages discussed earlier, one most only start the proxy at the root of the `assets` directory. Everything else should shake out from there.

## Sync Operation Details - Push

The `start` point here will be defined by what we settle on for the "main" branch. TODO: discuss storage option and then finish this.

TODO: there is no conclusion here it just kinda ends. Need summarize.

## Test Run

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



FEEDBACK FROM WES
  we should cover having the internals of each test framework checkout, etc
  What would the configuration look like there?

  Ever expanding size of the repo? We've already noted that these sync operations CAN be part of the framework. If they're only ever sparse-checkout-ing a single SHA, we should be fine.

  We can still have a common script that does this kinda stuff manually though.

  MERGE COMMITS ONLY WORK IF A SINGLE COMMIT IS NOT SQUASHED.
    Merge commits do not work.

  Primary feedback from Wes 1:1
    Get rid of Base SHA
    
    Description of base methodology ->
      conflicts shouldn't happen -> just re-record -> push to branch
      Map out what this would look like. They should be based off master.

    // ensure that we leave a comment describing why
    Split the `recordings.json` into a service directory

    Postulate additional metadata to recordings.json



