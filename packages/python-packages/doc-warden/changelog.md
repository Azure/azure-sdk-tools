# Release History

## 0.5.4
- Handle crashing setup.py in Python Index Packages implementation. 

## 0.5.3
- Update namespace reference to pathlib2

## 0.5.2
- Update package requirement to pathlib2

## 0.5.1
- Allow omission of individual folders, while still allowing issues BELOW that folder to be detected.

## 0.5.0
- Added support for verifying changelogs in addition to readmes.

## 0.4.2
- Update logic for discovering java client and data packages

## 0.4.1
- Added `all` option to `--target` argument.
- Default to running operations on readmes. When `--target` is set to `changelog` run operations on changelogs, when set to `all` run operations on both readmes and changlogs

## 0.4.0
- Extended `presence` and `content` functionality to also operate on CHANGELOG.md's
- Added `--target` argument to control if to the operations run on changelogs or readmes.
- Extended `known_presence_issues` and `known_content_issues` to function for CHANGELOG.md's
- Added `--pipeline-stage` argument which allows different functions to be run at different stages of the pipeline. 
  e.g. verifying the presence of release notes is only enforced when `--pipeline-stage` is set to `release`.

## 0.3.0 (2019-04-30)

New functionality for indexing packages within a repository has been added. Leverage with the `index` positional argument.


```
ward index <target directory>

```

Additional Details:

 * Can successfully statically index Python, .NET, Java, or Javascript repositories and locate `packages`
 * Generates a `packages.md` file at the root of each repo
    * This location can be adjusted.

Check the [readme](readme.md) for more explicit usage details.

Other Changes:

 * Reorganization between `enforce_readme_present.py` and `warden_common.py`

## 0.2.6 (2019-04-10)

Versions 0 -> 0.2.6 have all been various iterations on `base warden functionality`.

Release `0.2.6` is purely adding a `changelog.md` file, and ensuring that the file updates show up properly in a release note.

However, up till now, the following features have been added:

* Configuration File Parsing 
* Readme Presence Verification
* Readme Content Verification
* Support for exception and scan exclusion lists 
