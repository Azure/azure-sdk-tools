from os import path
import yaml
import glob
import re


def try_find_resource_manager_example(
    specs_path: str, sdk_package_path: str, example_dir: str, example_filename: str
) -> str:
    if "/resource-manager/" not in example_dir:
        # find the corresponding example file under {specs_path}/specification/{service}/resource-manager
        tsp_location_path = path.join(sdk_package_path, "tsp-location.yaml")
        if path.exists(tsp_location_path) and path.isfile(tsp_location_path):
            # load tsp_dir from tsp-location.yaml
            # e.g. tsp_dir = "specification/mongocluster/DocumentDB.MongoCluster.Management"
            with open(tsp_location_path, "r") as fin:
                yaml_json = yaml.safe_load(fin)
            tsp_dir = yaml_json["directory"]

            if tsp_dir:
                tsp_dir = tsp_dir.replace("\\", "/")

                # find example under directory
                # e.g. example_path = "specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-03-01-preview/MongoClusters_ListConnectionStrings.json"
                example_search_glob = f"{path.join(specs_path, tsp_dir)}/**/examples/{example_dir}/{example_filename}"
                example_paths = glob.glob(example_search_glob, recursive=True)

                if len(example_paths) > 0:
                    example_path = example_paths[0]
                    example_path = path.relpath(example_path, specs_path).replace("\\", "/")

                    example_dir = path.dirname(example_path)

                    match = re.match(r"specification/([^/]+)/.*/examples/([^/]+)(.*)", example_dir)
                    if match:
                        # example: specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-03-01-preview
                        # service: mongocluster
                        # api_version: 2024-03-01-preview
                        # additional_path: <empty>

                        service = match.group(1)
                        api_version = match.group(2)
                        additional_path = match.group(3)

                        glob_resource_manager_filename = f"specification/{service}/resource-manager/**/{api_version}/examples{additional_path}/{example_filename}"
                        candidate_resource_manager_filename = glob.glob(
                            path.join(specs_path, glob_resource_manager_filename), recursive=True
                        )
                        if len(candidate_resource_manager_filename) > 0:
                            example_path, _ = path.split(candidate_resource_manager_filename[0])
                            example_dir = path.relpath(example_path, specs_path).replace("\\", "/")
                else:
                    raise RuntimeError(f"Example file not found at path {example_search_glob}")
            else:
                raise RuntimeError("'directory' property not found in tsp-location.yaml")
        else:
            raise RuntimeError(f"tsp-location.yaml not found in SDK package folder {sdk_package_path}")

    example_dir = example_dir.replace("\\", "/")
    if re.search(r"[:\"*?<>|]+", example_dir):
        # invalid character in windows path < > : " | ? *
        raise RuntimeError(f"Example directory contains invalid character {example_dir}")

    return example_dir
