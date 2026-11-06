import unittest
import sys
from os import path
from unittest.mock import patch
import main

sys.path.append(path.abspath("../directory"))
from test_examples_dir import create_mock_unified_test_folder


class TestPython(unittest.TestCase):

    def test_unified_example_preserves_nested_output_path(self):
        with create_mock_unified_test_folder("_metadata.json") as temporary:
            with patch.multiple(
                main,
                specs_path=path.join(temporary, "specs"),
                sdk_package_path=path.join(temporary, "sdk"),
                create=True,
            ):
                example = main.parse_python_example(
                    [
                        "# x-ms-original-file: Compute/examples/VirtualMachines.yaml#virtualMachineExamples/Foo\n",
                        'print("sample")\n',
                    ]
                )
            self.assertEqual("Foo", example.target_filename)
            self.assertEqual(
                "specification/compute/resource-manager/Microsoft.Compute/Compute/"
                "stable/2026-04-01/examples-python/virtualMachineExamples",
                example.target_dir,
            )
            self.assertEqual('print("sample")\n', example.content)
