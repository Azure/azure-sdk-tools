# Go APIView token file generator

## Getting started

### Prerequisites

- Go 1.13 or above.

### Generate the tool

To build the tool, execute the following command from the `./src/go` directory.
```
go build
```

NOTE: To access the command anywhere you can add it to your $PATH variable. 

### Run the tool

Run the following command to generate the file containing the tokenized output for the SDK.
```
./apiviewgo <path to SDK> <output file location>
```

NOTE: The output file location must be a folder that already exists. Simply use `.` to output to the current directory where the command is being run.