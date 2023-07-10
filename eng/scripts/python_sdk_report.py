#!/usr/bin/python3

"""
python_sdk_report.py
Generate APIView for all SDKs in the azure-sdk-for-python repo and report on any failures.
"""

import glob
import json
import os
import re
import sys
from typing import Optional

from apistub import StubGenerator

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))

PACKAGE_NAME_RE = re.compile(r"sdk\\([a-z]+)\\([a-z\-]+)\\setup.py")

SKIP_PACKAGES = [
    "core:azure",
    "core:azure-mgmt",
    "monitor:azure-monitor",
    "storage:azure-storage"
]

class _Result:
    def __init__(self, *, service_dir: str, package_name: str, success: bool, error: Optional[str]):
        self.service_dir = service_dir
        self.package_name = package_name
        self.success = success
        self.error = error

if __name__ == '__main__':
    warning_color = '\033[91m'
    end_color = '\033[0m'

    stub_gen_path = os.path.join(ROOT, 'packages', 'python-packages', 'apiview-stub-generator')
    changelog_path = os.path.join(stub_gen_path, "CHANGELOG.md")
    version_path = os.path.join(stub_gen_path, 'apistub', '_version.py')

    args =  sys.argv
    if len(args) != 2:
        print("usage: python python_sdk_report.py <PYTHON SDK REPO ROOT>")
        sys.exit(1)
    
    python_sdk_root = args[1]
    print(f"Python SDK Root: {python_sdk_root}")

    results = {}
    for path in glob.glob(os.path.join(python_sdk_root, "sdk", "**", "**", "setup.py")):
        package_path = os.path.split(path)[0]
        try:
            (service_dir, package_name) = PACKAGE_NAME_RE.findall(path)[0]
        except:
            print(f"Couldn't parse: {path}")
            continue

        if f"{service_dir}:{package_name}" in SKIP_PACKAGES:
            continue

        print(f"Parsing {service_dir}/{package_name}...")
        if service_dir not in results:
            results[service_dir] = []
        try:
            _ = StubGenerator(pkg_path=package_path, skip_pylint=True).generate_tokens()
            success = True
            error = None
        except Exception as err:
            success = False
            error = str(err)
        results[service_dir].append(_Result(
            service_dir=service_dir,
            package_name=package_name,
            success=success,
            error=error
        ))
    filename = "stubgen_report.json"
    print(f"Saving results to {filename}...")
    with open(filename, "w") as outfile:
        outfile.write(json.dumps(results, indent=4))
