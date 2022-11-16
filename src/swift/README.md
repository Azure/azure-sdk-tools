# APIView for Swift

SwiftAPIView is a plugin that converts `.swift` source code and `.h` Objective-C files to an APIView-compatible token file for the Swift language.

## Getting started

### Prerequisites

* Swift APIView is written in modern Swift 5. Due to this, Xcode 10.2 or higher is required.

### Build Instructions

1. Clone the `azure-sdk-tools` repository.
2. Open the `SwiftAPIView.xcworkspace` file, which should launch Xcode.
3. In Xcode, select the `SwiftAPIView` console scheme and build, which will automatically bring in the necessary dependencies and automatically build `SwiftAPIViewCore`.

### Execution Instructions

You may run the tool directly in Xcode or from the command line.

Parameters:

- `--source`: May target a folder, which will collect and process all `*.swift`, `*.h` and `*.swiftinterace` files in the folder and its subfolders. Can also target a single file.
- `--dest`:  (Optional) The path and desired JSON filename. If not provided, it will output as `SwiftAPIView.json` inside your Documents directory.
- `--package-name`: (Optional) The top-level package name to use. If not provided, Swift APIView will attempt to determine the package name. If unsuccessful, you must supply the value.
- `--package-version`: (Optional) The package version the APIView is being created for. This will affect how the review is listed in APIView (ex: "AzureFoo (version 1.0.0)"). If not provided, Swift APIView will attempt to determine the package version. If unsuccessful, you must supply the value. 

#### Xcode

Edit `SwiftAPIView`'s "Run" scheme. This will allow you to pass command line arguments using the "Arguments Passed on Launch" section in the "Arguments" tab.

<img width="747" alt="Screen Shot 2022-10-06 at 9 27 52 AM" src="https://user-images.githubusercontent.com/5723682/194367976-652256bb-d563-4893-810e-9be03377fbc6.png">


#### Command Line

Navigate to the build artifacts folder from Xcode and then run:
```
./SwiftAPIView --source=<PATH_TO_SOURCE> [--dest=<PATH_TO_DESTINATION_FILE>] [--package-name=<NAME>] [--package-version=<VERSION>]`
```
