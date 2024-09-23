This page contains guidelines for developing or updating powershell scripts used by the Azure SDK engineering system.

Table of Contents
=================

- [Table of Contents](#table-of-contents)
  - [TLDR](#tldr)
  - [Structure](#structure)
  - [Functionality](#functionality)
  - [Style](#style)
  - [Testing](#testing)
    - [Unit Testing](#unit-testing)
      - [Running Pester Tests](#running-pester-tests)
    - [Local/Pipeline Functional Testing](#localpipeline-functional-testing)

## TLDR

_(i.e. too long, didn't read)_

**DO**

- Write scripts that can be run locally
- Write unit tests for your scripts. See [Unit Testing](#unit-testing).
- Handle exit codes from external programs
- Use `Set-StrictMode -Version 4` where possible
- Define your data structures as much as possible via composition, or unit test or comment examples.
- Use an editor extension or `Invoke-ScriptAnalyzer` to analyze code.

**DON'T**

- Write medium-large scripts inline to azure pipelines yaml (unless absolutely necessary)
- Write code without putting logic into functions. Scripts grow in size and complexity over time, and if they start out un-organized, they tend to stay un-organized.

## Structure

When structuring a powershell cmdlet or collection of scripts, it is important to think about how easy that code will be to re-use and to test (primarily unit tests and/or running locally). 

- When writing a powershell cmdlet file, keep the file as small as possible, or organize your code into functions. Scripts tend to grow in size and complexity over time with incremental edits from many authors, and it is much easier to understand, test and refactor code when it is written in a modular way from the start.
- Isolate calls to dependencies inside their own cmdlet files or within functions. This makes it easier to abstract these calls for testing.
- Add `[CmdletBinding()]` to the top of scripts to support [powershell common parameters](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_commonparameters?view=powershell-7.2).
- Avoid global state where possible. Powershell has very liberal scoping rules, and it is easy to take dependencies on variables that are defined outside a code block, which can lead to hard to debug issues. Where possible, only treat script parameters and variables defined at the top of the file as read-only global variables.
- Try not to rely on exporting your code as a powershell module. Favor [dot sourcing](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scripts?view=powershell-7.2#script-scope-and-dot-sourcing) when possible. Module imports are more finicky in the dev loop and and cause more trouble than they are worth.
- Import our common powershell modules for handling operations like logging, github/devops APIs, etc. See [common.ps1](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/common.ps1) for more information. This file is mirrored to every SDK repository.

## Functionality

There are many quirks to how powershell works that make trade offs between being a good command line shell vs. being a good scripting language. Here are some things to keep in mind when writing scripts:

- Reserve powershell pipelines (`|` operator) for simple assignment operations only (e.g. light transformations of a data structure)
  - If side effects are needed, favor `foreach` loops.
  - If the value being returned is any sort of collection (e.g. list), either favor a `foreach` loop, define the type of the variable like `[array]$arr`, or wrap it with array syntax `@()` like `$arr = @(some-func | ? { $_ -contains "foo" })` . Due to how pipelines work, when the result value is singular, the inner value with the collection item type will be returned instead of a collection with length 1. _This goes the same for powershell functions._
- Handle exit codes from calls to external binaries (e.g. `git`).
  - Powershell by default will only stop execution on thrown exceptions. Calls to external binaries do not throw powershell native exceptions, so failures will not be caught unless explicitly handled.
    - Check the `$LASTEXITCODE` variable for values greater than 0.
    - Use the new `$PSNativeCommandUseErrorActionPreference` variable coming in 7.3 (https://docs.microsoft.com/en-us/powershell/scripting/whats-new/what-s-new-in-powershell-73?view=powershell-7.2#experimental-features).
- Set global powershell variables to achieve consistent behavior:
  - `$ErrorActionPreference` should be set to `Stop` at the top of any cmdlet files unless there is a specific reason to do something else.
  - `Set-StrictMode -Version 4` should be set at the top of any cmdlet files unless there is a specific reason to do something else.
- Support the common `-WhatIf` parameter. See [SupportsShouldProcess docs](https://docs.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-shouldprocess?view=powershell-7.2#supportsshouldprocess).
  - Use `[CmdletBinding()]` at the top of the script to enable.
  - Scripts should be able to be run locally, and they should also support a dry run mode via `-WhatIf` so a user can see what actions might be performed that could be destructive.
  - Most common powershell module functions will support `-WhatIf` natively, but engsys code that does any write operations should also gate this functionality behind this parameter by checking [ShouldProcess](https://docs.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-shouldprocess?view=powershell-7.2#pscmdletshouldprocess).
- Log write actions with enough context that someone reading the log statement in a pipeline would be able to know what happened.
  - For example, `Write-Host "Updating code owner to '$user' in '$ServiceDirectory'"` is a much better log than `Write-Host "Updating codeowner"`.

## Style

Powershell is a very flexible language. In engsys we try to use a smaller subset of what's possible to keep programs consistent and their behavior predictable.

- Do NOT call functions like `myFunc(1, 2, 3)`. Powershell functions must be invoked like `myFunc 1 2 3`. The former syntax will end up calling `myFunc` with the single array-type argument value `@(1,2,3)`.
- Declare types for function parameters (e.g. `function foo([string]$bar) {}`.
- Add formatted documentation for entrypoints like top-level scripts or publicly exposed functions.
  ```
  <#
  .SYNOPSIS
  <blah>
  
  .DESCRIPTION
  <blah>
  
  .PARAMETER baz
  <blah>
  #>
  function foobar([string]$baz)
  ```
- Avoid complex pipeline statements.
- Use explicit `return` statements for returning function/script block values.
- Favor simple variable substitution in strings (see [variable substitution docs](https://docs.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-string-substitutions?view=powershell-7.2)).
  - For simple variables in a string, use syntax like `"my name is $username"`. Do not use `"my name is $($username)"`.
  - For properties, use syntax like `"my name is $($user.name)"`.
  - Only use curly braces when the delineation is ambiguous (`"Updating all ${itemType}s"`) or you are using a named scope (`"my name is ${env:USERNAME}"`).
- Use full CmdLet names for better scripts.
  - Favor `$foobar.Where(...)` over `$foobar | Where-Object {...}` where possible to sidestep powershell pipeline gotchas like single value returns.
  - Favor `$foobar | Where-Object {} | ForEach-Object {}` over `$foobar | ? {} | % {}`.
  - Read https://devblogs.microsoft.com/scripting/best-practice-for-using-aliases-in-powershell-scripts/ for some general reasons to avoid using aliases in scripts but even sense then we now have more reasons because of the cross-platform differences that pwsh brings. The more we depend on powershell scripts we need to be aware of ways to make them work on all our OS's. Of course we are using pwsh which runs on all OS's but default alias on the different OS's may change. [Two examples we hit](https://github.com/Azure/azure-sdk-tools/commit/0301c21a37de6e429f80cdb78a5f0adf41fafd12) while trying to run scripts on linux and windows:
    - `mkdir` - On Windows this is an alias on top of New-Item and will happily create any missing directories in the directory hierarchy, while on linux this is the shell command and will need to be passed `-p` in order to create the missing directories. In order to make this work consistently use the cmdlet on all OS's call `New-Item -ItemType Directory` instead. 
    - `echo` - This one isn't related to powershell it is more related to the script task where on windows echo cannot use quotes but on linux it requires quotes around the string. It is just another example where we need to be careful when writing steps we expect to work across OS's and in this case it might be better to use pwsh given we can make it consistent across OS's.
- Analyze your code via an editor extension or on the command line with `Invoke-ScriptAnalyzer <script path>`.

## Testing

Powershell scripts should be testable, via one or more methods:

- Unit testing business logic.
- Dry running scripts locally or in pipeline contexts.
- Local testing various scenarios involving external systems safely.
- Pipeline testing various scenarios involving external systems safely.

### Unit Testing

Unit tests should be written for all scripts, and should utilize [Pester](https://pester.dev/).

- Tests can be located alongside scripts in a directory called `tests`.
- Example pester test suites: [job matrix tests](https://github.com/Azure/azure-sdk-tools/tree/main/eng/common/scripts/job-matrix/tests), [asset sync tests](https://github.com/Azure/azure-sdk-tools/blob/main/tools/assets-automation/asset-sync/assets.Tests.ps1)
- A CI pipeline should be defined to run scripts unit tests at the very least. See [archetype-sdk-tool-pwsh](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/templates/stages/archetype-sdk-tool-pwsh.yml) for how to do this.
- Script code should always be written so as much of the surface area as possible can be run via unit tests. Move code that calls out to external dependencies into modular functions, and simplify context/data structures passed to functions as much as possible to it can be easily mocked.

#### Running Pester Tests

(stolen from https://github.com/Azure/azure-sdk-tools/blob/main/tools/assets-automation/asset-sync/contributing.md).

> **First, ensure you have `pester` installed:**
>
> `Install-Module Pester -Force`
> 
> Then invoke tests with:
> 
> `Invoke-Pester ./assets.Tests.ps1`
> 
> **See stdout output**
> 
> `Invoke-Pester <other arguments> -PassThru`
> 
> **Select a subset**
> 
> To select a subset of tests, invoke using the `FullNameFilter` argument. EG
> 
> `Invoke-Pester ./assets.Tests.ps1 -FullNameFilter="*Evaluate-Target-Dir*"`
> 
> The "full-name" is simply the full namespace including parent "context" names.
> 
> ```powershell
> Describe "AssetsModuleTests" {
>   Context "Evaluate-Target-Dir" {
>     It "Should evaluate a root directory properly." {
>     }
>   }
> }
> ```
> 
> Full evaluated test name is going to be:
> 
> `AssetsModuleTests.Evaluate-Target-Dir.Should evaluate a root directory properly.`

### Local/Pipeline Functional Testing

Some functionality relies so heavily on external process or service calls that unit tests do not provide a good return on investment. For these cases, we should still favor being able to run and validate the code as easily as possible.

Some tips for writing testable scripts:

- Avoid referencing implicit values that require environmental setup. For example, if a script processes an azure pipeline artifact, don't hardcode the standard azure pipeline artifact location, but rather add a parameter with that location as the default value.
- Structure large scripts into multiple functions and/or files. This way you can test functions with smaller pieces of logic in isolation.
  - A useful pattern to enable testing like this is to have two scripts: one "wrapper script" to define command line parameters and do any high level transformations, and another "library script" to contain all the functions and a main entrypoint function that the wrapper script can call. In order to test logic in isolation from the library script, support dot sourcing:
    - Add this block to the bottom of your library script.
      ```
      if ($MyInvocation.InvocationName -ne ".") {
        FindStressPackages $searchDirectory $filters
      }
      ```
    - Dot source the script directly from the command line:
      ```
      . <my library script.ps1>
      ```
    - Call the function under test directly, rather than trying to exercise it through calls to the wrapper script.
    - **IMPORTANT NOTE**: Never add the `InvocationName` block to a script that may be called from an azure pipeline. Some devops tasks like the `AzureCLI` task will call scripts with an `InvocationName` of `"."` which causes them to noop.

Some things to try and avoid:

- Do not write scripts that can only be run in pipeline contexts, unless the script code is written specifically for the pipeline, e.g. setting azure pipeline variables.
