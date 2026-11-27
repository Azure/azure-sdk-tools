import unittest
import sys
from os import path
from unittest.mock import patch
import main
from main import parse_original_file

sys.path.append(path.abspath("../directory"))
from test_examples_dir import create_mock_unified_test_folder


class TestMain(unittest.TestCase):

    def test_unified_example_preserves_nested_output_path(self):
        code = """package sample
// Generated from example definition: Compute/examples/VirtualMachines.yaml#virtualMachineExamples/Foo
func Example() {
}
"""
        with create_mock_unified_test_folder("testdata/_metadata.json") as temporary:
            filename = path.join(temporary, "sample.go")
            with open(filename, "w", encoding="utf-8") as fout:
                fout.write(code)
            with patch.multiple(
                main,
                specs_path=path.join(temporary, "specs"),
                sdk_package_path=path.join(temporary, "sdk"),
                create=True,
            ):
                examples = main.process_go_example(filename)
            self.assertEqual(1, len(examples))
            self.assertEqual("Foo", examples[0].target_filename)
            self.assertEqual(
                "specification/compute/resource-manager/Microsoft.Compute/Compute/"
                "stable/2026-04-01/examples-go/virtualMachineExamples",
                examples[0].target_dir,
            )

    def test_parse_original_file(self):
        expected_original_file = "specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_List.json"

        original_file = parse_original_file(
            "https://github.com/Azure/azure-rest-api-specs/tree/main/" + expected_original_file
        )
        self.assertEqual(expected_original_file, original_file)

        original_file = parse_original_file(
            "https://github.com/Azure/azure-rest-api-specs/tree/some_branch/" + expected_original_file
        )
        self.assertEqual(expected_original_file, original_file)

        original_file = parse_original_file(
            "https://local/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_List.json"
        )
        self.assertIsNone(original_file)

        original_file = parse_original_file(expected_original_file)
        self.assertEqual(expected_original_file, original_file)
