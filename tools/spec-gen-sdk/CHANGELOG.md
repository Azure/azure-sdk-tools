# Release

## 2025-02-11 - 0.1.9

- The writeTmpJsonFile function now clears the file content using fs.truncateSync before writing new content to ensure no residual data remains.

## 2025-02-11 - 0.1.7

- Excluded general messages containing 'error' from being displayed in the Azure pipeline results

## 2025-02-11 - 0.1.6

- Enabled error messages to be displayed in the Azure pipeline result
- Generate a markdown file with the package results and publish it to the Extensions tab in the Azure pipeline result
- Updated the 'is-triggered-by-pipeline' parameter type to string

## 2025-01-27 - 0.1.5

- Introduced a root folder to save all the logs and artifacts

## 2025-01-22 - 0.1.4

- Deprecated 'azure-sdk-for-net-track2' and repurposed 'azure-sdk-for-net' for the .NET track2 SDK
- Added functionality to generate an HTML file for the filtered log

## 2025-01-14 - 0.1.3

- Ensure the PrBranch variable is consistently set in all scenarios

## 2025-01-07 - 0.1.2

- Cleaned up dependencies
- Set variables for pipeline scenarios
- Added alias for CLI parameters

## 2024-12-19 - 0.1.1

- Added saveFilterLog function to save the filtered log
- Introduced loggerWaitToFinish function to ensure log transports complete

## 2024-12-17 - 0.1.0

- Initial Release
