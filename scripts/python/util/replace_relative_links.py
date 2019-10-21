import argparse
import sys
import os
import logging
import glob
import fnmatch

logging.getLogger().setLevel(logging.INFO)

RELATIVE_LINK_REPLACEMENT_SYNTAX = "https://github.com/{repo_id}/tree/{build_sha}/{target_resource_path}"

def locate_readmes(directory):
    return [os.path.join(directory, obj) for obj in os.listdir(directory) if obj.lower() == "readme.md"]
    
if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Replaces relative links for README.md. Given any discovered relative link, will replace with the provided repoId and SHA."
    )

    parser.add_argument(
        "-t",
        "--target",
        dest="target_folder",
        help="The target folder that contains a README ",
        # required=True,

        default="C:/repo/sdk-for-python/sdk/core/azure-core"
    )

    parser.add_argument(
        "-i",
        "--repoid",
        dest="repo_id",
        help="The target repository used as the base for the path replacement. Full Id, example: \"Azure/azure-sdk-for-net\"",
        # required=True,

        default="Azure/azure-sdk-for-python"
    )

    parser.add_argument(
        "-r",
        "--root",
        dest="root_folder",
        help="The root directory of the repository. This gives us the ability to rationalize links in situations where a relative link traverses UPWARDS from the readme.",
        # required=True,

        default="C:/repo/sdk-for-python"
        )

    parser.add_argument(
        "-s",
        "--sha",
        dest="build_sha",
        help="The commit hash associated with this change. Using this will mean that links will never be broken.",
        # required=True,

        default="82785eb5aaecd0d135adc8657d54ca1d5d6a2f9b"
    )

    args = parser.parse_args()

    readme_files = locate_readmes(args.target_folder)

    for readme in readme_files:
        with open(readme) as readme_stream:
            readme_content = readme_stream.read()

        print(readme_content)

    exit(1)
