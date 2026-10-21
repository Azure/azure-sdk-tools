# Azure SDK for Python

Minimal, hermetic repository fixture for mock local SDK-generation evals, not a
full clone of `Azure/azure-sdk-for-python` or a buildable SDK implementation.

The eval environment copies this directory into `azure-sdk-for-python/` inside
the specs workspace, then initializes a separate Git repository with the official
Python SDK origin URL as an identity marker. This setup does not clone or contact
that remote. The nested repository keeps SDK repo checks distinct from the outer
`azure-rest-api-specs` repository.

The service README and minimal package metadata identify the target package at
`sdk/contosowidgetmanager/azure-contoso-widgetmanager/`. TypeSpec configuration
remains in the outer specs workspace. The mock MCP supplies tool responses; this
fixture contains no generated SDK source and does not support real generation,
builds, installs, or publication.
