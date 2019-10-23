# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import argparse
import sys
import os
import logging
import glob
import re
import fnmatch
from io import open

# This script is intended to be run against a single folder. All readme.md files (regardless of casing) will have the relative links
# updated with appropriate full reference links. This is a recursive update..

logging.getLogger().setLevel(logging.INFO)

RELATIVE_LINK_REPLACEMENT_SYNTAX = (
    "https://github.com/{repo_id}/tree/{build_sha}/{target_resource_path}"
)
LINK_DISCOVERY_REGEX = r"\[([^\]]*)\]\(([^)]*)\)"


def locate_readmes(directory):
    readme_set = []

    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.lower() == "readme.md":
                readme_set.append(os.path.join(root, file))
    return readme_set


def is_relative_link(link_value, readme_location):
    try:
        return os.path.isfile(
            os.path.join(os.path.dirname(readme_location), link_value)
        )
    except:
        return False


def replace_relative_link(match, readme_location, root_folder, build_sha, repo_id):
    link_path = match[2]

    if is_relative_link(link_path, readme_location):
        # if it is a relative reference, we need to find the path from the root of the repository
        resource_absolute_path = os.path.abspath(
            os.path.join(os.path.dirname(readme_location), link_path)
        )
        placement_from_root = os.path.relpath(resource_absolute_path, root_folder)

        updated_link = RELATIVE_LINK_REPLACEMENT_SYNTAX.format(
            repo_id=repo_id,
            build_sha=build_sha,
            target_resource_path=placement_from_root,
        ).replace("\\", "/")

        return "[{}]({})".format(match[1], updated_link)
    else:
        return match[0]


def transfer_content_to_absolute_references(
    root_folder, build_sha, repo_id, readme_location, content
):
    content = re.sub(
        LINK_DISCOVERY_REGEX,
        lambda match, readme_location=readme_location, root_folder=root_folder, build_sha=build_sha, repo_id=repo_id: replace_relative_link(
            match, readme_location, root_folder, build_sha, repo_id
        ),
        content,
    )

    return content


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Replaces relative links for any README.md under the target folder. Given any discovered relative link, will replace with the provided repoId and SHA. Case insensitive"
    )

    parser.add_argument(
        "-t",
        "--target",
        dest="target_folder",
        help="The target folder that contains a README ",
        default="${{ parameters.TargetFolder }}",
    )

    parser.add_argument(
        "-i",
        "--repoid",
        dest="repo_id",
        help='The target repository used as the base for the path replacement. Full Id, example: "Azure/azure-sdk-for-net"',
        default="${{ parameters.RootFolder }}",
    )

    parser.add_argument(
        "-r",
        "--root",
        dest="root_folder",
        help="The root directory of the repository. This gives us the ability to rationalize links in situations where a relative link traverses UPWARDS from the readme.",
        default="${{ parameters.BuildSHA }}",
    )

    parser.add_argument(
        "-s",
        "--sha",
        dest="build_sha",
        help="The commit hash associated with this change. Using this will mean that links will never be broken.",
        default="${{ parameters.RepoId }}",
    )

    args = parser.parse_args()

    readme_files = locate_readmes(args.target_folder)

    for readme_location in readme_files:
        try:
            logging.info(
                "Running Relative Link Replacement on {}.".format(readme_location)
            )

            with open(readme_location, "r", encoding="utf-8") as readme_stream:
                readme_content = readme_stream.read()

            new_content = transfer_content_to_absolute_references(
                args.root_folder,
                args.build_sha,
                args.repo_id,
                readme_location,
                readme_content,
            )

            with open(readme_location, "w") as readme_stream:
                readme_stream.write(new_content)
        except Exception as e:
            logging.error(e)
            exit(1)
