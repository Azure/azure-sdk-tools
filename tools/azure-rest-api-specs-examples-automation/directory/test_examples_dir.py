import unittest
import tempfile
from os import path, makedirs

from examples_dir import try_find_resource_manager_example


tsp_location_file_content = """directory: specification/mongocluster/DocumentDB.MongoCluster.Management
commit: 07bdede4651ce2ea0e4039d76e81a69df23a3d6e
repo: Azure/azure-rest-api-specs
additionalDirectories: null
"""

json_example_file_content = """{
  "operationId": "MongoClusters_ListConnectionStrings",
  "title": "List the available connection strings for the Mongo Cluster resource.",
  "parameters": {
    "subscriptionId": "ffffffff-ffff-ffff-ffff-ffffffffffff",
    "resourceGroupName": "TestGroup",
    "mongoClusterName": "myMongoCluster",
    "api-version": "2024-07-01"
  },
  "responses": {
    "200": {
      "body": {
        "connectionStrings": [
          {
            "connectionString": "mongodb+srv://<user>:<password>@myMongoCluster.mongocluster.cosmos.azure.com",
            "description": "default connection string"
          }
        ]
      }
    }
  }
}
"""


def create_mock_test_folder() -> tempfile.TemporaryDirectory:
    tmp_path = path.abspath(".")
    tmp_dir = tempfile.TemporaryDirectory(dir=tmp_path)
    try:
        sdk_path = path.join(tmp_dir.name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster")
        makedirs(sdk_path)
        with open(path.join(sdk_path, "tsp-location.yaml"), "w+", encoding="utf-8") as file:
            file.write(tsp_location_file_content)

        # api-version 2024-03-01-preview
        specs_path = path.join(
            tmp_dir.name,
            "azure-rest-api-specs/specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-03-01-preview",
        )
        makedirs(specs_path)
        with open(path.join(specs_path, "MongoClusters_ListConnectionStrings.json"), "w+", encoding="utf-8") as file:
            file.write(json_example_file_content)

        specs_path = path.join(
            tmp_dir.name,
            "azure-rest-api-specs/specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
        )
        makedirs(specs_path)
        with open(path.join(specs_path, "MongoClusters_ListConnectionStrings.json"), "w+", encoding="utf-8") as file:
            file.write(json_example_file_content)

        # api-version 2024-07-01
        specs_path = path.join(
            tmp_dir.name,
            "azure-rest-api-specs/specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-07-01",
        )
        makedirs(specs_path)
        with open(path.join(specs_path, "MongoClusters_ListConnectionStrings.json"), "w+", encoding="utf-8") as file:
            file.write(json_example_file_content)

        specs_path = path.join(
            tmp_dir.name,
            "azure-rest-api-specs/specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-07-01/examples",
        )
        makedirs(specs_path)
        with open(path.join(specs_path, "MongoClusters_ListConnectionStrings.json"), "w+", encoding="utf-8") as file:
            file.write(json_example_file_content)
    except Exception as error:
        tmp_dir.cleanup()
        raise error

    return tmp_dir


class TestExamplesDir(unittest.TestCase):

    def test_find_resource_manager_example_typespec(self):
        with create_mock_test_folder() as tmp_dir_name:
            example_dir = try_find_resource_manager_example(
                path.join(tmp_dir_name, "azure-rest-api-specs"),
                path.join(tmp_dir_name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster"),
                "2024-07-01",
                "MongoClusters_ListConnectionStrings.json",
            )

            self.assertEqual(
                "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-07-01/examples",
                example_dir,
            )

    def test_find_resource_manager_example_typespec_no_tsp_location(self):
        mock_path = path.abspath(".")
        with self.assertRaises(RuntimeError):
            example_dir = try_find_resource_manager_example(
                path.join(mock_path, "azure-rest-api-specs"),
                path.join(mock_path, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster"),
                "2024-07-01",
                "MongoClusters_ListConnectionStrings.json",
            )

    def test_find_resource_manager_example_typespec_windows_directory_separator(self):
        with create_mock_test_folder() as tmp_dir_name:
            # use windows directory separator
            tsp_location_path = path.join(
                tmp_dir_name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster/tsp-location.yaml"
            )
            with open(tsp_location_path, "w+", encoding="utf-8") as file:
                file.write(
                    """directory: specification\mongocluster\DocumentDB.MongoCluster.Management
commit: 07bdede4651ce2ea0e4039d76e81a69df23a3d6e
repo: Azure/azure-rest-api-specs
additionalDirectories: null
"""
                )

            example_dir = try_find_resource_manager_example(
                path.join(tmp_dir_name, "azure-rest-api-specs"),
                path.join(tmp_dir_name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster"),
                "2024-07-01",
                "MongoClusters_ListConnectionStrings.json",
            )

            self.assertEqual(
                "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-07-01/examples",
                example_dir,
            )

    def test_find_resource_manager_example_swagger(self):
        example_dir = try_find_resource_manager_example(
            "not_used",
            "not_used",
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            "MongoClusters_ListConnectionStrings.json",
        )

        self.assertEqual(
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            example_dir,
        )

    def test_find_resource_manager_example_swagger_invalid_path(self):
        with self.assertRaises(RuntimeError):
            example_dir = try_find_resource_manager_example(
                "not_used",
                "not_used",
                "D:/w/Azure/azure-rest-api-specs/specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
                "MongoClusters_ListConnectionStrings.json",
            )
