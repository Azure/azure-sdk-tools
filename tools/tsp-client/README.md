# tsp-client

A simple command line tool for generating TypeSpec clients.

Use one of the supported commands to get started generating clients from a TypeSpec project.
This tool will default to using your current working directory to generate clients in and will
use it to look for relevant configuration files. To specify a different directory, use
the -o or --output-dir option.

### Usage
```
tsp-client <command> [options]
```

## Commands:
  init        Initialize the SDK project folder from a tspconfig.yaml   [string]
  sync        Sync TypeSpec project specified in tsp-location.yaml      [string]
  generate    Generate from a TypeSpec project                          [string]
  update      Sync and generate from a TypeSpec project                 [string]

## Options:
  -c, --tsp-config          The tspconfig.yaml file to use                      [string]
  --commit                  Commit to be used for project init or update        [string]
  -d, --debug               Enable debug logging                                [boolean]
  --emitter-options         The options to pass to the emitter                  [string]
  -h, --help                Show help                                           [boolean]
  --local-spec-repo         Path to local repository with the TypeSpec project  [string]
  --save-inputs             Don't clean up the temp directory after generation  [boolean]
  --skip-sync-and-generate  Skip sync and generate during project init          [boolean]
  -o, --output-dir          The output directory for the generated files        [string]
  --repo                    Repository where the project is defined for init 
                            or update                                           [string]
  -v, --version             Show version number                                 [boolean]

## Examples

Initializing and generating a new client from a `tspconfig.yaml`:

> NOTE: The `init` command must be run from the root of the repository.

```
tsp-client init -c https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/specification/cognitiveservices/OpenAI.Inference/tspconfig.yaml
```

Generating in a directory that contains a `tsp-location.yaml`:

```
tsp-client update
```
