# APIView for Swift

SwiftAPIView is a plugin that converts `.swift` source code and `.h` Objective-C files to an APIView-compatible token file for the Swift language.

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
  `--source`: May target a folder, which will collect and process all `*.swift`, `*.h` and `*.swiftinterace` files in the folder and its subfolders. Can also target a single file.
  `--dest`:  (Optional) The path and desired JSON filename. If not provided, it will output as `SwiftAPIView.json` inside your Documents directory.
  `--package-name`: (Optional) The top-level package name to use. If not provided, Swift APIView will attempt to determine the package name. If unsuccessful, supply the value or it will use "Default".

#### Xcode

Edit `SwiftAPIView`'s "Run" scheme. This will allow you to pass command line arguments using the "Arguments Passed on Launch" section in the "Arguments" tab.

#### Command Line

Navigate to the build artifacts folder from Xcode and then run:
```
./SwiftAPIView --source=<PATH_TO_SOURCE> [--dest=<PATH_TO_DESTINATION_FILE>] [--package-name=<NAME>]`
```
