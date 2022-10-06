# Transitioning recording assets from language repositories into <https://github.com/Azure/azure-sdk-assets>

Part of the migration, moving to the common test-proxy service, will require migrating the recording assets from the individual language repositories into <https://github.com/Azure/azure-sdk-assets> as well as creating the assets.json file for those assets. To that end a script was created that'll create the assets.json and migrate the test recordings into the assets repository. It's worth noting that the script only handles migrating recording assets.

The script is [generate-assets-json.ps1](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/transition-scripts/generate-assets-json.ps1)

## Setup

Running the script requires the following:

1. Git needs to be on the machine and in the path. Git is used by the script and test-proxy.
2. test-proxy needs to be on the machine and in the path. Instructions for that are [here](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/Azure.Sdk.Tools.TestProxy/README.md#installation). The script uses test-proxy's CLI commands.
3. Powershell, version 7 or higher.

## Running the script

[generate-assets-json.ps1](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/transition-scripts/generate-assets-json.ps1) is a standalone powershell script with no supporting script requirements. The easiest way to run the script would be to pull the <https://github.com/Azure/azure-sdk-tools> repository locally and running the script directly from there Eg. `c:\src\azure-sdk-tools\tools\test-proxy\transition-scripts\generate-assets-json.ps1`

The script needs to be executed inside an `sdk\<ServiceDirectory>` or deeper, in an up to date language repository. A good rule here would be look at where the ci.yml is for an service directory, or, in the case where each library for a given service directory has their own pipelines, at the `sdk\<ServiceDirectory><Library>` level. In either case, the ci.yml would be in their respective directories.

For example, running the script from `C:\src\azure-sdk-for-python\sdk\tables>` would be as follows:
`C:\src\azure-sdk-for-java\sdk\attestation> C:\src\azure-sdk-tools\tools\test-proxy\transition-scripts\generate-assets-json.ps1 -InitialPush $true`
This will create the assets.json file in the `sdk\attestation` directory, it would move all of the **/*/session-records/*/*.json files into a temporary directory, setup by the test-proxy, perform the initial push and update the Tag, in the assets.json, to the tag that was just created and pushed to. After running a script, executing a `C:\src\azure-sdk-for-python\sdk\tables>git status` in the directory will show the new assets.json, which needs to be added, and then any recordings from subdirectories would show up as deleted since they were moved.

Running the script without the `-InitialPush` option will just create the assets.json with an empty tag.

### What's the script doing behind the scenes?

The script does the following:

1. It creates the assets.json file in the current working directory. Initially, the Tag in the assets.json file is empty as nothing has been pushed yet. It uses git to analyze the remotes to get the repository language. The AssetsRepoPrefixPath, in the assets.json is set to the language. If `-InitialPush $true` has not been set, the script stops here and exits.
2. A temp directory is created and the test-proxy's CLI restore is called on the current assets.json. Since there's nothing there, it sets up the empty repository.
3. The recordings are moved from their initial directories into temp directory that was setup in the previous step. The paths are underneath the `sdk\<ServiceDirectory>` are preserved. For example, the recordings for `C:\src\azure-sdk-for-python\sdk\tables` live in the `azure-data-tables/tests/recordings` subdirectory and in the target repository they'll live in `python/sdk/tables/azure-data-tables/tests/recordings`. The reason for the language in the directory path because all language are going to live in the same assets repository.
4. test-proxy's CLI push will be called and the initial push will be done into the assets repository. When the push completes the Tag, in the assets.json file, will be updated. This push will be automatic and will not require approval.

At this point the script is complete. The assets.json and deleted recording files will need to be pushed into the language repository.

#### Why does the script analyze the remotes to compute the language?

We need to do this because the language is used in several places.

1. The AssetsRepoPrefixPath in assets.json is set to the language.
2. The TagPrefix is set to the `<language>/<ServiceDirectory>` or `<language>/<ServiceDirectory>/<Library>` etc.
3. The language also used to determine what the [recording directories within a repository are named](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/transition-scripts/generate-assets-json.ps1#L47).
