# Engineering System Workflows and Skills

The `.github/workflows` directory contains the common GitHub actions and scheduled events yml files used in azure-sdk, azure-sdk-tools and the azure-sdk languages repositories. It should only contain YML files.

The `.github/skills` directory contains common [copilot skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills) used in azure-sdk, azure-sdk-tools and the azure-sdk languages repositories, as well as skills local to azure-sdk-tools. 

## Updating

Any updates to files in the `.github/workflows` directory should be made in the [azure-sdk-tools](https://github.com/azure/azure-sdk-tools) repo.

Any updates to directories matching the pattern `.github/skills/azsdk-common-*` should be made in the [azure-sdk-tools](https://github.com/azure/azure-sdk-tools) repo.

All changes made through the sync pipelines will cause PRs to be created in subscribed azure-sdk language repos which will blindly replace the synced files or directories in those repos. For that reason do **NOT** make changes to these files in the azure-sdk or individual azure-sdk language repos as they will be overwritten the next time an update is taken from the corresponding directory in the azure-sdk-tools repository.

## Pipelines

- [`tools - sync-.github`][workflow-yml] syncs selected files from `.github/workflows/`, including `post-apiview.yml` and `protected-files.yml`.
- [`tools - sync-.github-skills`][skills-yml] syncs shared skills from `.github/skills/azsdk-common-*`.

## Workflow

When you create a PR against `azure-sdk-tools` that changes synced workflow targets in `.github/workflows` or shared skills in `.github/skills/azsdk-common-*`, the matching sync pipeline is triggered automatically and mirrors those changes into the subscribed language repositories. The pipeline also triggers language-repository-specific tests for you to review. This process of mirroring involves multiple stages and requires your manual reviews and approvals before the changes are fully reflected to the language repositories. Your approval is needed first to allow automatic creation of PRs, then to allow them being merged on your behalf.

This process is set up in such a way to make it easier for changes to be tested in azure-sdk and each individual language repo before merging the changes in the `azure-sdk-tools` repo. The workflow is explained below:

1. You create a PR (let's call it here the **Tools PR**) in the `azure-sdk-tools` repo with changes to the synced workflow files in `.github/workflows` and/or `.github/skills/azsdk-common-*`.
2. The matching sync pipeline is automatically triggered for the **Tools PR**:
   - [`tools - sync-.github`][workflow-yml] for the synced workflow targets in `.github/workflows`.
   - [`tools - sync-.github-skills`][skills-yml] for `.github/skills/azsdk-common-*` changes.
   - If your PR changes both areas, both pipelines will run.
3. Each triggered pipeline creates branches mirroring your changes, one branch in azure-sdk and one per language repository receiving that sync. You can use these branches to run tests in those repos. The pipeline also queues test runs for template pipelines for each repo. These help you test the changes in the **Tools PR**. All of this is done in the `Create Sync` stage of the corresponding pipeline, specifically through `template: ./templates/steps/sync-directory.yml`.
4. If you make additional changes to your **Tools PR** repeat steps 1 - 3 until you have completed the necessary testing of your changes. This includes full releases of the template package, if necessary.
5. Once you reviewed all the test runs and did any of additional ad-hoc tests from the created branches in language repositories, you must manually approve in your pipeline execution instance the next stage - creation of PRs.
6. Once approved, the pipeline proceeds to the next stage, named `CreateSyncPRs` in the corresponding pipeline YAML. This stage creates one pull request for each language repository, merging changes from the branch created in step 3 into the default (usually `main`) branch. We call these PRs here **Sync PRs**. A link to each of the **Sync PRs** will show up in the **Tools PR** for you to click and review.
7. Review and approve each of your **Sync PRs** and resolve any open review conversations. For some repos (C and C++) you might need to frequently use the `Update Branch` button to get the checks green.
    - You can mass approve and mark AI-generated PR reviews as resolved with [this script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/scripts/Approve-Sync-PRs.ps1): `<repo root>/eng/scripts/Approve-Sync-PRs.ps1 <tools sync PR>`
    - This script will also approve the next stage in the sync pipeline (`CreateSyncPRs` or `VerifyAndMerge`) so it can proceed.
8. Sign off on the `VerifyAndMerge` stage in each triggered sync pipeline. This will merge any remaining open **Sync PR** and also append `auto-merge` to the **Tools PR**.
   - If a **Sync PR** has any failing checks, it will need to be manually merged, even if `/check-enforcer override` has been run ([azure-sdk-tools#1147](https://github.com/Azure/azure-sdk-tools/issues/1147)).

[workflow-yml]: https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/sync-.github.yml
[skills-yml]: https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/sync-.github-skills.yml
