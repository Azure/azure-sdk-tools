# Test-Proxy Startup Scripts

The scripts contained in this directory are intended for two reasons:

1. To be run within CI as a step prior to invoking tests.
2. To be run automatically as part of the test pipelines.

They are intended to align with the needs of the pipeline or test suite that is invoking it. This is why the same script is implemented multiple times in multiple languages.

The core algo should be shared across them, but interactions with the local environment will be adjusted for each tech stack.

These scripts are intended to work on a system that has a `docker` version >= `1.13.0`.