# this script uses the package pipgrip to evaluate the targeted packages and return the full list of dependencies
import argparse
import os
import shutil
import re
import json
import bs4
import requests
from typing import List, Set, Tuple
import subprocess

def get_file(url: str, dest_folder: str) -> None:
    """
    Downloads a targeted URL to a destination folder.
    """
    file_name = url.split("/")[-1].replace(" ", "_")
    file_name = file_name.split("#")[0]
    file_path = os.path.join(dest_folder, file_name)

    r = requests.get(url, stream=True)
    if r.status_code == 200:
        with open(file_path, "wb") as f:
            for chunk in r.iter_content(chunk_size=1024 * 8):
                if chunk:
                    f.write(chunk)
                    f.flush()
                    os.fsync(f.fileno())
    else:
        print('Download failed with status code "{}" and error "{}".'.format(r.status_code, r.text))


def get_pkg_tuple(package_specifier: str) -> Tuple[str, str]:
    """
    Takes a specifier of form "azure-core==1.20.0" and returns ("azure-core", "1.20.0")

    If no specific version is specified, the latter part of the tuple will be None.
    """
    result = re.split(r"[><=]", package_specifier)

    if len(result) == 3:
        package_name = result[0]
        package_version = result[2]

    elif len(result) == 2:
        package_name = result[0]
        package_version = ""

    return package_name, package_version


def get_pkg_files(package_specifier: str) -> List[str]:
    """
    Given a package specifier "azure-core==1.20.0", find all wheels and source distributions that match that version.

    Retrieves using the PyPI simple index. This will provide up-to-date CDN links to each download.
    """
    pkg_name, pkg_version = get_pkg_tuple(package_specifier)

    if not pkg_version:
        print("This script does not support a specifier-less target package. Please specify an exact version.")
        return []

    retrieval_string = "https://pypi.org/simple/{0}".format(pkg_name)
    result = requests.get(retrieval_string)
    soup = bs4.BeautifulSoup(result.text, "html.parser")

    all_links = soup.find_all("a")

    def check_link(input_str: str) -> bool:
        return (
            "{}-".format(pkg_version) in input_str
            or input_str.endswith("{}.tar.gz".format(pkg_version))
            or input_str.endswith("{}.zip".format(pkg_version))
        )

    return [link["href"] for link in all_links if check_link(link.text)]


def read_package_list(json_string: str) -> List[str]:
    """
    Gets the python representation of a json array string.
    """
    return json.loads(json_string)


def get_dependencies(package_specifier: str) -> List[str]:
    """
    Evaluates a presented specifier and retrieves the complete dependency web in the form of package specifiers.

    azure-core==1.20.0
    azure-storage-blob==12.3.0
    """

    try:
        output = subprocess.check_output(["pipgrip", package_specifier, "--json"])
    except Exception as f:
        error_encountered = True
        output = ""

    if output:
        json_output = json.loads(output)

        return [key + "==" + json_output[key] for key in json_output]
    else:
        print("Unable to get output from pipgrip invocation.")
        return []


def download_dependencies(targeted_packages: Set[str], target_folder: str) -> None:
    """
    Downloads all the wheel and source distribution files for a given set of targeted packages.

    An individual item will have the standard specifier form of "azure-core==1.20.0".
    """

    if not os.path.exists(target_folder):
        os.mkdir(target_folder)
    else:
        shutil.rmtree(target_folder)
        os.mkdir(target_folder)

    for targeted_package in targeted_packages:
        files = get_pkg_files(targeted_package)
        for file in files:
            get_file(file, target_folder)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Download wheels and source distributions (if possible) for a set of targeted specifiers."
    )

    parser.add_argument("target_packages", nargs="?", help=("An array of targeted specifiers, in a json string."))

    parser.add_argument(
        "-d",
        "--dest-dir",
        dest="dest_dir",
        help="Temporary Location of downloaded bits.",
    )

    args = parser.parse_args()
    
    # powershell does some weird escaping. Leaving behind just the '`' in what would normally look like '`"`.
    pkg_list = read_package_list(args.target_packages.replace("`", "\""))

    assembled_package_list = set()

    for pkg in pkg_list:
        full_dependencies = get_dependencies(pkg)

        for dep in full_dependencies:
            assembled_package_list.add(dep)

    download_dependencies(assembled_package_list, args.dest_dir)
