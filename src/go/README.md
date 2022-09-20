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
