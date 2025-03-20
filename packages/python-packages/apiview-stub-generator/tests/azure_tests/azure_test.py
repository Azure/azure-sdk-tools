# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import os
import sys
import tempfile
import shutil
import requests
from subprocess import check_call, run, PIPE
from pytest import fail, mark

from apistub import ApiView, TokenKind, StubGenerator

SDK_PARAMS = [
    ("azure-core", "1.32.0", "core", "azure.core"),
    #("azure-ai-documentintelligence", "1.0.1", "documentintelligence", "azure.ai.documentintelligence"),
    #("corehttp", "1.0.0b5", "corehttp", "core", "corehttp"),
    #("azure-eventhub-checkpointstoreblob", "1.2.0", "eventhub", "azure.eventhub.extensions.checkpointstoreblob")
]
SDK_IDS = [f"{pkg_name}_{version}" for pkg_name, version, _, _ in SDK_PARAMS]

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

def _get_pypi_files(temp_dir, package_name, version):
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
    
    wheel_url = None
    tar_gz_url = None
    
    for file_info in data['urls']:
        if file_info['packagetype'] == 'bdist_wheel':
            wheel_url = file_info['url']
        elif file_info['packagetype'] == 'sdist':
            tar_gz_url = file_info['url']
    
    if not wheel_url or not tar_gz_url:
        raise ValueError("Could not find both wheel and tar.gz files for the specified version.")
    
    wheel_path = _download_file(temp_dir, wheel_url)
    tar_gz_path = _download_file(temp_dir, tar_gz_url)
    
    return wheel_path, tar_gz_path


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

    def _dependency_installed(self, dep):
        result = run([sys.executable, "-m", "pip", "show", dep], stdout=PIPE, stderr=PIPE, text=True)
        # return code 1 means the package is not installed
        return result.returncode == 0

    def _uninstall_dep(self, dep):
        if self._dependency_installed(dep):
            check_call([sys.executable, "-m", "pip", "uninstall", "-y", dep])
            try:
                for module in list(sys.modules):
                    if module.startswith(dep):
                        del sys.modules[module]
            except KeyError:
                pass
        assert not self._dependency_installed(dep)
    
    def _download_packages(self, directory, package_name, version):
        temp_dir = tempfile.mkdtemp()
        temp_path = os.path.join(temp_dir, package_name)
        # copy src to tmp/tmp**/azure-*
        src_path = _get_src(temp_path, directory, package_name, version)
        # copy whl and sdist files to tmp/tmp**
        whl_path, sdist_path = _get_pypi_files(temp_dir, package_name, version)
        print(f"Source directory copied to: {src_path}")
        print(f"Wheel file downloaded to: {whl_path}")
        print(f"Tar.gz file downloaded to: {sdist_path}")
        return src_path, whl_path, sdist_path

    @mark.parametrize("pkg_name,version,directory,pkg_namespace", SDK_PARAMS, ids=SDK_IDS)
    def test_sdks(self, pkg_name, version, directory, pkg_namespace):
        src_path, whl_path, sdist_path = self._download_packages(directory, pkg_name, version)
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=src_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)

        assert apiview.package_name == pkg_name
        assert apiview.namespace == pkg_namespace
