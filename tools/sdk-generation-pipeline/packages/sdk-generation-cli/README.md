# @azure-tools/sdk-generation-cli

This packages includes some commands used by sdk generation pipeline.

## Install

```shell
npm i @azure-tools/sdk-generation-cli
```

## Commands

### docker-cli
It's used by docker image, and for the details, please refer to  [How to Use Docker Image for SDK Generation](../../documents/docker/README.md).

### run-mock-host
Run this command to start the mock host.
Usage:
```shell
run-mock-host --readme=<path-to-readme> --spec-repo=<path-to-spec-repo> --mock-host-path=<mock-host-install-path>
```
For more details, please refer to [mock service host document](https://github.com/Azure/azure-sdk-tools/tree/main/tools/mock-service-host).

### getRepoName
Get repository name from the http url and set it as azure pipeline variable.
Usage:
```shell
getRepoName <variable-key> <repo-http-url>
```

### generateResult
TODO

### publishResult
TODO

### uploadArtifact
TODO

### prepareArtifactFiles
TODO


