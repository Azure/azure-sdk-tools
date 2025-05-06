# Release

## 2025-05-02 - 0.6.0

- Normalize the log message with prefixes
- Report more info in the summary line of log for telemetry purpose
- Removed unused codes

## 2025-04-24 - 0.5.1

- Move service name variable into the package object

## 2025-04-22 - 0.5.0

- Output service names and artifact staging folder in execution report.

## 2025-04-17 - 0.4.2

- Refactor sdk-suppressions.yaml link
- Remove render template's sdk-suppressions link only

## 2025-04-17 - 0.4.1

- Added pull request link to the generated html report
- Added captureTransport to each script log

## 2025-04-10 - 0.4.0

- Added run-mode input parameter
- Deprecated is-triggered-by-pipeline parameter

## 2025-04-03 - 0.3.5

- Excluded '-pr' in the language value when spec repo is private repo

## 2025-04-02 - 0.3.4

- Read the corresponding sdk repository configuration for private repository

## 2025-04-01 - 0.3.3

- Use relative directory paths from 'sdk' as go service name

## 2025-03-27 - 0.3.2

- Ensure relativeFolderPath is correct for all languages
- Ensure GeneratedSDK.StagedArtifactsFolder variable is set for all languiges

## 2025-03-21 - 0.3.1

- Added 'sdk-release-type' to input parameters and 'generateInput' type
- Removed setting pipeline variables of SDK pull request

## 2025-03-14 - 0.3.0

- Removed breaking change label artifact related code
- Refactored loggings for pipeline scenarios and created separted log file

## 2025-03-11 - 0.2.2

- Saved label artifacts in separate folders
- Log issue depended on the 'scriptError' setting in 'swagger_to_sdk_config.json'

## 2025-03-07 - 0.2.1

- Generated SDK breaking change label artifacts in pipeline scenario
- Refactored filter log and added entries for pipeline logging issue
- Log issue depended on the 'scriptError' setting in 'swagger_to_sdk_config.json'

## 2025-03-04 - 0.2.0

- Combined all logging errors from custom scripts into a single entry for devops
- Removed the dependency on the simple-git library and eliminated unused functions

## 2025-02-21 - 0.1.9

- Fixed some warnings output

## 2025-02-18 - 0.1.8

- Formated error log which has ANSI codes to prevent garbled characters are displayed
- Added a new state `NotEnabled` for cases where the language configuration is missing in readme.md or tspconfig.yaml
- Unified the SDK generation process for both the spec PR and release scenarios

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
