# Assets Maintenance Tooling

The solution contained in this directory is made up of two tools surfaced through a single command line API.


- `azure.sdk.tools.assets.scantool`
  - Used in combination with an input configuration to identify _all_ references to the assets repo.
- `azure.sdk.tools.assets.backuptool`
  - Used to backup individual tags to a storage medium.
- `azure.sdk.tools.assets.cleanuptool`
  - Used to delete tags that are not referenced from a `main` commit after a certain period.
- `azure.sdk.tools.assets.maintainencetool`
  - Commandline interop with `backuptool` as well as `cleanuptool`.

## How does this tool scan our repos?

![example processing layout](processing_layout.png)