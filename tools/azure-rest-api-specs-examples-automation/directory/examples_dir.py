from os import path
import yaml
import glob
import re


def try_find_resource_manager_example(specs_path: str, example_dir: str, example_filename: str, module_path: str) -> str:
    if '/resource-manager/' not in example_dir:
        # find the corresponding example file under {specs_path}/specification/{service}/resource-manager
        try:
            tsp_location_path = path.join(module_path, "tsp-location.yaml")
            if path.exists(tsp_location_path) and path.isfile(tsp_location_path):
                # load tsp_dir from tsp-location.yaml
                # e.g. tsp_dir = "specification/mongocluster/DocumentDB.MongoCluster.Management"
                with open(tsp_location_path, 'r') as fin:
                    yaml_json = yaml.safe_load(fin)
                tsp_dir = yaml_json['directory']

                if tsp_dir:
                    # find example under directory
                    # e.g. example_path = "specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-03-01-preview/MongoClusters_ListConnectionStrings.json"
                    example_paths = glob.glob(f'{path.join(specs_path, tsp_dir)}/**/{example_filename}', recursive=True)

                    if len(example_paths) > 0:
                        example_path = example_paths[0]
                        example_path = path.relpath(example_path, specs_path).replace('\\', '/')

                        example_dir = path.dirname(example_path)

                        match = re.match(r'specification/([^/]+)/.*/examples/([^/]+)(.*)', example_dir)
                        if match:
                            # example: specification/mongocluster/DocumentDB.MongoCluster.Management/examples/2024-03-01-preview
                            # service: mongocluster
                            # api_version: 2024-03-01-preview
                            # additional_path: <empty>

                            service = match.group(1)
                            api_version = match.group(2)
                            additional_path = match.group(3)

                            glob_resource_manager_filename = f'specification/{service}/resource-manager/**/{api_version}/examples{additional_path}/{example_filename}'
                            candidate_resource_manager_filename = glob.glob(path.join(specs_path, glob_resource_manager_filename),
                                                                            recursive=True)
                            if len(candidate_resource_manager_filename) > 0:
                                example_path, _ = path.split(candidate_resource_manager_filename[0])
                                example_dir = path.relpath(example_path, specs_path).replace('\\', '/')
        except NameError:
            # specs_path not defined
            pass

    return example_dir
