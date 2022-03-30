# What's the Mock Service Host

The Mock Service Host works as a mock server for ARM FrontDoor Service. It supports all management plane APIs that are defined in the azure-rest-api-specs repo (or any subset of it). It could be invoked from different types of clients including AzureCLI/Terraform/SDKs/Postman, etc. and it has the below capabilities:

1. Verify whether the incoming request meets the Swagger definition.
2. Generate a response based on Swagger and return it to the client.
3. Generate Swagger examples based on the request and response.

![overall.png](doc/pic/overall.png)

# The Behaviour of Mock Service Host

The Mock Service Host could be run in your local environment. After started, it will listen to the following endpoints by default:

-   https://localhost:8441, stateful.
-   http://localhost:8442, stateless http.
-   https://localhost:8443, stateless https.
-   https://localhost:8445, always return 500 to simulate service internal error (except resourcegroup operations).

Since currently the HTTPS certificate is created with domain localhost, so the HTTPS endpoints can only be visited though "localhost".

> **_NOTE:_** If these listening ports are already been used by other apps in your local environment, change the default ports according to [the Configuration](#configuration).

## What's stateful/stateless

Stateful:

-   Client could only call GET/DELETE after CREATE succeeded, otherwise it will get the response code of 404.
-   If cascadeEnabled is true:
    -   The sub resource could only be created if parent resource already exists. For instance: the following resources should be created one after another: resourceGroup-->virtual network-->subnet.
    -   Deleting a parent resource will also delete it's all descendants.
-   If cascadeEnabled is false:
    -   Can create sub resource even if parent does not exist.
    -   Deleting a parent resource will not delete it's children.

Stateless:

-   Mock service host will return a valid mocked response for each valid call of GET/List.

# Getting Started

## Prerequisites

- [Nodejs](https://nodejs.org/en/download/) 14.15 or above

## Run Mock Service Host

### Install Mock Service Host

You need to create a workspace to place the config and cache of Mock Service Host and then use `npm install` to get the latest version.
```shell
# mkdir <MY_WORKSPACE> && cd <MY_WORKSPACE>
# npm install @azure-tools/mock-service-host
```

### Configure Mock Service Host's running environment to use your local swagger

The Azrue Rest Api Spec repo is a companion repo of Mock Service Host. By default, the Mock Service Host downloads [the public azure swagger repo](https://github.com/Azure/azure-rest-api-specs) to the cache folder and loads the target Resource provider based on configuration. You can ask it to load all RPs by add config file '.env' under your workspace.

```
+-- <MY_WORKSPACE>/
|   +-- cache/                   // swagger repos will be downloaded here in the first start-up of mock-service-host
|   +-- .env                     // you can create this file, and add configs in it.
```

Set target swagger files as bellow in .env:

```
specRetrievalGitUrl=https://github.com/Azure/azure-rest-api-specs
specRetrievalGitBranch=main
specRetrievalGitCommitID=6023d2b16a66b70c7a870a003c2e3f6750eacd70
validationPathsPattern=specification/*/resource-manager/*/**/*.json
```

Change specRetrievalGitUrl and specRetrievalGitBranch if you are not using the public swagger main branch. Change specRetrievalGitCommitID if you are not using the branch head. And you can specify a filter rule to enable only your own RP's json files to accelerate the loading speed of Mock Service Host. For instance:

```
validationPathsPattern=specification/mediaservices/resource-manager/Microsoft.Media/**/*.json
```

### Start Mock Service Host

```
# cd <MY_WORKSPACE>
# node node_modules/@azure-tools/mock-service-host/dist/src/main.js
```

Common trouble shootings for starting the web server:

-   Make sure all ports used in Mock Service Host haven't been used by other processes.
-   Try to use sudo/"run as administrator" if failed to start listening.

It takes up to two minutes to load all swagger files in the `azure-rest-api-specs` repo after the Mock Service Host started. When loading finished, a log with "validator initialized" is shown in the console.

## Consume Mock Service Host

You can use different client tool/lib to consume the Mock Service Host after it is started.

- [Azure SDK for GO](doc/consume_with_go.md)
- [Azure CLI](doc/consume_with_cli.md)

# Configuration

You can create a file .env to customize the configurations used at runtime. The file .env should be located at current working directory, for instance:

```
+-- mock-service-host
|   +-- .env                 // configuration files
```

## Customize Mock Service Host listen ports

Bellow options in .env are available to configure the server to listen specific local TCP ports.

```
httpsPortStateful=5001
httpPortStateless=5002
httpsPortStateless=5003
internalErrorPort=5004
```

## Consume a remote swagger repo

You can use below configures to configure the server to load and run against a remote swagger repo:

```
specRetrievalMethod=git
specRetrievalLocalRelativePath=./cache
specRetrievalGitUrl=https://github.com/Azure/azure-rest-api-specs-pr
specRetrievalGitBranch=main
```

## Consume a local swagger repo

Instead of download swagger repo from git remotely, you can configure the Mock Service Host to load your local swagger files:

```
specRetrievalMethod=filesystem
specRetrievalLocalRelativePath=../azure-rest-api-specs
validationPathsPattern=specification/*/resource-manager/*/**/*.json
```

## Configure example generation folder

With below configuration, the REST calling request and mocked response can be preserved in local swagger repo.

```
enableExampleGeneration=true
```

The example files are generated in the **mock** sub-folder relative to the swagger directory, for example:

-   [specRetrievalLocalRelativePath]\specification\apimanagement\resource-manager\Microsoft.ApiManagement\preview\2018-01-01\mock
    You can update exampleGenerationFolder in file .env to use another folder, for instance:

```
exampleGenerationFolder=examples
```

## Configure cascadeEnabled

The stateful endpoint has different behaviour on Create/Delete operations depending on value of **cascadeEnabled**.
The cascadeEnabled is false by default, you can enable it in file .env like below:

```
cascadeEnabled=true
```

