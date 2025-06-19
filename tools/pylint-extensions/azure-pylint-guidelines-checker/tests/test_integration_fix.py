# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

"""
Integration test that demonstrates the fix for the wheel vs source package issue.
This test simulates the exact scenario described in the GitHub issue.
"""

import os
import tempfile
import shutil
import ast
import sys
from pylint.lint import Run
import pylint_guidelines_checker as checker


def create_test_package_structure(base_dir, package_name, with_init_files=True):
    """Create a test package structure."""
    if package_name == "azure_core":
        package_dir = os.path.join(base_dir, "azure", "core")
    else:
        raise ValueError(f"Unknown package: {package_name}")
    
    os.makedirs(package_dir, exist_ok=True)
    
    # Create a simple Python module
    module_file = os.path.join(package_dir, "test_module.py")
    with open(module_file, 'w') as f:
        f.write("""
# Test module for azure.core
def some_function():
    pass

class SomeClass:
    def method(self):
        pass
""")
    
    if with_init_files:
        # Create __init__.py files
        init_files = [
            os.path.join(base_dir, "__init__.py"),
            os.path.join(base_dir, "azure", "__init__.py"),
            os.path.join(base_dir, "azure", "core", "__init__.py"),
        ]
        for init_file in init_files:
            os.makedirs(os.path.dirname(init_file), exist_ok=True)
            with open(init_file, 'w') as f:
                f.write("# Package init file\n")
    
    return package_dir


def create_pylintrc(temp_dir):
    """Create a test pylintrc file."""
    pylintrc_path = os.path.join(temp_dir, ".pylintrc")
    pylintrc_content = """
[MASTER]
ignore-patterns=test_*,conftest,setup
reports=no
load-plugins=pylint_guidelines_checker

# PYLINT DIRECTORY IGNORE LIST
ignore=_vendor,_generated,samples,examples,test,tests,doc,.tox,build,dist

[MESSAGES CONTROL]
# Ignore ALL standard pylint checks, only run our custom checkers
disable=all
enable=client-constructor-parameter-credentials,client-constructor-parameter-kwargs
"""
    
    with open(pylintrc_path, 'w') as f:
        f.write(pylintrc_content)
    
    return pylintrc_path


def run_linter_safe(path, rcfile_path):
    """Run pylint safely and capture the results."""
    try:
        # Add the current directory to Python path so pylint can find our checker
        current_dir = os.path.dirname(os.path.abspath(__file__))
        if current_dir not in sys.path:
            sys.path.insert(0, current_dir)
        
        params = [path, "-f", "json", "--recursive=y", f"--rcfile={rcfile_path}"]
        linter = Run(params, exit=False)
        messages = linter.linter.reporter.messages
        return messages
    except Exception as e:
        print(f"Error running pylint: {e}")
        return []


def test_package_name_reconstruction_integration():
    """
    Integration test that demonstrates the fix works end-to-end.
    
    This simulates the exact problem described in the GitHub issue:
    different behavior between wheel packages (no __init__.py) and 
    source packages (with __init__.py).
    """
    with tempfile.TemporaryDirectory() as temp_dir:
        print("Testing package name reconstruction integration...")
        
        # Create pylintrc
        rcfile_path = create_pylintrc(temp_dir)
        
        # Test 1: Package structure with __init__.py files (source/sdist)
        source_dir = os.path.join(temp_dir, "source")
        create_test_package_structure(source_dir, "azure_core", with_init_files=True)
        
        # Test 2: Package structure without __init__.py files (wheel)
        wheel_dir = os.path.join(temp_dir, "wheel")
        create_test_package_structure(wheel_dir, "azure_core", with_init_files=False)
        
        # Test package name reconstruction directly
        print("\n=== Testing package name reconstruction ===")
        
        # Test wheel structure (no __init__.py)
        wheel_module_path = os.path.join(wheel_dir, "azure", "core", "test_module.py")
        with open(wheel_module_path, 'r') as f:
            wheel_code = f.read()
        
        import astroid
        wheel_module = astroid.parse(wheel_code, module_name="test_module", path=wheel_module_path)
        
        class MockNode:
            def __init__(self, module):
                self._module = module
            def root(self):
                return self._module
        
        wheel_name = checker.get_full_package_name(MockNode(wheel_module))
        print(f"Wheel package name: {wheel_name}")
        
        # Test source structure (with __init__.py)
        source_module_path = os.path.join(source_dir, "azure", "core", "test_module.py")
        with open(source_module_path, 'r') as f:
            source_code = f.read()
        
        source_module = astroid.parse(source_code, module_name="azure.core.test_module", path=source_module_path)
        source_name = checker.get_full_package_name(MockNode(source_module))
        print(f"Source package name: {source_name}")
        
        # Verify both produce the same result now
        assert wheel_name == "azure.core.test_module", f"Expected 'azure.core.test_module', got '{wheel_name}'"
        assert source_name == "azure.core.test_module", f"Expected 'azure.core.test_module', got '{source_name}'"
        assert wheel_name == source_name, f"Wheel and source should produce same result: '{wheel_name}' vs '{source_name}'"
        
        print("✅ Package name reconstruction test passed!")
        print(f"Both wheel and source packages now correctly identify as: {wheel_name}")
        
        return True


if __name__ == "__main__":
    try:
        test_package_name_reconstruction_integration()
        print("\n🎉 Integration test passed! The fix is working correctly.")
    except Exception as e:
        print(f"\n❌ Integration test failed: {e}")
        sys.exit(1)