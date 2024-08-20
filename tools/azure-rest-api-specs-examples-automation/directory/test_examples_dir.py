import unittest
import tempfile
from os import path, makedirs

from examples_dir import try_find_resource_manager_example


def create_mock_test_folder() -> tempfile.TemporaryDirectory:
    tsp_location_file_content = """directory: specification/mongocluster/DocumentDB.MongoCluster.Management
commit: 7ed015e3dd1b8b1b0e71c9b5e6b6c5ccb8968b3a
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
    "api-version": "2024-03-01-preview"
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

    tmp_path = path.abspath(".")
    tmp_dir = tempfile.TemporaryDirectory(dir=tmp_path)
    try:
        sdk_path = path.join(tmp_dir.name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster")
        makedirs(sdk_path)
        with open(path.join(sdk_path, "tsp-location.yaml"), "w+", encoding="utf-8") as file:
            file.write(tsp_location_file_content)

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
                "2024-03-01-preview",
                "MongoClusters_ListConnectionStrings.json",
            )

            self.assertEqual(
                "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
                example_dir,
            )

    def test_find_resource_manager_example_swagger(self):
        example_dir = try_find_resource_manager_example(
            "c:/github/azure-rest-api-specs",
            "c:/github/azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster",
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            "MongoClusters_ListConnectionStrings.json",
        )

        self.assertEqual(
            "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/2024-03-01-preview/examples",
            example_dir,
        )
