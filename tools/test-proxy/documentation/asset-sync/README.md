## Asset Sync (Retrieve External Test Recordings)

The `test-proxy` optionally offers integration with other git repositories for **storing** and **retrieving** recordings. This enables the proxy to work against repositories that do not emplace their test recordings directly alongside their test implementations.

**Please note**, this feature is under active development as of July 2022 and is not fully integrated or complete.

![image](https://user-images.githubusercontent.com/45376673/180101415-cf864d95-8a8b-4d43-bb05-42604e9f7622.png)

In the context of a `monorepo`, this means that we store FAR less data per feature.

The test-proxy is an excellent place to integrate external data, as packages within the azure-sdk that have moved to leverage it only pass it a single key when loading a recording. That key is passed in the `x-recording-file` header during a POST to `/Playback/Start/`.

This header will contain a value of where the test framework "expects" the recording to be located, expressed as a relative path. EG `tests/SessionRecords/recording1.json`.

The combination of the the `assets.json` context and this relative path will allow the test-proxy to restore a set of recordings to a path, then _load_ the recording from that newly gathered data. The path to the recording file within the external assets repo can be _predictably_ calculated and retrieved given just the location of the `assets.json` within the code repo, the requested file name during playback or record start, and the properties within the assets.json itself. The diagram above has colors to show how the paths are used in context.

### The `assets.json` and how it enables external recordings

An `assets.json` contains _targeting_ information for use by the test-proxy when restoring (or updating) recordings "below" a specific path.

> For the `azure-sdk` team specifically, engineers are encouraged to place their `assets.json` files under a path of form `sdk/<service>/assets.json`

An `assets.json` takes the form:

```jsonc
{
  "AssetsRepo": "Azure/azure-sdk-assets-integration",
  "AssetsRepoPrefixPath": "python/recordings/",
  "AssetsRepoBranch": "auto/test",
  "SHA": "786b4f3d380d9c36c91f5f146ce4a7661ffee3b9"
}
```

| Property | Description |
|---|---|
| AssetsRepo | The full name of the external github repo storing the data. EG: `Azure/azure-sdk-assets` |
| AssetsRepoPrefixPath | The assets repository may want to place the content under a specific path in the assets repo. Populate this property with that path. EG: `python/recordings`. |
| AssetsRepoBranch | The branch within the assets repo that your updated recordings will be pushed to. |
| SHA | The reference SHA the recordings that should be restored from the assets repository. |

Comments within the assets.json are allowed and _maintained_ by the tooling. Feel free to leave notes to yourself. They will not be eliminated.

As one can see in the example image above, the test-proxy does the heavy lifting for push and pull of files to and from the assets repository.

### Restore, push, reset when proxy is waiting for requests

Interactions with the external assets repository are accessible when the proxy is actively serving requests. These are available through routes:

| Route | Description |
|---|---|
| `/Playback/Restore` | Retrieve files from external git repo as targeted in the SHA from assets.json |
| `/Playback/Reset` | Discard pending changes and reset to the original SHA from targeted assets.json. |
| `/Record/Push` | Push changes if they are pending for files described by targeted assets.json. |

### Restore, push, reset as a CLI app

The test-proxy also offers interactions with the external assets repository as a CLI app. What this means is that one could invoke

```bash
> test-proxy --command restore --asetsJsonPath <assetsJsonPath>
```

to pull the necessary recordings files down for a targeted assets.json. The following commands are available.

```bash
> test-proxy --command reset --asetsJsonPath <assetsJsonPath>
> test-proxy --command restore --asetsJsonPath <assetsJsonPath>
> test-proxy --command push --asetsJsonPath <assetsJsonPath>
```

When a `push` activity is completed, the `SHA` value within the targeted `assets.json` will be UPDATED with the new reference to the external assets repository.

As a user, ensure that this new SHA is commit alongside your code changes.