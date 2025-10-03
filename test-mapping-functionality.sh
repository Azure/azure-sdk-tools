#!/bin/bash

# Test script for Create-Apiview-Token-Python.ps1

echo "Testing PowerShell script with and without mapping file..."

# Create test directories
TEST_DIR="/tmp/apiview-test-$(date +%s)"
mkdir -p "$TEST_DIR/with-mapping"
mkdir -p "$TEST_DIR/without-mapping"

# Create a dummy Python package structure for testing
echo "Creating test package structure..."

# Package without mapping file
cat > "$TEST_DIR/without-mapping/setup.py" << EOF
from setuptools import setup
setup(name='test-package', version='1.0.0')
EOF

# Package with mapping file
cat > "$TEST_DIR/with-mapping/setup.py" << EOF
from setuptools import setup
setup(name='test-package-with-mapping', version='1.0.0')
EOF

cat > "$TEST_DIR/with-mapping/apiview-properties.json" << EOF
{
    "packageName": "test-package-with-mapping",
    "packageVersion": "1.0.0"
}
EOF

echo "Test directories created:"
echo "- Without mapping: $TEST_DIR/without-mapping"
echo "- With mapping: $TEST_DIR/with-mapping"

echo ""
echo "To test the PowerShell script, run:"
echo "pwsh eng/scripts/Create-Apiview-Token-Python.ps1 -SourcePath '$TEST_DIR/without-mapping' -OutPath '/tmp/output1.json'"
echo "pwsh eng/scripts/Create-Apiview-Token-Python.ps1 -SourcePath '$TEST_DIR/with-mapping' -OutPath '/tmp/output2.json'"

echo ""
echo "Expected behavior:"
echo "1. First command should NOT include --mapping-path"
echo "2. Second command SHOULD include --mapping-path"
