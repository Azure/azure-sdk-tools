# Pipeline v3


## State of the world

The azure-sdk maintains a common build definition pattern for all our repositories.

```text
<language repo root>
  eng/
    <devops yml>
  sdk/
    keyvault/
      ci.yml               <-- recorded tests
      tests.yml            <-- livetests
      azure-keyvault-keys/
        tests.yml          <--- package specific livetests
```

A devops build definition exists for each `ci.yml` and most `tests.yml` that are present in a given language repo. For the `ci.yml`, there exists an `internal` and `public` version of the build. The former of which is used to **release the package**.

The important detail here is that in most cases, an `internal` and `public` run are **mostly the same** when it comes to the _amount_ of testing that happens.

Let's draw that out using `python core` as an example.

![What tests are repeated?](_imgs/example.png "What is repeated")

As you can see, given there is no intelligence on _what_ to run, we end up running the **full** test suites for all packages for _all_ platforms. This is a ton of redundant testing that _probably_ doesn't catch that many issues.

> Note: The azure-sdk-for-net core PR builds break up the invoked tests across multiple agents into what are called "dependency groups". That strategy is the general thrust of `pipelinev3`, just a bit beyond the initial rollout steps. The sdk-for-net doesn't attempt to narrow the selected packages, it _expands_ to run all the tests that rely on core. Is this something that we want to keep around for pipelinev3? Probably.

## Goals

`pipelinev3` is a generic title, but here are the outcomes in order of priority (highest first).

- A single `public` build pipeline exists to service all `PR` builds. This will appear as `<language> - pr`.
  - All other `public` builds that have `internal` versions will be deleted. 
  - These per-service `internal` builds will remain separate for ease of release, scheduled build notification access, and ownership reasons.
- The single public build will expand and contract to **just the packages that were touched in the changed files of the Pull Request**.
  - We **must** have an early integration meeting with each language team ahead of the rollout of their job to highlight which langauges will be built for various sample changesets.
  - They **must** agree with the package selection logic. EG: for `python`, do changes to `core` always run `azure-storage-blob`?
- The single public build will be based upon _most_ of the same yml as our `internal` build. The changes that `pipelinev3` should bring _granularity_ but not forced exclusion from existing builds. This is a step forward, not throwing away efforts that already exist. [Example PR](https://github.com/Azure/azure-sdk-for-python/compare/main...pipelinev3). 

What does this actually LOOK LIKE?

- [Here is an example PR](https://github.com/Azure/azure-sdk-for-python/pull/36294/files)

In the above PR, two files were changed:

- `sdk/appconfiguration/test-resources.json`
- `sdk/containerregistry/azure-containerregistry/tests/testcase.py`

The above changed files will only queue build and test for `azure-containerregistry`. No other packages will be built or tested.

`scbedd` will get buy-in from the appropriate language devs to discuss how 


## Rollout

For each language:

- Meeting with <language> team
- Agree on which packages will trigger what
- `scbedd` will integrate necessary code changes to repo and prepare a build that is available on `main`, but only triggered based on `core` folder
- Integrate feedback from owning team
- When we determine we are ready for rollout, we will:
  - mass disable the `python - X - ci` jobs 
  - merge a PR that adds `/` to the PR triggers for the new common build.
- Watch and deal with fallout.

## Next Steps

This will require further reliance on each language team. These less obvious improvements revolve around:

1. Sparseness. Can we run a specific test just once across all platforms that we test?
2. Changed files. Can we ascertain to only run specific tests given a set of changed files?
3. Can we cease running a test if it's not affected for a while? Just rely on nightly?

All of these questions are _also_ part of pipelinev3, but not on first rollout where we get the biggest bang for the least uncertainty.

