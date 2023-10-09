# tsp-client

A simple command line tool for generating TypeSpec clients.

### Usage
```
tsp-client [options] <outputDir>
```

## Options

### <outputDir> (required)

The only positional parameter. Specifies the directory to pass to the language emitter.

### --emitter, -e (required)

Specifies which language emitter to use. Current choices are "csharp", "java", "javascript", "python", "openapi".

Aliases are also available, such as cs, js, py, and ts.

### --mainFile, -m

Used when specifying a URL to a TSP file directly. Not required if using a `tsp-location.yaml`

### --debug, -d

Enables verbose debug logging to the console.

### --no-cleanup

Disables automatic cleanup of the temporary directory where the TSP is written and referenced npm modules are installed.

## Examples

Generating from a TSP file to a particular directory:

```
tsp-client -e openapi -m https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/specification/cognitiveservices/OpenAI.Inference/main.tsp ./temp
```

Generating in a directory that contains a `tsp-location.yaml`:

```
tsp-client sdk/openai/openai
```
