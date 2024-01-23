# tsp-client

A simple command line tool to facilitate generating client libraries from TypeSpec.

## Installation
```
npm install @azure-tools/typespec-client-generator-cli
```

## Prerequisites
Please note that these prerequisites apply on the repository where the client library is going to be generated. Repo owners should make sure to follow these prerequisites. Users working with a repository that already accepts this tool can continue to see the [Usage](#usage) section.

- Add an emitter-package.json to the repo following this [configuration](#emitter-packagejson).
- Add the [TempTypeSpecFiles](#TempTypeSpecFiles) directory to the .gitignore file for your repository.

## Usage
```
tsp-client <command> [options]
```

## Commands
Use one of the supported commands to get started generating clients from a TypeSpec project.
This tool will default to using your current working directory to generate clients in and will
use it to look for relevant configuration files. To specify a different output directory, use
the `-o` or `--output-dir` option.

### init
Initialize the SDK project folder from a tspconfig.yaml. When using this command pass in a path to a local or remote tspconfig.yaml, using the `-c` or `--tsp-config` flag.

The command generates the appropriate SDK folder in the repository, creates a [tsp-location.yaml](#tsp-locationyaml) file to control generation, and performs an initial generation of the client library. If you want to skip client library generation, then pass the `--skip-sync-and-generate` flag.

### update
Sync and generate client libraries from a TypeSpec project. The `update` command will look for a [tsp-location.yaml](#tsp-locationyaml) file in your current directory to sync a TypeSpec project and generate a client library.

### sync
Sync a TypeSpec project with the parameters specified in tsp-location.yaml.

By default the `sync` command will look for a tsp-location.yaml to get the project details and sync them to a temporary directory called `TempTypeSpecFiles`. Alternately, you can pass in the `--local-spec-repo` flag with the path to your local TypeSpec project to pull those files into your temporary directory.

### generate
Generate a client library from a TypeSpec project. The `generate` command should be run after the `sync` command. `generate` relies on the existence of the `TempTypeSpecFiles` directory created by the `sync` command and on an `emitter-package.json` file checked into your repository at the following path: `<repo root>/eng/emitter-package.json`. The `emitter-package.json` file is used to install project dependencies and get the appropriate emitter package.

### convert
Convert an existing swagger specification to a TypeSpec project. This command should only be run once to get started working on a TypeSpec project. TypeSpec projects will need to be optimized manually and fully reviewed after conversion. When using this command a path or url to a swagger README file is required through the `--swagger-readme` flag.

## Options
```
  -c, --tsp-config          The tspconfig.yaml file to use                      [string]
  --commit                  Commit to be used for project init or update        [string]
  -d, --debug               Enable debug logging                                [boolean]
  --emitter-options         The options to pass to the emitter                  [string]
  -h, --help                Show help                                           [boolean]
  --local-spec-repo         Path to local repository with the TypeSpec project  [string]
  --save-inputs             Don't clean up the temp directory after generation  [boolean]
  --skip-sync-and-generate  Skip sync and generate during project init          [boolean]
  --swagger-readme          Path or url to swagger readme file                  [string]
  -o, --output-dir          Specify an alternate output directory for the 
                            generated files. Default is your local directory.   [string]
  --repo                    Repository where the project is defined for init 
                            or update                                           [string]
  -v, --version             Show version number                                 [boolean]
```

## Examples

Initializing and generating a new client from a `tspconfig.yaml`:

> NOTE: The `init` command must be run from the root of the repository.

```
tsp-client init -c https://github.com/Azure/azure-rest-api-specs/blob/3bae4e510063fbd777b88ea5eee03c41644bc9da/specification/cognitiveservices/ContentSafety/tspconfig.yaml
```

Generating in a directory that contains a `tsp-location.yaml`:

```
tsp-client update
```

## Important concepts

### Per project setup
Each project will need to have a configuration file called tsp-location.yaml that will tell the tool where to find the TypeSpec project.

#### tsp-location.yaml

This file is created through the `tsp-client init` command or you can manually create it under the project directory to run other commands supported by this tool. 

> NOTE: This file should live under the project directory for each service.

The file has the following properties:

| Property | Description | IsRequired |
| --- | --- | --- |
| <a id="directory-anchor"></a> directory | The top level directory where the main.tsp for the service lives.  This should be relative to the spec repo root such as `specification/cognitiveservices/OpenAI.Inference` | true |
| <a id="additionalDirectories-anchor"></a> additionalDirectories | Sometimes a typespec file will use a relative import that might not be under the main directory.  In this case a single `directory` will not be enough to pull down all necessary files.  To support this you can specify additional directories as a list to sync so that all needed files are synced. | false: default = null |
| <a id="commit-anchor"></a> commit | The commit sha for the version of the typespec files you want to generate off of.  This allows us to have idempotence on generation until we opt into pointing at a later version. | true |
| <a id="repo-anchor"></a> repo | The repo this spec lives in.  This should be either `Azure/azure-rest-api-specs` or `Azure/azure-rest-api-specs-pr`.  Note that pr will work locally but not in CI until we add another change to handle token based auth. | true |

Example:

```yml
directory: specification/cognitiveservices/OpenAI.Inference
additionalDirectories:
  - specification/cognitiveservices/OpenAI.Authoring
commit: 14f11cab735354c3e253045f7fbd2f1b9f90f7ca
repo: Azure/azure-rest-api-specs
```

### TempTypeSpecFiles

This tool creates a `TempTypeSpecFiles` directory when syncing a TypeSpec project to your local repository. This temporary folder will contain a copy of the TypeSpec project specified by the parameters set in the tsp-location.yaml file. If you pass the `--save-inputs` flag to the commandline tool, this directory will not be deleted. You should add a new entry in the .gitignore of your repo so that none of these files are accidentally checked in if `--save-inputs` flag is passed in.

```text
# .gitignore file
TempTypeSpecFiles/
```

### emitter-package.json

This will be the package.json that gets used when `npm install` is called by this tool. This replaces the package.json checked into the spec repo and allows each language to fix the version of their emitter to be the same for all packages in their repo.
The file should be checked into this location `<root of repo>/eng/emitter-package.json`

Example:

```json
{
    "main": "dist/src/index.js",
    "dependencies": {
      "@azure-tools/typespec-csharp": "0.1.11-beta.20230123.1"
    }
}
```

Note that tsp compile currently requires the "main" line to be there.
