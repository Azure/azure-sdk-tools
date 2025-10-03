#!/bin/bash

# Integration test using a real Azure SDK package that has apiview-properties.json

echo "Integration test with real Azure SDK package..."

# Clone azure-sdk-for-python to get a package with apiview-properties.json
TEMP_DIR="/tmp/azure-sdk-integration-test-$(date +%s)"
mkdir -p "$TEMP_DIR"

echo "Cloning azure-sdk-for-python repository..."
git clone --depth 1 https://github.com/Azure/azure-sdk-for-python.git "$TEMP_DIR/azure-sdk-for-python"

# Look for packages that have apiview-properties.json
echo "Finding packages with apiview-properties.json..."
find "$TEMP_DIR/azure-sdk-for-python/sdk" -name "apiview-properties.json" -type f | head -5

echo ""
echo "To test with a real package, run:"
echo "pwsh eng/scripts/Create-Apiview-Token-Python.ps1 -SourcePath '<path-to-sdk-package>' -OutPath '/tmp/real-test-output.json'"

echo ""
echo "For example, if azure-core has apiview-properties.json:"
echo "pwsh eng/scripts/Create-Apiview-Token-Python.ps1 -SourcePath '$TEMP_DIR/azure-sdk-for-python/sdk/core/azure-core' -OutPath '/tmp/azure-core-test.json'"
