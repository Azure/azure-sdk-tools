# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import os
import tempfile
import shutil
import requests
from subprocess import check_call
from pytest import fail, mark

from apistub import ApiView, StubGenerator
import json

SDK_PARAMS = [
    ("azure-ai-documentintelligence", "1.0.1", "documentintelligence", "azure.ai.documentintelligence", "src"),
    ("azure-ai-documentintelligence", "1.0.1", "documentintelligence", "azure.ai.documentintelligence", "whl"),
    ("azure-ai-documentintelligence", "1.0.1", "documentintelligence", "azure.ai.documentintelligence", "sdist"),
    ("corehttp", "1.0.0b5", "core", "corehttp", "src"),
    ("corehttp", "1.0.0b5", "core", "corehttp", "whl"),
    ("corehttp", "1.0.0b5", "core", "corehttp", "sdist"),
    ("azure-eventhub-checkpointstoreblob", "1.2.0", "eventhub", "azure.eventhub.extensions.checkpointstoreblob", "src"),
    ("azure-eventhub-checkpointstoreblob", "1.2.0", "eventhub", "azure.eventhub.extensions.checkpointstoreblob", "whl"),
    ("azure-eventhub-checkpointstoreblob", "1.2.0", "eventhub", "azure.eventhub.extensions.checkpointstoreblob", "sdist"),
    #("azure-synapse-artifacts", "0.20.0", "synapse", "azure.synapse.artifacts")
]
SDK_IDS = [f"{pkg_name}_{version}[{pkg_type}]" for pkg_name, version, _, _, pkg_type in SDK_PARAMS]

def _copy_directory_from_github(dest_dir, repo_url, directory, tag=None):
    """
    Copy a directory from a GitHub repository to a local temporary folder.

    :param repo_url: URL of the GitHub repository
    :param directory: Directory within the repository to copy
    :param tag: Optional tag of the repository to clone
    :return: Path to the local temporary folder containing the copied directory
    """
    # Create a temporary directory
    temp_dir = tempfile.mkdtemp()
    
    # Clone the repository to the temporary directory
    clone_cmd = ["git", "clone", "--depth", "1"]
    if tag:
        clone_cmd.extend(["--branch", tag])
    clone_cmd.extend([repo_url, temp_dir])
    check_call(clone_cmd)
    
    # Path to the specific directory in the cloned repository
    src_dir = os.path.join(temp_dir, directory)
    
    # Create another temporary directory to copy the specific directory
    shutil.copytree(src_dir, dest_dir)
    
    # Clean up the cloned repository
    shutil.rmtree(temp_dir)

def _get_src(src_dir, subdirectory, package_name, version):

    # Example usage for copying directory from GitHub
    repo_url = "https://github.com/Azure/azure-sdk-for-python.git"
    directory = f"sdk/{subdirectory}/{package_name}"
    tag = f"{package_name}_{version}"  # Optional: specify a tag to clone
    
    _copy_directory_from_github(src_dir, repo_url, directory, tag)
    print(f"Directory copied to: {src_dir}")
    return src_dir

def _download_file(dest_folder, url):
    """
    Download a file from a URL to a destination folder.

    :param url: URL of the file to download
    :param dest_folder: Destination folder to save the downloaded file
    :return: Path to the downloaded file
    """
    local_filename = os.path.join(dest_folder, url.split('/')[-1])
    with requests.get(url, stream=True) as r:
        r.raise_for_status()
        with open(local_filename, 'wb') as f:
            for chunk in r.iter_content(chunk_size=8192):
                f.write(chunk)
    return local_filename

def _get_pypi_files(temp_dir, package_name, version, pkg_type):
    """
    Get the wheel and tar.gz files from PyPI for a specific version of a package.

    :param package_name: Name of the package
    :param version: Version of the package
    :return: Paths to the downloaded wheel and tar.gz files
    """
    pypi_url = f"https://pypi.org/pypi/{package_name}/{version}/json"
    response = requests.get(pypi_url)
    response.raise_for_status()
    data = response.json()
    
    url = None
    
    for file_info in data['urls']:
        if pkg_type == "whl" and file_info['packagetype'] == 'bdist_wheel':
            url = file_info['url']
            break
        elif pkg_type == "sdist" and file_info['packagetype'] == 'sdist':
            url = file_info['url']
            break
    
    if not url:
        raise ValueError(f"Could not find {pkg_type} file for the specified version.")
    
    pkg_path = _download_file(temp_dir, url)
    
    return pkg_path


class TestApiViewAzure:
    def _count_newlines(self, apiview: ApiView):
        newline_count = 0
        for line in apiview.review_lines[::-1]:
            if len(line.tokens) == 0:
                newline_count += 1
            else:
                break
        return newline_count

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_line_ids(self, apiview: ApiView):
        line_ids = set()

        def collect_line_ids(review_lines, index=0):
            for line in review_lines:
                # Ensure that there are no repeated definition IDs.
                if line.line_id and line.line_id in line_ids:
                    fail(f"Duplicate definition ID {line.line_id}.")
                    line_ids.add(line.line_id)
                # Recursively collect definition IDs from child lines
                if line.children:
                    collect_line_ids(line.children, index)

        collect_line_ids(apiview.review_lines)
    
    def _download_packages(self, directory, package_name, version, pkg_type):
        temp_dir = tempfile.mkdtemp()
        temp_path = os.path.join(temp_dir, package_name)
        # copy src to tmp/tmp**/azure-*
        if pkg_type == "src":
            src_path = _get_src(temp_path, directory, package_name, version)
            print(f"Source directory copied to: {src_path}")
            return src_path
        # copy whl and sdist files to tmp/tmp**
        pkg_path = _get_pypi_files(temp_dir, package_name, version, pkg_type)
        if pkg_type == "whl":
            print(f"Wheel file downloaded to: {pkg_path}")
        else:
            print(f"Tar.gz file downloaded to: {pkg_path}")
        return pkg_path

    def _diff_token_file(self, old_file, new_file):
        """
        Compare two token JSON files and return the differences.
        """
        with open(old_file, 'r') as f1, open(new_file, 'r') as f2:
            old_tokens = json.load(f1)
            new_tokens = json.load(f2)

            # Replace "ParserVersion" value with "x.x.x"
            old_tokens["ParserVersion"] = "x.x.x"
            new_tokens["ParserVersion"] = "x.x.x"
            # Replace the GLOBAL header value ("# Package is parsed using apiview-stub-generator(version:0.3.17), Python version: 3.10.12") which may differ.
            old_tokens["ReviewLines"][0]["Tokens"][0]["Value"] = "Package is parsed using apiview-stub-generator(version:x.x.x), Python version: x.x.x"
            new_tokens["ReviewLines"][0]["Tokens"][0]["Value"] = "Package is parsed using apiview-stub-generator(version:x.x.x), Python version: x.x.x"

            assert old_tokens == new_tokens, "Generated token file does not match the provided token file."

    def _write_tokens(self, stub_gen):
        apiview = stub_gen.generate_tokens()
        json_tokens = stub_gen.serialize(apiview)
        # Write to JSON file
        out_file_path = stub_gen.out_path
        # Generate JSON file name if outpath doesn't have json file name
        if not out_file_path.endswith(".json"):
            out_file_path = os.path.join(
                stub_gen.out_path, f"{apiview.package_name}_python.json"
            )
        with open(out_file_path, "w") as json_file:
            json_file.write(json_tokens)

        return apiview

    @mark.parametrize("pkg_name,version,directory,pkg_namespace,pkg_type", SDK_PARAMS, ids=SDK_IDS)
    def test_sdks(self, pkg_name, version, directory, pkg_namespace, pkg_type):
        print("Pip freeze before test")
        check_call(["pip", "freeze"])
        pkg_path = self._download_packages(directory, pkg_name, version, pkg_type)
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(
            pkg_path=pkg_path, temp_path=temp_path, out_path=temp_path,
        )
        apiview = self._write_tokens(stub_gen)
        self._validate_line_ids(apiview)

        assert apiview.package_name == pkg_name
        assert apiview.namespace == pkg_namespace

        # Compare the generated token file with the provided token file
        outfile = f"{pkg_name}_python.json"
        generated_token_file = os.path.join(temp_path, outfile)
        provided_token_file = os.path.abspath(os.path.join(os.path.dirname(__file__), f"token_files/{outfile}"))

        self._diff_token_file(provided_token_file, generated_token_file)
