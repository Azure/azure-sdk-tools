# Test-Proxy Startup Scripts

The scripts contained in this directory are intended for two reasons:

- To serve as examples for necessary steps to take within each language's shim.
   1. Once part of the shim: to be run within CI as a step prior to invoking tests.
   2. Once part of the shim: To be run automatically as part of the test pipelines.

This is why the same script is implemented multiple times in multiple languages.

The core algo should be shared across them, but interactions with the local environment will be adjusted for each tech stack. There is no reason for these scripts to stick around here, as they should become part of the `shim` for each individual language.

These scripts are intended to work on a system that has a `docker` version >= `1.13.0`.

The scripts as written provide a simple interface. Run the script in a mode of `start` or `stop` and provide a `targetPath` that corresponds to the **root of your repo**.

Example invocations as-is:

- JS
  - `node ./start-server.js start "C:/repo/sdk-for-js"`
- Python
  - `python ./start-server.py --mode="start" --target_folder="C:/repo/sdk-for-python"`
- Powershell
  - `./start-server.ps1 -mode start -targetFolder "C:/repo/sdk-for-net"`
