#!/usr/bin/python3

"""
python_version_check.py
Validate that the version file matches the CHANGELOG.
"""

import glob
import json
import logging
import os
import re
import sys


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))


if __name__ == '__main__':
    warning_color = '\033[91m'
    end_color = '\033[0m'

    stub_gen_path = os.path.join(ROOT, 'packages', 'python-packages', 'apiview-stub-generator')
    changelog_path = os.path.join(stub_gen_path, "CHANGELOG.md")
    version_path = os.path.join(stub_gen_path, 'apistub', '_version.py')

    with open(changelog_path, 'r') as changelog_file:
        latest = re.findall(r'## Version (\d+.\d+.\d+)', changelog_file.read())[0]

    with open(version_path, 'r') as version_file:
        version = re.findall(r'VERSION = "(\d+.\d+.\d+)"', version_file.read())[0]

    if version != latest:
        msg = f"Latest changelog version {latest} does not match _version.py version {version}."
        logging.error(f'{warning_color}{msg}{end_color}')
        sys.exit(1)
    else:
        print(f"Version {latest} is consistent.")
        sys.exit(0)
