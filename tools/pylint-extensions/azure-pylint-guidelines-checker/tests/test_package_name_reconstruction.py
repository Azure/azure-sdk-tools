# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

import os
import tempfile
import astroid
import pytest
import pylint_guidelines_checker as checker


class TestPackageNameReconstruction:
    """Test that package names are correctly reconstructed when __init__.py files are missing."""

    def test_get_full_package_name_with_complete_path(self):
        """Test that the function returns the original name when it's already complete."""
        sample_code = "def test_func(): pass"
        module = astroid.parse(sample_code, module_name="azure.core.test_module")
        
        class MockNode:
            def root(self):
                return module
        
        node = MockNode()
        result = checker.get_full_package_name(node)
        assert result == "azure.core.test_module"

    def test_get_full_package_name_with_file_path(self):
        """Test that the function reconstructs package name from file path when module name is incomplete."""
        with tempfile.TemporaryDirectory() as temp_dir:
            # Create directory structure without __init__.py files (simulating wheel package)
            azure_dir = os.path.join(temp_dir, "azure", "core")
            os.makedirs(azure_dir, exist_ok=True)
            test_file = os.path.join(azure_dir, "test_module.py")
            
            sample_code = "def test_func(): pass"
            with open(test_file, 'w') as f:
                f.write(sample_code)
            
            # Simulate what happens when pylint processes a file from a wheel
            module = astroid.parse(sample_code, module_name="test_module", path=test_file)
            
            class MockNode:
                def root(self):
                    return module
            
            node = MockNode()
            result = checker.get_full_package_name(node)
            
            # Should reconstruct the full package name from file path
            assert result == "azure.core.test_module"

    def test_get_full_package_name_fallback_to_original(self):
        """Test that the function falls back to original name when no file path is available."""
        sample_code = "def test_func(): pass"
        module = astroid.parse(sample_code, module_name="test_module")
        
        class MockNode:
            def root(self):
                return module
        
        node = MockNode()
        result = checker.get_full_package_name(node)
        
        # Should fallback to original module name
        assert result == "test_module"

    def test_get_full_package_name_with_non_azure_package(self):
        """Test that the function handles non-azure packages correctly."""
        with tempfile.TemporaryDirectory() as temp_dir:
            # Create directory structure for non-azure package
            other_dir = os.path.join(temp_dir, "some", "other", "package")
            os.makedirs(other_dir, exist_ok=True)
            test_file = os.path.join(other_dir, "test_module.py")
            
            sample_code = "def test_func(): pass"
            with open(test_file, 'w') as f:
                f.write(sample_code)
            
            module = astroid.parse(sample_code, module_name="test_module", path=test_file)
            
            class MockNode:
                def root(self):
                    return module
            
            node = MockNode()
            result = checker.get_full_package_name(node)
            
            # Should fallback to original module name for non-azure packages
            assert result == "test_module"

    def test_azure_core_detection_with_reconstructed_name(self):
        """Test that azure.core detection works with reconstructed package names."""
        with tempfile.TemporaryDirectory() as temp_dir:
            # Create azure.core structure without __init__.py
            azure_core_dir = os.path.join(temp_dir, "azure", "core", "pipeline", "transport")
            os.makedirs(azure_core_dir, exist_ok=True)
            test_file = os.path.join(azure_core_dir, "test_module.py")
            
            sample_code = """
from some_module import SomeClass
"""
            with open(test_file, 'w') as f:
                f.write(sample_code)
            
            # Parse the module as if it came from a wheel (incomplete module name)
            module = astroid.parse(sample_code, module_name="test_module", path=test_file)
            import_node = module.body[0]  # The import statement
            
            # Test that our function correctly identifies this as an azure.core module
            full_name = checker.get_full_package_name(import_node)
            assert full_name == "azure.core.pipeline.transport.test_module"
            assert full_name.startswith("azure.core")

    def test_edge_cases(self):
        """Test edge cases and error handling."""
        # Test with node that doesn't have root method
        class BadNode:
            pass
        
        result = checker.get_full_package_name(BadNode())
        assert result == ""
        
        # Test with node that has root but root raises exception
        class ErrorNode:
            def root(self):
                raise Exception("Error")
        
        result = checker.get_full_package_name(ErrorNode())
        assert result == ""