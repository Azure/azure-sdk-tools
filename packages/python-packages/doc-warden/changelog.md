# Release History
## 0.7.2
- Add and `--repo-root` argument. This allows scoping of the run to any directory in the repo as long as all entries in the config file are relative to this directory.

## 0.7.1
- Fixed an issue where `doc-warden` was handling the code fence blocks improperly. This issue caused verson `0.7.0` to throw errors when it SHOULDN'T have been.

## 0.7.0
- Fixed an issue where `doc-warden` ignored the very first h1 element. This is due to a BOM not being handled properly. Dropped support for py2.7.

## 0.6.1
- `Pygments` style code-fence blocks are parsed badly by `markdown2`. This results in phantom headers that break document hierarchy. Remove these blocks right after reading the readme and before passing to html parsing.

## 0.6.0 
- Support for `sub-heading` verification. Consumption of `sub-heading` yml will crash on less than version `0.6.0`.

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
