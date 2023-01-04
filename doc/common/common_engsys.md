# Common Engineering System

The `eng/common` directory contains engineering files that are common across the various azure-sdk language repos.
It should remain relatively small and only contain textual based files like scripts, configs, or templates. It
should not contain binary files as they don't play well with git.

## Code Structure

To help keep the content of the `eng/common` directory as small as possible we should split the language specific parts of a common script out if possible.

- Keep the language specific logic as a function in the `eng/scripts/Language-Settings.ps1` files found in each language repo.
- Assign the function name to a variable in the `eng/common/scripts/common.ps1` file.
- Call the function using the variable name like so `&$VariableName`.

## Updating

Any updates to files in the `eng/common` directory should be made in the [azure-sdk-tools](https://github.com/azure/azure-sdk-tools) repo.
All changes made will cause a PR to be created in all subscribed azure-sdk language repos which will blindly replace all contents of
the `eng/common` directory in that repo. For that reason do **NOT** make changes to files in this directory in the individual azure-sdk
languages repos as they will be overwritten the next time an update is taken from the common azure-sdk-tools repo.

## Workflow

When you create a PR against `azure-sdk-tools` repo that changes contents of the `eng/common` directory, the PR
triggers an [`azure-sdk-tools - sync - eng-common` pipeline][pipeline] that will mirror all changes in the `azure-sdk-tools eng/common` directory
to the corresponding `eng/common` dirs in the language repositories. The pipeline also triggers language-repository-specific tests for you to review. This process of mirroring involves multiple stages and requires
your manual reviews & approvals before the changes are fully reflected to the language repositories. Your approval is needed first to allow automatic creation of PRs, then to allow them being merged on your behalf.

This process is set up in such a way to make it easier for changes to be tested in each individual language repo before merging the changes in the `azure-sdk-tools` repo. The workflow is explained below:

1. You create a PR (let's call it here the **Tools PR**) in the `azure-sdk-tools` repo with changes to the `eng/common` directory.
2. The [`azure-sdk-tools - sync - eng-common` pipeline][pipeline] is automatically triggered for the **Tools PR**.
3. That pipeline creates branches mirroring your changes, one branch per each language repository to whose `eng/common` dir the changes are mirrored. You can use these branches to run your tests on these repos. The pipeline also queues test runs for template pipelines for each repo. These help you test your changes in the **Tools PR**.  All of this is done in `Create Sync` stage (display name: `Sync eng/common Directory`) [of the pipeline eng-common-sync.yml file][yml], specifically, the logic lives in `template: ./templates/steps/sync-directory.yml`.
    - If there are changes in the **Tools PR** that will affect the release stage you should approve the release test pipelines by clicking the approval gate. The test (template) pipeline will automatically release the next eligible version without needing manual intervention for the versioning. Please approve your test releases as quickly as possible. A race condition may occur due to someone else queueing the pipeline and going all the way to release using your version while yours is still waiting. If this occurs manually rerun the pipeline that failed.
4. If you make additional changes to your **Tools PR** repeat steps 1 - 3 until you have completed the necessary testing of your changes. This includes full releases of the template package, if necessary.
5. Once you reviewed all the test runs and did any of additional ad-hoc tests from the created branches in language repositories, you must manually approve in your pipeline execution instance the next stage - creation of PRs.
6. Once approved, the pipeline proceeds to the next stage, named `CreateSyncPRs` in the [eng-common-sync.yml file][yml]. This stage creates one pull request for each language repository, merging changes from the branch created in step 3 into the default (usually `main`) branch. We call these PRs here **Sync PRs**. A link to each of the **Sync PRs** will show up in the **Tools PR** for you to click and review.
7. Review and approve each of your **Sync PRs**. For some repos (C and C++) you might need to frequently use the `Update Branch` button to get the checks green.
8. Sign Off on the [`VerifyAndMerge` stage][yml]. This will merge any remaining open **Sync PR** and also append `auto-merge` to the **Tools PR**.
   - If a **Sync PR** has any failing checks, it will need to be manually merged, even if `/check-enforcer override` has been run ([azure-sdk-tools#1147](https://github.com/Azure/azure-sdk-tools/issues/1147)).

[pipeline]: https://dev.azure.com/azure-sdk/internal/_build?definitionId=1372&_a=summary
[yml]: https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/eng-common-sync.yml