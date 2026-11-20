# Go APIView token file generator

## Getting started

### Prerequisites

- Go 1.16 or above.

### Build the tool

To build the tool, execute the following command from the `./src/go` directory.
```
go build
```

NOTE: To access the command anywhere you can add it to your $PATH variable.

### Run the tool

Run the following command to generate a JSON file for upload to [APIView](https://apiview.dev):
```
./apiviewgo <path to module> <output file location>
```

NOTE: The output file location must be a folder that already exists. Simply use `.` to output to the current directory where the command is being run.

Cross-module aliases are resolved using the dependency version in `go.mod`, not neighboring
checkouts. Set `GOMODCACHE` to use an existing module cache; otherwise missing versions are
downloaded from the Go module proxy. Explicit `replace` directives in the reviewed module
are honored.

The parser rejects public aliases of generic instantiations, composites with hidden component
types, and hoisted method signatures that reference hidden types. It also rejects array-length
expressions it cannot represent reliably. Exposed pointer aliases remain unsupported; defined
pointers to representable types are supported.
