# Python SDK Requirements

For Python requirements:
- Requirements should be installed into a virtual environment. 
- Detect or ask the user for the virtual environment they want to check for. Create one if none exist.
- Ensure the venv is activated when running all commands.

## Required Checks

| Requirement | Check Command | Min Version | Purpose | Auto Install | Installation Instructions |
|-------------|---------------|-------------|---------|--------------|--------------------------|
| azpysdk | `azpysdk --help` | - | Python SDK validation tool | true | `cd <azure-sdk-for-python-root> && python -m pip install eng/tools/azure-sdk-tools[build]` (activate venv first) |
| sdk_generator | `sdk_generator --help` | - | SDK code generation | true | `cd <azure-sdk-for-python-root> && python -m pip install eng/tools/azure-sdk-tools[sdk_generator]` (activate venv first) |
| GitPython | `pip show GitPython` | - | Git integration tools | true | `cd <azure-sdk-for-python-root> && python -m pip install eng/tools/azure-sdk-tools[ghtools]` (activate venv first) |
| pytest | `pytest --version` | 8.3.5 | Testing framework | true | `cd <azure-sdk-for-python-root> && python -m pip install pytest` (activate venv first) |

