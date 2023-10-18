# tsp-client

A simple command line tool for generating TypeSpec clients.

### Installation
```
npm install @azure-tools/typespec-client-generator-cli
```

### Usage
```
tsp-client <command> [options]
```

## Commands:
Use one of the supported commands to get started generating clients from a TypeSpec project.
This tool will default to using your current working directory to generate clients in and will
use it to look for relevant configuration files. To specify a different directory, use
the `-o` or `--output-dir` option.

### init
Initialize the SDK project folder from a tspconfig.yaml. When using this command pass in a path to a local or remote tspconfig.yaml, using the `-c` or `--tsp-config` flag.

### update
Sync and generate client libraries from a TypeSpec project. The `update` command will look for a `tsp-location.yaml` file in your current directory to sync a TypeSpec project and generate a client library.

### sync
Sync a TypeSpec project with the parameters specified in tsp-location.yaml.

By default the `sync` command will look for a tsp-location.yaml to get the project details and sync them to a temporary directory called `TempTypeSpecFiles`. Alternately, you can pass in the `--local-spec-repo` flag with the path to your local TypeSpec project to pull those files into your temporary directory.

### generate
Generate a client library from a TypeSpec project. The `generate` command should be run after the `sync` command. `generate` relies on the existence of the `TempTypeSpecFiles` directory created by the `sync` command and on an `emitter-package.json` file checked into your repository at the following path: `<repo root>/eng/emitter-package.json`. The `emitter-package.json` file is used to install project dependencies and get the appropriate emitter package.

## Options:
```
  -c, --tsp-config          The tspconfig.yaml file to use                      [string]
  --commit                  Commit to be used for project init or update        [string]
  -d, --debug               Enable debug logging                                [boolean]
  --emitter-options         The options to pass to the emitter                  [string]
  -h, --help                Show help                                           [boolean]
  --local-spec-repo         Path to local repository with the TypeSpec project  [string]
  --save-inputs             Don't clean up the temp directory after generation  [boolean]
  --skip-sync-and-generate  Skip sync and generate during project init          [boolean]
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
