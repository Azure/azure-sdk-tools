import unittest
import tempfile
import json
from os import path, makedirs, remove

from examples_dir import resolve_example_path, try_find_resource_manager_example


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


def create_mock_unified_test_folder(metadata_file="metadata.json") -> tempfile.TemporaryDirectory:
    temporary = tempfile.TemporaryDirectory()
    tsp_dir = "specification/compute/resource-manager/Microsoft.Compute/Compute"
    files = {
        "sdk/tsp-location.yaml": f"directory: {tsp_dir}\n",
        f"sdk/{metadata_file}": json.dumps(
            {"apiVersions": {"Compute": "2026-04-01", "ComputeDisk": "2026-03-02"}}
        ),
        f"specs/{tsp_dir}/Compute/examples/VirtualMachines.yaml": (
            "$namespace: Compute\nVirtualMachines.get:\n"
            "  - id: virtualMachineExamples/Foo\n    request: {}\n    responses: {}\n"
        ),
        f"specs/{tsp_dir}/stable/2026-04-01/examples/virtualMachineExamples/Foo.json": "{}",
    }
    try:
        for relative_path, content in files.items():
            filename = path.join(temporary.name, relative_path)
            makedirs(path.dirname(filename), exist_ok=True)
            with open(filename, "w", encoding="utf-8") as fout:
                fout.write(content)
    except Exception:
        temporary.cleanup()
        raise
    return temporary


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

    def test_find_swagger_without_authored_typespec_json(self):
        with create_mock_test_folder() as tmp_dir_name:
            remove(
                path.join(
                    tmp_dir_name,
                    "azure-rest-api-specs/specification/mongocluster/DocumentDB.MongoCluster.Management/"
                    "examples/2024-07-01/MongoClusters_ListConnectionStrings.json",
                )
            )
            resolved = resolve_example_path(
                path.join(tmp_dir_name, "azure-rest-api-specs"),
                path.join(tmp_dir_name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster"),
                "2024-07-01/MongoClusters_ListConnectionStrings.json",
            )
            self.assertEqual(
                (
                    "specification/mongocluster/resource-manager/Microsoft.DocumentDB/preview/"
                    "2024-07-01/examples",
                    "MongoClusters_ListConnectionStrings.json",
                ),
                resolved,
            )

    def test_missing_typespec_and_swagger_json(self):
        with create_mock_test_folder() as tmp_dir_name:
            with self.assertRaisesRegex(RuntimeError, "Example file not found"):
                resolve_example_path(
                    path.join(tmp_dir_name, "azure-rest-api-specs"),
                    path.join(tmp_dir_name, "azure-sdk-for-java/sdk/mongocluster/azure-resourcemanager-mongocluster"),
                    "2024-07-01/Missing.json",
                )

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


class TestUnifiedExamples(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.specs = path.join(temporary.name, "specs")
        self.sdk = path.join(temporary.name, "sdk")
        self.tsp_dir = "specification/schemaregistry/data-plane/SchemaRegistry"

    def write(self, root, relative_path, content):
        filename = path.join(root, relative_path)
        makedirs(path.dirname(filename), exist_ok=True)
        with open(filename, "w", encoding="utf-8") as fout:
            fout.write(content)
        return filename

    def configure(self, versions, metadata_file="metadata.json"):
        self.write(self.sdk, "tsp-location.yaml", f"directory: {self.tsp_dir}\n")
        return self.write(self.sdk, metadata_file, json.dumps({"apiVersions": versions}))

    def write_swagger_example(self, version, relative_file, prefix=""):
        relative = f"{self.tsp_dir}/{prefix}stable/{version}/examples/{relative_file}"
        self.write(self.specs, relative, "{}")
        return relative.rsplit("/", 1)

    def test_schema_registry_uses_metadata_version_without_authored_json(self):
        self.write(
            self.specs,
            f"{self.tsp_dir}/examples/SchemaRegistry.yaml",
            "$namespace: SchemaRegistry\nGetSchemaById:\n"
            "  - id: GetSchemaById\n    request: {}\n    responses: {}\n",
        )
        expected = {}
        for version in ("2021-10", "2022-10", "2023-07-01"):
            expected[version] = self.write_swagger_example(version, "GetSchemaById.json")

        for version in expected:
            with self.subTest(version=version):
                self.configure({"SchemaRegistry": version})
                self.assertEqual(
                    tuple(expected[version]),
                    resolve_example_path(
                        self.specs, self.sdk, "examples/SchemaRegistry.yaml#GetSchemaById"
                    ),
                )

    def test_metadata_locations(self):
        self.write(self.specs, f"{self.tsp_dir}/examples.yaml", "$namespace: SchemaRegistry\n")
        expected = self.write_swagger_example("2023-07-01", "GetSchemaById.json")
        for metadata_file in (
            "metadata.json",
            "_metadata.json",
            "testdata/_metadata.json",
            "src/main/resources/META-INF/azure-resourcemanager-test_metadata.json",
        ):
            with self.subTest(metadata_file=metadata_file):
                filename = self.configure({"SchemaRegistry": "2023-07-01"}, metadata_file)
                try:
                    self.assertEqual(
                        tuple(expected),
                        resolve_example_path(self.specs, self.sdk, "examples.yaml#GetSchemaById"),
                    )
                finally:
                    remove(filename)

    def test_compute_service_prefixes_select_independent_versions_and_preserve_nested_id(self):
        self.tsp_dir = "specification/compute/resource-manager/Microsoft.Compute/Compute"
        versions = {"Contoso.Compute": "2026-04-01", "Contoso.ComputeDisk": "2026-03-02"}
        self.configure(versions)
        for service, version in versions.items():
            folder = service.split(".")[-1]
            self.write(
                self.specs,
                f"{self.tsp_dir}/{folder}/examples/VirtualMachines.yaml",
                f"$namespace: {service}\n",
            )
            expected = self.write_swagger_example(version, "virtualMachineExamples/Foo.json")
            for reference in (
                f"{folder}/examples/VirtualMachines.yaml#virtualMachineExamples/Foo",
                f"{folder}\\examples\\VirtualMachines.yaml#virtualMachineExamples\\Foo",
            ):
                with self.subTest(reference=reference):
                    self.assertEqual(
                        tuple(expected), resolve_example_path(self.specs, self.sdk, reference)
                    )

    def test_service_local_swagger_does_not_match_another_subservice(self):
        self.tsp_dir = "specification/compute/resource-manager/Microsoft.Compute/Compute"
        self.configure({"Compute": "2026-04-01", "ComputeDisk": "2026-04-01"})
        self.write(self.specs, f"{self.tsp_dir}/Compute/examples.yaml", "$namespace: Compute\n")
        expected = self.write_swagger_example(
            "2026-04-01", "virtualMachineExamples/Foo.json", "Compute/"
        )
        self.write_swagger_example("2026-04-01", "virtualMachineExamples/Foo.json", "ComputeDisk/")
        self.assertEqual(
            tuple(expected),
            resolve_example_path(
                self.specs, self.sdk, "Compute/examples.yaml#virtualMachineExamples/Foo"
            ),
        )
        remove(path.join(self.specs, *expected))
        with self.assertRaisesRegex(RuntimeError, "Swagger example not found"):
            resolve_example_path(
                self.specs, self.sdk, "Compute/examples.yaml#virtualMachineExamples/Foo"
            )

    def test_missing_namespace_uses_unique_service_prefix_or_single_metadata_entry(self):
        for source in ("examples.yaml", "SchemaRegistry/examples.yaml"):
            with self.subTest(source=source):
                self.configure({"Contoso.SchemaRegistry": "2023-07-01"})
                self.write(self.specs, f"{self.tsp_dir}/{source}", "{}")
                expected = self.write_swagger_example("2023-07-01", "GetSchemaById.json")
                self.assertEqual(
                    tuple(expected),
                    resolve_example_path(self.specs, self.sdk, source + "#GetSchemaById"),
                )

    def test_reports_ambiguous_service_instead_of_guessing_version(self):
        self.configure({"Compute": "2026-04-01", "ComputeDisk": "2026-03-02"})
        self.write(self.specs, f"{self.tsp_dir}/examples.yaml", "{}")
        with self.assertRaisesRegex(RuntimeError, "Cannot uniquely match"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")

    def test_reports_ambiguous_swagger_matches(self):
        self.configure({"SchemaRegistry": "2023-07-01"})
        self.write(self.specs, f"{self.tsp_dir}/examples.yaml", "$namespace: SchemaRegistry\n")
        self.write_swagger_example("2023-07-01", "Foo.json", "first/")
        self.write_swagger_example("2023-07-01", "Foo.json", "second/")
        with self.assertRaisesRegex(RuntimeError, "Ambiguous Swagger example"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")

    def test_missing_metadata_or_swagger_reports_failure(self):
        self.write(self.sdk, "tsp-location.yaml", f"directory: {self.tsp_dir}\n")
        self.write(self.specs, f"{self.tsp_dir}/examples.yaml", "$namespace: SchemaRegistry\n")
        with self.assertRaisesRegex(RuntimeError, "API-version metadata not found"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")
        self.configure({"SchemaRegistry": "2023-07-01"})
        with self.assertRaisesRegex(RuntimeError, "Swagger example not found"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")

    def test_namespace_mismatch_or_conflicting_metadata_reports_failure(self):
        self.configure({"AnotherService": "2023-07-01"})
        self.write(self.specs, f"{self.tsp_dir}/examples.yaml", "$namespace: SchemaRegistry\n")
        with self.assertRaisesRegex(RuntimeError, "Cannot uniquely match"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")
        self.configure({"SchemaRegistry": "2023-07-01"})
        self.configure({"SchemaRegistry": "2022-10"}, "_metadata.json")
        with self.assertRaisesRegex(RuntimeError, "Conflicting API versions"):
            resolve_example_path(self.specs, self.sdk, "examples.yaml#Foo")

    def test_rejects_missing_or_unsafe_id(self):
        for reference in (
            "examples.yaml",
            "examples.yaml#",
            "examples.yaml#../Foo",
            "examples.yaml#/Foo",
            "examples.yaml#C:/Foo",
            "../examples.yaml#Foo",
        ):
            with self.subTest(reference=reference):
                with self.assertRaises(RuntimeError):
                    resolve_example_path(self.specs, self.sdk, reference)
