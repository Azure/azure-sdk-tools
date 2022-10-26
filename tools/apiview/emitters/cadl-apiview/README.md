# Cadl APIView Emitter

This package provides the [Cadl](https://github.com/microsoft/cadl) emitter to produce APIView token file output from Cadl source.

## Install

Add `@azure-tools/cadl-apiview` to your `package.json` and run `npm install`.

## Emit APIView spec

1. Via the command line

```bash
cadl compile {path to cadl project} --emit=@azure-tools/cadl-apiview
```

2. Via the config

Add the following to the `cadl-project.yaml` file.

```yaml
emitters:
  @azure-tools/cadl-apiview: true
```

For configuration [see options](#emitter-options)

## Use APIView-specific decorators:

Currently there are no APIView-specific decorators...

## Emitter options:

Emitter options can be configured via the `cadl-project.yaml` configuration:

```yaml
emitters:
  '@azure-tools/cadl-apiview':
    <optionName>: <value>


# For example
emitters:
  '@azure-tools/cadl-apiview':
    output-file: my-custom-apiview.json
```

or via the command line with

```bash
--option "@azure-tools/cadl-apiview.<optionName>=<value>"

# For example
--option "@azure-tools/cadl-apiview.output-file=my-custom-apiview.json"
```

### `output-file`

Configure the name of the output JSON token file relative to the `output-dir`.

### `output-dir`

Configure the name of the output directory. Default is `cadl-output/cadl-apiview`.

### `namespace`

For Cadl specs, the namespace should be automatically resolved as the service namespace. If
that doesn't work, or for libraries (which have no service namespace) this option should be
specified to filter the global namespace. Any subnamespaces of the provided namespace will
also be emitted.

### `version`

For multi-versioned Cadl specs, this parameter is used to control which version to emit. This
is not required for single-version specs. For multi-versioned specs that have a "latest" value,
this will be the default if this option is omitted.

## See also

- [Cadl Getting Started](https://github.com/microsoft/cadl#getting-started)
- [Cadl Tutorial](https://github.com/microsoft/cadl/blob/main/docs/tutorial.md)
- [Cadl for the OpenAPI Developer](https://github.com/microsoft/cadl/blob/main/docs/cadl-for-openapi-dev.md)
