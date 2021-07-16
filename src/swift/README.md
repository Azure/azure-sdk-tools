# APIView for Swift

SwiftAPIView is a plugin that converts `.swift` source code or `.swiftinterface` files to an APIView-compatible token file.

## Getting started

### Prerequisites

* Swift APIView is written in modern Swift 5. Due to this, Xcode 10.2 or higher is required.

### Build Instructions

1. Clone the `azure-sdk-tools` repository.
2. Open the `SwiftAPIView.xcodeproj` file, which should launch Xcode.
3. In Xcode, build the project file, which will automatically bring in the necessary dependencies.

### Execution Instructions

You may run the tool directly in Xcode or from the command line.

Parameters:
  `--source`: May target a folder, which will collect all `*.swift` files, or a single `*.swift` or `*.swiftinterface` file.
  `--dest`:  (Optional) The path and desired JSON filename. If not provided, it will output as `SwiftAPIView.json` inside your documents directory. 

#### Xcode

Edit `SwiftAPIView`'s "Run" scheme. This will allow you to pass `--source` and/or `--dest` as arguments using the "Arguments Passed on Launch" section in the "Arguments" tab.

#### Command Line

Navigate to the build artifacts folder from Xcode and then run:
```
./SwiftAPIView --source=<PATH_TO_SOURCE> --dest=<PATH_TO_DESTINATION_FILE>`
```
