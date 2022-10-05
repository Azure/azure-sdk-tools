# Asset Sync (Retrieve External Test Recordings)

The `test-proxy` optionally offers integration with other git repositories for **storing** and **retrieving** recordings. This enables the proxy to work against repositories that do not emplace their test recordings directly alongside their test implementations.

**Please note**, this feature is under active development as of July 2022 and is not fully integrated or complete.

![image](https://user-images.githubusercontent.com/45376673/180101415-cf864d95-8a8b-4d43-bb05-42604e9f7622.png)

In the context of a `monorepo`, this means that we store FAR less data per feature.

The test-proxy is an excellent place to integrate external data, as packages within the azure-sdk that have moved to leverage it only pass it a single key when loading a recording. That key is passed in the `x-recording-file` header during a POST to `/Playback/Start/`.

This header will contain a value of where the test framework "expects" the recording to be located, expressed as a relative path. EG `tests/SessionRecords/recording1.json`.

The combination of the the `assets.json` context and this relative path will allow the test-proxy to restore a set of recordings to a path, then _load_ the recording from that newly gathered data. The path to the recording file within the external assets repo can be _predictably_ calculated and retrieved given just the location of the `assets.json` within the code repo, the requested file name during playback or record start, and the properties within the assets.json itself. The diagram above has colors to show how the paths are used in context.

## The `assets.json` and how it enables external recordings

An `assets.json` contains _targeting_ information for use by the test-proxy when restoring (or updating) recordings "below" a specific path.

> For the `azure-sdk` team specifically, engineers are encouraged to place their `assets.json` files under a path of form `sdk/<service>/assets.json`

An `assets.json` takes the form:

```jsonc
{
  "AssetsRepo": "Azure/azure-sdk-assets-integration",
  "AssetsRepoPrefixPath": "python",
  "TagPrefix": "python/core",
  "Tag": "python/core<Guid>"
}
```

| Property | Description |
|---|---|
| AssetsRepo | The full name of the external github repo storing the data. EG: `Azure/azure-sdk-assets` |
| AssetsRepoPrefixPath | The assets repository may want to place the content under a specific path in the assets repo. The default is the language that the assets belong to. EG: `python`, `net`, `java` etc. |
| TagPrefix | `<Language>/<ServiceDirectory>` or `<Language>/<ServiceDirectory>/<Library>` or deeper if things are nested in such a manner. |
| Tag | Initially empty until after the first push at which point the tag will be the `<TagPrefix><Guid>` |

Comments within the assets.json are allowed and _maintained_ by the tooling. Feel free to leave notes to yourself. They will not be eliminated.

As one can see in the example image above, the test-proxy does the heavy lifting for push and pull of files to and from the assets repository.

## Restore, push, reset when proxy is waiting for requests

Interactions with the external assets repository are accessible when the proxy is actively serving requests. These are available through routes:

| Route | Description |
|---|---|
| `/Playback/Restore` | Retrieve files from external git repo as targeted in the Tag from assets.json |
| `/Playback/Reset` | Discard pending changes and reset to the original Tag from targeted assets.json. |
| `/Record/Push` | Push changes if they are pending for files described by targeted assets.json. Updates the Tag in assets.json to match the new tag generated from the push operation. |

## test-proxy CLI commands

The test-proxy also offers interactions with the external assets repository as a CLI. Invoking `test-proxy --help` will show the available list of commands. `test-proxy <command> --help` will show the help and options for an individual command. The options for a given command are all `--<option>`, for example, `--assets-json-path`, but each option has an abbreviation shown in the help, those are a single dash. For example the abbreviation for `--assets-json-path` is `-a`.

### The following CLI commands are available for manipulation of assets

#### Restore

A restore operation is merely a test-proxy-encapsulated `clone or pull` operation. A given `asets.json` provides the target `Tag` and `AssetsRepo`.

```bash
> test-proxy restore --assets-json-path <assetsJsonPath>
```

#### Reset

Reset discards local changes to a targeted assets.json files and resets the local copy of the files back to the version targeted by the given assets.json Tag.  Reset would be used if the assets were already restored, modified (maybe re-recorded while library development was done), and then needed to be reset back to their original files. If there are pending changes, the user will be prompted to overwrite. If there are no pending changes, then reset is no-op, otherwise, the following prompt will be displayed.
`There are pending git changes, are you sure you want to reset? [Y|N]`

- Selecting `N` will leave things as they are.
- Selecting `Y` will discard pending changes and reset the locally cloned assets to the Tag within the targeted `assets.json`.

```bash
> test-proxy reset --assets-json-path <assetsJsonPath>
```

#### Push

After assets have been restored and then modified (re-recorded etc.) a push will update the assets in the AssetsRepo. After the push completes, the `Tag` within the targeted assets.json will be updated with the new Tag. The updated asset.json will need to be committed into the language repository along with the code changes.

```bash
> test-proxy restore --assets-json-path <assetsJsonPath>
```
