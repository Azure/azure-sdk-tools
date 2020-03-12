# Doc Warden [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/108?branchName=master)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=108&branchName=master)

Every CI build owned by the Azure-SDK team also needs to verify that the documentation within the target repo meets a set of standards. `Doc-warden` is intended to ease the _implementation_ of these checks in CI builds.

Features:

* Enforces Readme Standards
    - Readmes present
    - Readmes have appropriate content
* Enforces Changelog Standards
    - Changelogs Present
    - Changelogs contain entry and content for the latest package version
* Generates report for included observed packages

This package is tested on Python 2.7 -> 3.8.

## Prerequisites
This package is intended to be run as part of a pipeline within Azure DevOps. As such, [Python](https://www.python.org/downloads/) must be installed prior to attempting to install or use `Doc-Warden.` While `pip` comes pre-installed on most modern Python installs, if `pip` is an unrecognized command when attempting to install `warden`, run the following command **after** your Python installation is complete.

In addition, `warden` is distributed using `setuptools` and `wheel`, so those packages should also be present prior to install. 

```
/:> python -m ensurepip
/:> pip install setuptools wheel
```

## Usage

Right now, `warden` supports two main purposes. 1. Readme and Changelog enforcement (`scan`, `content`, `presence`), and 2. package indexing (`index`).  

### Example usage (for any of the above commands):

```

<pre-step, clone target repository>
...
/:> pip install setuptools wheel
/:> pip install doc-warden
...
<next task, because PATH doesn't update without another one>
/:> ward scan -d $(Build.SourcesDirectory)

```
**Notes for example above**

* Assumption is that the `.docsettings` file is placed at the root of the repository.

To provide a different path (like `azure-sdk-for-java` does...), use: 

```

/:> ward scan -d $(Build.SourcesDirectory) -c $(Build.SourcesDirectory)/eng/.docsettings.yml

```

##### Parameter Options

`command` 
Currently supports 3 commands. Values: `['scan', 'presence', 'content', `index`]` **Required.**

* `scan`
    * Run both `content` and `presence` enforcement on the targeted directory.
* `content`
    * Run only `content` enforcement on the target directory. Ensures that:
      - The content in each readme matches the regex patterns defined in the .docsettings file
      - Each changelog contains entry for the latest version.
* `presence` 
    * Run only `presence` enforcement on the target directory. Ensures readmes and changelogs exist where they should.
* `index`
    * Take inventory of the target folder. Attempts to leverage selected docsettings to discover all packages within the directory, and generate a `packages.md` index file.

`--scan-directory`
The target directory `warden` should be scanning. **Required.**

`--scan-language`
`warden` checks for packages by _convention_, so it needs to understand what language it is looking at. This must be populated either in `.docsettings file` or by parameter. **Required.**

`--config-location`
By default, `warden` looks for the `.docsettings` file in the root of the repository. However, populating this location will override this behavior and instead pull the file from the location in this parameter. **Optional.**

`--pipeline-stage`
The stage of the pipeline. can be `pr`, `ci`, or `release`. **Optional.**

`--target`
Specify what file to run enforcement on `readme` or `changelog`. Used when running `content` or `presence` verification only. **Optional.**

`--package-output`
Override the default location that the generated `packages.md` file is dropped to during execution of the `index` command.

`--verbose-output`
Enable or disable output of an html report. Defaults to false. **Optional.**

##### Notes for Devops Usage

The `-d` argument should be `$(Build.SourcesDirectory)`. This will point `warden` at the repo that has been associated with CI.

## Methodology

### Enforcing Readme Presence 

When should we expect a readme and/or changelog to be present?

**Always:**

* At the root of the repo (Readme only)
* Associated with a `package` directory (Readme and Changelog)

#### .Net

A package directory is indicated by:

* a `*,csproj` file under the `sdk` directory
    * Note that this is just a proxy. `warden` attempts to omit test projects by convention.


#### Python

A package directory is indicated by: 

* the presence of a `setup.py` file

#### Java

A package directory is indicated by:

* the presence of a `pom.xml` file
    * The POM `<packaging>` value within is set to `JAR`

#### Node & JS

A package directory is indicated by: 

* The presence of a `package.json` file

### Enforcing Readme Content

`doc-warden` has the ability to check discovered readme files to ensure that a set of configured sections is present. How does it work? `doc-warden` will ensure that each regex defined in `required_readme_sections` matches against at least one section header in the readme. If all the patterns match at least one header, the readme will pass content verification.

Other Notes:

* A `section` header is any markdown or RST that will result in a `<h1>` to `<h2>` html tag.
* `warden` will content verify any `readme.rst` or `readme.md` file found outside the `omitted_paths` in the targeted repo.

### Enforcing Changelog Content
`doc-warden` checks the latest entry in the changelog file to make sure it matches the latest version of the package. It also checks to make sure that the entry is not empty. 


#### Control, the `.docsettings.yml` File, and You

Special cases often need to be configured. It seems logical that there needs be a central location (per repo) to override conventional settings. To that end, a new `.docsettings.yml` file will be added to each repo. 

```
<repo-root>
│   README.md
│   .docsettings.yml
│
└───.azure-pipelines
│   │   <build def>
│   
└───<other files and folders>
```

The presence of this file allows each repository to customize how enforcement takes place within their repo.

**Example DocSettings File for Java Repo**

```
omitted_paths:
  - archive/*
  - sdk/eventhub/
language: java
root_check_enabled: True
required_readme_sections:
  - "(Client Library for Azure .*|Microsoft Azure SDK for .*)"
  - Getting Started
known_presence_issues:
  - ['cognitiveservices/data-plane/language/bingspellcheck/README.md', '#2847']
  - ['cognitiveservices/data-plane/language/bingspellcheck/CHANGELOG.md', '#2847']
known_content_issues:
  - ['sdk/template/azure-sdk-template/README.md','#1368']
  - ['sdk/template/azure-sdk-template/CHANGELOG.md','#1368']
```
The above configuration tells `warden`...

- The language within the repo is `java`
- To ensure that a `README.md` is present at the root of the repository.
- To omit **any** paths under `archive/` from the readme checks.
- To omit paths found **directly** under `sdk/eventhub/`. 
   - This means that if there is a readme content issue under `sdk/eventhub/azure-messaging/`, it will still throw an error!

Possible values for `language` right now are `['net', 'java', 'js', 'python']`. Greater than one target language is not currently supported.

##### `required_readme_sections` Configuration
This section instructs `warden` to verify that there is at least one matching section title for each provided `section` pattern in any discovered readme. Regex is fully supported.

The two items listed from the example `.docsettings` file will:
- Match a header matched by a simple regex expression
- Match a header exactly titled "Getting Started"

Note that the regex is surrounded by quotation marks where the regex will break `yml` parsing of the configuration file.

##### `known_presence_issues` and `known_content_issues` Configuration
`doc-warden` is designed to crash builds if it detects failures. However, the vast majority of the time, these issues cannot be fixed immediately. In the above configuration, there are two paths highlighted as known issues. 

The first, `known_presence_issues`, tells warden that a presence failure detected at the specified paths _should be ignored_ and should not result in a crashed build. A `tuple` describing each known issue specifies both what the known issue is, as well as some sort of justification. Having an exception with an issueId attached is a good justification for not failing the build.

> We're aware of this issue, and it is tracked in the following github issue.

The `known_content_issues` parameter functions _identically_ to the `known_presence_issues` check. If a readme is listed as "already known" to have failures, the entire CI build will not be crashed by Warden.

##### `package_indexing_exclusion_list` and `package_indexing_traversal_stops` Configuration
Indexing packages is often done as part of nightly (or triggered) automation. With this being the case, sometimes `warden` may detect a PackageId that users wish to omit from the generated `packages.md` file. The Azure SDK team leverages 
the `package_indexing_exclusion_list` array members to enable just this sort of scenario.

`package_indexing_traversal_stops` is used during parse of .NET language repos _only_. This is due to how the discovery logic for readme and changelog is implemented for .NET projects. Specifically, readmes for a .csproj are often a couple directories up from their parent .csproj location!

For .net, `warden` will traverse **up** one directory at a time, looking for the readme and changelog files in each traversed directory. `warden` will continue to traverse until...

1. It discovers a folder with a `.sln` within it
2. It encounters a folder that exactly matches one present in `package_indexing_traversal_stops`

Note that `warden` will not even execute an index against a .NET repo _unless the traversal stops are set_. 

[SDK for net .docsettings](https://github.com/Azure/azure-sdk-for-net/blob/master/eng/.docsettings.yml) is a great example for both the exclusion list as well as the traversal stops.

## Provide Feedback

If you encounter any bugs or have suggestions, please file an issue [here](https://github.com/Azure/azure-sdk-tools/issues) and assign to `scbedd`.
