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
Parse the logs produced by tasks, and generate a summarized task result in json format.   
Usage:
```shell
generateResult \
    --buildId=<build-id> \
    --taskName=<task-name> \
    --logfile=<task-log-path> \
    --resultOutputPath=<path-to-generate-result-file> \
    [--dockerResultFile=<all-tasks-result-path>] \
    [--exeResult=<tasks-result-status>] \
    [--taskOutputPath=<addition-object-path>] \
    [--logFilterStr=<specify-filter-for-log>]
```

### publishResult
Publish pipeline result to storage. [eventhub] is supported.   
NOTE: will get eventhub connection string from environment, variable is [EVENTHUB_SAS_URL]   
Usage:
```shell
publishResult \
    --storageType=eventhub \
    --pipelineStatus=<status> \
    --buildId=<build-id> \
    --trigger=<pipeline-trigger> \
    --logPath=<log-path-of-full-log> \
    --resultsPath=<task-result-path-arr>
```

### uploadArtifact
Upload artifact to blob.   
NOTE: will get blob connection string from environment, variable is [AZURE_STORAGE_BLOB_SAS_URL]   
Usage:
```shell
uploadArtifact \
    --generateAndBuildOutputFile=<generateAndBuildOutput-file-path> \
    --buildId=<build-id> \
    --language=<build-language>
```

### prepareArtifactFiles   
Determine which files to upload, copy it to artifact directory.   
Usage:
```shell
prepareArtifactFiles \
    --artifactDir=<artifact-directory> \
    --generateAndBuildOutputFile=<generateAndBuildOutput-file-path> \
    --language=<build-language>
```
