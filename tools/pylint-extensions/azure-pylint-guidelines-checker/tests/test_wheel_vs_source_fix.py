# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

import os
import tempfile
import astroid
import pytest
import pylint_guidelines_checker as checker


class TestWheelVsSourcePackageFix:
    """Test that reproduces and validates the fix for the wheel vs source package issue."""

    def test_azure_core_detection_in_wheel_structure(self):
        """
        Test that azure.core packages are correctly detected even when __init__.py files are missing.
        
        This reproduces the exact issue described in the GitHub issue where wheel packages
        (without __init__.py files) would not be correctly identified as azure.core packages,
        causing checkers to fail to apply ignores correctly.
        """
        with tempfile.TemporaryDirectory() as temp_dir:
            # Simulate wheel package structure (no __init__.py files)
            wheel_structure = os.path.join(temp_dir, "wheel_package")
            azure_transport_dir = os.path.join(wheel_structure, "azure", "core", "pipeline", "transport")
            os.makedirs(azure_transport_dir, exist_ok=True)
            
            # Create a test module in azure.core.pipeline.transport
            transport_module_path = os.path.join(azure_transport_dir, "test_transport.py")
            transport_code = """
from some_http_lib import HttpTransport
"""
            with open(transport_module_path, 'w') as f:
                f.write(transport_code)
            
            # Parse as if it came from a wheel (module name would be incomplete)
            transport_module = astroid.parse(transport_code, module_name="test_transport", path=transport_module_path)
            import_node = transport_module.body[0]
            
            # Test that our function correctly reconstructs the full package name
            full_name = checker.get_full_package_name(import_node)
            assert full_name == "azure.core.pipeline.transport.test_transport"
            assert full_name.startswith("azure.core.pipeline.transport")
            
            # Before the fix, this would have been just "test_transport"
            # After the fix, it should be the full package name

    def test_azure_storage_detection_in_wheel_structure(self):
        """Test that non-azure.core modules are correctly detected."""
        with tempfile.TemporaryDirectory() as temp_dir:
            # Simulate a regular SDK package structure
            sdk_structure = os.path.join(temp_dir, "sdk_package")
            azure_storage_dir = os.path.join(sdk_structure, "azure", "storage", "blob")
            os.makedirs(azure_storage_dir, exist_ok=True)
            
            # Create a test module
            storage_module_path = os.path.join(azure_storage_dir, "test_storage.py")
            storage_code = """
from azure.core.pipeline.transport import HttpResponse
"""
            with open(storage_module_path, 'w') as f:
                f.write(storage_code)
            
            # Parse as if it came from a wheel
            storage_module = astroid.parse(storage_code, module_name="test_storage", path=storage_module_path)
            import_node = storage_module.body[0]
            
            # Test that our function correctly reconstructs the package name
            full_name = checker.get_full_package_name(import_node)
            assert full_name == "azure.storage.blob.test_storage"
            assert not full_name.startswith("azure.core")
            assert full_name.startswith("azure.storage")

    def test_azure_mgmt_core_detection(self):
        """Test that azure.mgmt.core modules are also correctly detected."""
        with tempfile.TemporaryDirectory() as temp_dir:
            mgmt_structure = os.path.join(temp_dir, "mgmt_package")
            azure_mgmt_dir = os.path.join(mgmt_structure, "azure", "mgmt", "core", "test_module")
            os.makedirs(azure_mgmt_dir, exist_ok=True)
            
            mgmt_module_path = os.path.join(azure_mgmt_dir, "test_mgmt.py")
            mgmt_code = """
from azure.core.pipeline.transport import HttpResponse
"""
            with open(mgmt_module_path, 'w') as f:
                f.write(mgmt_code)
            
            mgmt_module = astroid.parse(mgmt_code, module_name="test_mgmt", path=mgmt_module_path)
            import_node = mgmt_module.body[0]
            
            # Test that our function correctly reconstructs the package name
            full_name = checker.get_full_package_name(import_node)
            assert full_name == "azure.mgmt.core.test_module.test_mgmt"
            assert full_name.startswith("azure.mgmt.core")

    def test_comparison_wheel_vs_source_behavior(self):
        """
        Test that demonstrates the fix - wheel and source packages now behave the same.
        
        This test simulates the exact scenario from the GitHub issue description.
        """
        with tempfile.TemporaryDirectory() as temp_dir:
            base_path = os.path.join(temp_dir, "azure", "core")
            os.makedirs(base_path, exist_ok=True)
            
            test_file = os.path.join(base_path, "test_module.py")
            sample_code = "def some_function(): pass"
            
            with open(test_file, 'w') as f:
                f.write(sample_code)
            
            # Simulate wheel behavior (no __init__.py, incomplete module name)
            wheel_module = astroid.parse(sample_code, module_name="test_module", path=test_file)
            
            # Simulate source behavior (with __init__.py, complete module name)
            source_module = astroid.parse(sample_code, module_name="azure.core.test_module", path=test_file)
            
            class MockNode:
                def __init__(self, module):
                    self._module = module
                def root(self):
                    return self._module
            
            # Get package names using our fixed function
            wheel_name = checker.get_full_package_name(MockNode(wheel_module))
            source_name = checker.get_full_package_name(MockNode(source_module))
            
            # Both should now produce the same result
            assert wheel_name == "azure.core.test_module"
            assert source_name == "azure.core.test_module"
            assert wheel_name == source_name
            
            # Before the fix, wheel_name would have been just "test_module"
            # After the fix, both should be "azure.core.test_module"