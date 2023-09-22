# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import argparse
import sys
import os
import logging
import glob
import shutil
import tarfile
import re

from subprocess import check_call
from zipfile import ZipFile
from io import open


from azure.storage.blob import BlobServiceClient

# Currently, the docs.ms python feeds do not support pulling data from:
#   1) SDISTS
#   2) Non tar.gz files
# In ADDITION there is a new discovered bug as well
#   - Essentially, if a __unit__.py STARTS with a comment instead of the pkg_resources namespace extension, docs.ms does not render properly
#     To resolve this, this script will also strip comments from __init__.py files before rezipping the package
# This script is intended to take in a comma separated list of packages in the form of
#   azure-core==1.0.0,azure-storage-blob==12.0.0b5
# The intent is to download the SDIST version of this package, unzip it, rezip it as tar.gz, 
# and upload to a storage account.
# This script takes a dependency on azure-storage-blob 12.0.
# 

logging.getLogger().setLevel(logging.INFO)

DESTINATION_CONTAINER = "generation"
# https://scbeddscratch.blob.core.windows.net/generation/azure-appconfiguration-1.0.0b5.tar.gz
URI_CONSTRUCTOR = "{primary_endpoint}{container}/{name}"

def get_targets(argument):
    return [p.strip() for p in argument.split(",")]


def clean_dir(target_dir):
    if os.path.exists(target_dir):
        shutil.rmtree(target_dir)
    os.mkdir(target_dir)


def prep_env(working_directories):
    for working_dir in working_directories:
        clean_dir(working_dir)


def make_tarfile(output_filename, source_dir):
    with tarfile.open(output_filename, "w:gz") as tar:
        tar.add(source_dir, arcname=os.path.basename(source_dir))


def download_package(specifier, download_dir):
    logging.info("Downloading {}".format(specifier))

    check_call(
        [
            "pip",
            "download",
            "-d",
            download_dir,
            "--no-deps",
            "--no-binary=:all:",
            specifier,
        ]
    )


def strip_comments_from_inits(start_dir):
    init_files = []

    for folder, subfolders, files in os.walk(start_dir): 
        for file in files:
            if file == "__init__.py":
                init_files.append(os.path.join(folder, file))

    for located_init in init_files:
        with open(located_init, 'r') as f:
            lines = f.readlines()

        # in case rstrip is necessary re.sub(r'[\s]*\#\stype\:\signore[\s]*', "", line).rstrip()
        lines = [line.replace('"pkgutil"', "'pkgutil'").rstrip() for line in lines if not line.strip().startswith('#')]

        with open(located_init, 'w') as f:
            f.write("".join(lines))


def repackage_data(download_dir, unzip_directory, upload_directory):
    for sdist in os.listdir(download_dir):
        full_path = os.path.join(download_dir, sdist)
        pkg_full_name = os.path.splitext(sdist)[0]
        tar_name = "{}.tar.gz".format(pkg_full_name)
        tar_location = os.path.join(upload_directory, tar_name)
        tar_source = os.path.join(unzip_directory, pkg_full_name)

        with ZipFile(full_path, 'r') as zipObj:
           # Extract all the contents of zip file in current directory
           zipObj.extractall(unzip_directory)

        strip_comments_from_inits(tar_source)

        make_tarfile(tar_location, tar_source)

        clean_dir(unzip_directory)


def upload_data(upload_directory, blob_container_client, endpoint):
    resulting_uris = []

    for targz in os.listdir(upload_directory):
        logging.info("Uploading {} to blob storage.".format(targz))
        full_path = os.path.join(upload_directory, targz)

        with open(full_path, "rb") as data:
            blob_container_client.upload_blob(targz, data, overwrite=True)

        resulting_uris.append(URI_CONSTRUCTOR.format(primary_endpoint=endpoint,container=DESTINATION_CONTAINER, name=targz))

    return resulting_uris


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Takes a list of packages, downloads the sdists, rezips as tar.gz, and uploads to a target blob storage."
    )

    parser.add_argument(
        "-d",
        "--destination",
        dest="working_folder",
        help="The folder where work will be executed.",
        required=True,
    )

    parser.add_argument(
        "-c",
        "--connectionString",
        dest="connection_string",
        help="The folder where work will be executed.",
        required=True,
    )

    parser.add_argument(
        "-t",
        "--targetPackages",
        dest="target_package_list",
        help="List of the following form: 'azure-core==1.0.0,azure-storage-blob==12.0.0b5'",
        required=True,
    )

    args = parser.parse_args()

    all_packages = get_targets(args.target_package_list)
    working_directory = os.path.abspath(args.working_folder)
    download_dir = os.path.join(working_directory, "download")
    unzip_directory = os.path.join(working_directory, "unzip")
    upload_directory = os.path.join(working_directory, "upload")

    logging.info("Targeted Packages: {}".format(all_packages))
    logging.info("Targeted Working Directory: {}".format(working_directory))

    logging.info("Prepping Working Environment")
    prep_env([download_dir, unzip_directory, upload_directory])

    # download the sdist format
    for specifier in all_packages:
        download_package(specifier, download_dir)
    
    # unzip, tar
    repackage_data(download_dir, unzip_directory, upload_directory)

    # instantiate blob client and upload data
    service = BlobServiceClient.from_connection_string(conn_str=args.connection_string)
    container_client = service.get_container_client(DESTINATION_CONTAINER)
    results = upload_data(upload_directory, container_client, service.primary_endpoint)

    # output URI links for each blob
    logging.info("Uploaded {} sdists.".format(len(results)))
    for uri in results:
        print(uri)

