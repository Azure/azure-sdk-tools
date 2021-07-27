# Overview

This area is focused on the management of GitHub labels for the various Azure SDK repositories, containing tools and data used for that purpose.  

## Prerequisites

- **.NET SDK:** In order to run the GHCreator tool, a version of the .NET Core SDK capable of executing an application targeting `netcoreapp3.1` is needed.  The latest version of the [.NET Core SDK](https://dotnet.microsoft.com/download) is recommended.

- **PowerShell:** In order to execute the scripts that perform the automation, PowerShell is needed.  The latest version of [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/README.md) is recommended.

- **GitHub personal access token:** When run, the GHCreator tool requires a personal access token for GitHub from a user with the ability to manage labels in each of the Azure SDK repositories.  Instructions for generating a token can be found in the GitHub Docs article "[Creating a personal access token](https://docs.github.com/en/github/authenticating-to-github/keeping-your-account-and-data-secure/creating-a-personal-access-token)."

## Structure

* **root**  
  _The root contains the core scripts for managing labels and the set of label data common across repositories._

* **repository-snapshots**  
  _The container for a snapshot of the labels that exist in each repository; primarily used to help detect and report on new labels that have been added._

* **ghcreator**  
  _The base container for the .NET Core-based tool used to query label information and perform creation/updates.  This is a compiled drop from the [GHCreator repository](https://github.com/AlexGhiondea/ghcreate)._
  
 ## Scripts
 
 ### `deploy-labels.ps1`
   _With the default parameter set, this script will deploy set of labels intended to be common across repositories from their definitions in the `common-labels.csv` file to the repositories identified by the `repositories.txt` file._
   
   ```bash
   # Execute using the default set of label and repository data.
   pwsh ./deploy-labels.ps1 << TOKEN >>
   
   # View the help for the full set of parameters.
   pwsh -Command "get-help ./deploy-labels.ps1 -full"
   ```
   
  ### `refresh-shapshots.ps1`
   _With the default parameter set, this script will query set of labels for each of the repositories identified by the `repositories.txt` file and save them as individual files in the `repository-shapshots` directory._
   
   ```bash
   # Execute using the default set of repository data.
   pwsh ./refresh-shapshots.ps1 << TOKEN >>
   
   # View the help for the full set of parameters.
   pwsh -Command "get-help ./refresh-shapshots.ps1 -full"
   ```
   
  ### `scan-new-labels.ps1`
   _With the default parameter set, this script will query set of labels for each of the repositories identified by the `repositories.txt` file and compare them against the latest snapshot from in the `repository-shapshots` directory. Any new labels found will be written as output._
   
   ```bash
   # Execute using the default set of repository data.
   pwsh ./scan-new-labels.ps1 << TOKEN >>
   
   # View the help for the full set of parameters.
   pwsh -Command "get-help ./scan-new-labels.ps1 -full"
   ```
   
  ## References and Resources
  
  - [GHCreator Usage Guide](https://github.com/AlexGhiondea/ghcreate/blob/master/README.md)
