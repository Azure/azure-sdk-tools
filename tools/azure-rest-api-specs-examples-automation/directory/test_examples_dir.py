import unittest

from examples_dir import try_find_resource_manager_example


class TestExamplesDir(unittest.TestCase):

    @unittest.skip(
        'require call "_set_paths" with your local azure-rest-api-specs repository and azure-sdk-for-java repository'
    )
    def test_find_resource_manager_example_typespec(self):
        example_dir = try_find_resource_manager_example(
            "c:/github/azure-rest-api-specs",
            "2024-03-01-preview",
            "MongoClusters_ListConnectionStrings.json",
            "c:/github/azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster",
        )

        self.assertEqual(
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            example_dir,
        )

    def test_find_resource_manager_example_swagger(self):
        example_dir = try_find_resource_manager_example(
            "c:/github/azure-rest-api-specs",
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            "MongoClusters_ListConnectionStrings.json",
            "c:/github/azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster",
        )

        self.assertEqual(
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            example_dir,
        )
