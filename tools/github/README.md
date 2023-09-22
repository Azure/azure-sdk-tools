# Overview

This area is focused on the management of the Azure SDK GitHub repositories, containing tools and data used for that purpose.  

## Prerequisites

- **PowerShell:** In order to execute the scripts that perform the automation, PowerShell is needed.  The latest version of [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/README.md) is recommended.

- **GitHub CLI:** Repository data, such as milestones and labels, are read and written using the [GitHub CLI](https://github.com/cli/cli/tree/trunk#installation).  The latest version is recommended.

## Structure

## Structure

* **root**  
  _The root contains the scripts used for repository management and their associated data._

* **scripts**  
  _The core scripts for repository management, such as managing milestones and labels._

* **data**  
  _The data associated with the scripts.  This includes items such as the set of centrally managed Azure SDK repositories and the labels common to all repositories._

* **data/repository-snapshots**  
  _The container for a snapshot of the labels that exist in each managed repository; primarily used to help detect and report on new labels that have been added outside the common set._

 ## Scripts
 
 ### `Add-AzsdkMilestones.ps1`
 
 _Creates a set of milestones in one or more repositories.  The milestones follow Azure SDK conventions for naming and due dates.  With the default parameter set, milestones will be created for the next 6 months in repositories in the 'repositories.txt' file in the script directory._
   
 ```powershell
 # Create the default set of milestone and repository data.
 ./Add-AzsdkMilestones.ps1
   
 # Create milestones in the .NET and Java repositories for a specific date range.
 ./Add-AzsdkMilestones.ps1 -Languages 'net', 'java' -StartDate 2023-01-01 -EndDate 2023-07-31
   
 # View the help for the full set of parameters.
 get-help ./Add-AzsdkMilestones.ps1 -full
 ```
 
 ### `Add-AzsdkProjectIssues.ps1`
 _Adds a set of Azure SDK repository issues tagged with a given set of labels to a GitHub project.  With the default parameter set, the issues with the specified labels will be queried from a set of core language repositories._
   
 ```powershell
 # Adds issues with the "Client" and "KeyVault" labels to project #150, querying a set 
 # of core language  repositories.
 ./Add-AzsdkProjectIssues.ps1 -ProjectNumber 150 -Labels Client, KeyVault
   
 # Adds issues with the "KeyVault" label in the .NET repository to project #150, setting
 # the "Status" custom field to "Todo".
 ./Add-AzsdkProjectIssues.ps1 -Languages 'net' -ProjectNumber 150 -Labels KeyVault -Fields @{Status="Todo"}
   
 # View the help for the full set of parameters.
 get-help ./Add-AzsdkProjectIssues.ps1 -full
 ```
 
 ### `Sync-AzsdkLabels.ps1`
   _Creates or updates the set of labels expected to be common across the Azure SDK repositories, ensuring that names, descriptions, and colors share the common configuration._
   
   ```powershell
   # Uses the files from the `data` directory to synchronize the common labels to all centrally managed 
   # repositories using the default delay between each repository to guard against GitHub throttling.
   ./Sync-AzsdkLabels.ps1 

   # Synchronize the common labels to the Azure SDK for .NET repository.
   ./Sync-AzsdkLabels.ps1 -LabelsFilePath "../data/common-labels.csv" -Languages 'net' 
   
   # View the help for the full set of parameters.
   get-help ./Sync-AzsdkLabels.ps1 -full
   ```

  ### `Snapshot-AzsdkLabels.ps1`
   _Creates or updates the set of labels expected to be common across the Azure SDK repositories, ensuring that names, descriptions, and colors share the common configuration._
   
   ```powershell
   # Uses the files from the "data" directory to generate snapshots of non-common labels for each 
   # of the centrally managed repositories in the "data/repository-snapshots" directory while 
   # writing any new non-common labels created for the repository to the host.
   ./Snapshot-AzsdkLabels.ps1 -Diff

   # Create a snapshot for the Azure SDK for .NET repository, treating the labels defined in 
   # "../data/common-labels.csv" as the expected common set.  The resulting snapshot is written to the ""./snapshots" directory.
   ./Snapshot-AzsdkLabels.ps1 -LabelsFilePath "../data/common-labels.csv" -Languages 'net' -RepositoryFilePath "snapshots"
   
   # View the help for the full set of parameters.
   get-help ./Snapshot-AzsdkLabels.ps1 -full
   ```
   
## References and Resources
  
- [GitHub CLI Docs](https://docs.github.com/en/github-cli)
