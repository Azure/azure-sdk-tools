# Go APIView token file generator

## Getting started

### Prerequisites

- Go 1.13 or above.

### Generate the tool

To create the command (this will output an executable), run in the `/go` directory:
```
go build
```

NOTE: To access the command anywhere you can add it to your $PATH variable. 

### Run the tool

Output a JSON file with the tokenized output for the SDK by running:
```
./apiview <path to SDK> <output file location>
```

NOTE: The output file location must be a folder that already exists. Simply use `.` to output to the current directory where the command is being run.