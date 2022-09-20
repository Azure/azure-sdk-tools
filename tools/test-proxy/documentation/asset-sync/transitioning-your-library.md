# How to transition an existing recording

This transition document is written for the intended audience of Azure SDK devs transitioning an _existing_ set of recordings from being stored within their language repo to being stored in the [azure/azure-sdk-assets](https://github.com/azure/azure-sdk-assets) repository.

## Minimum requirements

- [x] The transition script is written in **powershell core**
- [x] A working installation of either `.NET>5.0` OR `docker`
- [x] Git version `>2.25.1`

## Nomenclature

We will refer to `language` repo and `assets` repo. It is important to why we name these this way.

The `test-proxy` tool is integrated with the ability to automatically restore these assets. This process is kick-started by the presence of an `assets.json` alongside a dev's actual code. This means that while assets will be cloned down externally, the _map_ to those assets will be stored alongside the tests. We would normally prescribe an `assets.json` under the path `sdk/<service>`. However, more granular storage is also possible.

EG:

- `sdk/storage/azure-storage-blob/assets.json`
- `sdk/storage/azure-storage-file-datalake/assets.json`

We call the location of the actual test code the `language repo`.

The location of these automatically restored assets is colloquially called the `assets repo`. There is an individual `assets repo` cloned for **each `assets.json` in the language repo.**

## In practice

- Swap into the directory that you want to convert to external recordings.
- Run the transition script provided TODO: HERE
- Delete your existing recordings, run your tests in `record` mode.
- Push resulting recordings using `test-proxy push <assets-json>`
  - The test-proxy will update your local assets.json with the new tag.
