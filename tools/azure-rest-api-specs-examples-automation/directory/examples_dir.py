from os import path
import glob
import re


def try_find_resource_manager_example(specs_path: str, example_dir: str, example_filename: str) -> str:
    if '/resource-manager/' not in example_dir:
        try:
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
