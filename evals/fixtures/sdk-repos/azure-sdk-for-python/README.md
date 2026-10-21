# Azure SDK for Python

Minimal stand-in for a local `Azure/azure-sdk-for-python` clone.

Local SDK-generation eval scenarios need a target repo to generate into —
without one the agent reasonably refuses to call `azsdk_package_generate_code`
and goes looking for a clone instead. Only the root marker and the `sdk/`
directory layout matter; the mock MCP server returns canned success responses,
so no real package source is required.

Packages live under `sdk/<service-directory>/<package-name>/`.
