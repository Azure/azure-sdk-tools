# 2019-04-30 - 0.3.0

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

# 2019-04-10 - 0.2.6

Versions 0 -> 0.2.6 have all been various iterations on `base warden functionality`.

Release `0.2.6` is purely adding a `changelog.md` file, and ensuring that the file updates show up properly in a release note.

However, up till now, the following features have been added:

* Configuration File Parsing 
* Readme Presence Verification
* Readme Content Verification
* Support for exception and scan exclusion lists
