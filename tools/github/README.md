# Overview

This area is focused on the management of the Azure SDK GitHub repositories, containing tools and data used for that purpose.  

## Prerequisites

- **PowerShell:** In order to execute the scripts that perform the automation, PowerShell is needed.  The latest version of [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/README.md) is recommended.

- **GitHub CLI:** Repository data, such as milestones and labels, are read and written using the [GitHub CLI](https://github.com/cli/cli/tree/trunk#installation).  The latest version is recommended.

## Structure

* **root**  
  _The root contains the core scripts for managing data and the set of centrally managed Azure SDK repositories._

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
   
## References and Resources
  
- [GitHub CLI Docs](https://docs.github.com/en/github-cli)
