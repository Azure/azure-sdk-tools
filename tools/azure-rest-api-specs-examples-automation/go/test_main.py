import unittest
from main import parse_original_file


class TestMain(unittest.TestCase):

    def test_parse_original_file(self):
        expected_original_file = 'specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_List.json'

        original_file = parse_original_file('https://github.com/Azure/azure-rest-api-specs/tree/main/' + expected_original_file)
        self.assertEqual(expected_original_file, original_file)

        original_file = parse_original_file('https://github.com/Azure/azure-rest-api-specs/tree/some_branch/' + expected_original_file)
        self.assertEqual(expected_original_file, original_file)

        original_file = parse_original_file('https://local/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_List.json')
        self.assertIsNone(original_file)

        original_file = parse_original_file(expected_original_file)
        self.assertEqual(expected_original_file, original_file)
