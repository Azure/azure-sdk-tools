import os, argparse, glob, json

from subprocess import run
from typing import List, Dict, Any

import yaml  # pyyaml
from ci_tools.functions import (
    discover_targeted_packages,
)  # azure-sdk-tools from azure-sdk-for-python

generated_folder = os.path.abspath(os.path.join(os.path.abspath(__file__), "..", "generated"))

TABLE_HEADER: str = """| Package | Using Proxy | Externalized Recordings |
|---|---|---|
"""

TABLE_LAYER: str = "|{}|{}|{}|"

DOCUMENT: str = """
<table>
<tr>
<td>

{}

</td>
<td>

{}

</td>
<td>

{}

</td>
</tr>
</table>
"""

TABLE_HEIGHT: int = 10


class ScanResult:
    def __init__(self, language: str):
        self.language = language
        self.packages: List[str] = []
        self.packages_using_proxy: List[str] = []
        self.packages_using_external: List[str] = []


def get_repo(language: str) -> str:
    target_folder = os.path.join(generated_folder, language)

    if not os.path.exists(target_folder):
        os.makedirs(target_folder)

        command = [
            "git",
            "clone",
            "--depth",
            "1",
            "--branch",
            "main",
            f"https://github.com/azure/azure-sdk-for-{language.lower()}",
            target_folder,
        ]
        run(command, cwd=generated_folder)

    return target_folder


def evaluate_python_package(package_path: str) -> int:
    service_dir, _ = os.path.split(package_path)
    recordings_path = os.path.join(package_path, "tests", "recordings", "*.json")
    assets_json = os.path.join(package_path, "assets.json")
    ci_yml = os.path.join(service_dir, "ci.yml")
    result = 0

    if os.path.exists(ci_yml):
        with open(ci_yml, "r") as file:
            ci_config = yaml.safe_load(file)

            # there is no reason to even do further evaluation if the TestProxy parameter isn't set. CI won't use it if it's not.
            parameters = ci_config["extends"]["parameters"]
            if "TestProxy" in parameters and parameters["TestProxy"] == True:
                # if there is an assets.json present at root, we are done. it's transitioned.
                if os.path.exists(assets_json):
                    return 2

                # otherwise, we have to check the recordings for yml (vcrpy) or json (test-proxy)
                test_proxy_files = glob.glob(recordings_path)
                if test_proxy_files:
                    return 1
    return result


def generate_go_report() -> ScanResult:
    pass


def generate_net_report() -> ScanResult:
    pass


def evaluate_js_package(package_path: str) -> int:
    with open(package_path, "r", encoding="utf-8") as f:
        package_json = json.load(f)

    assets_json = os.path.join(os.path.dirname(package_path), "assets.json")
    if os.path.exists(assets_json):
        return 2

    if "devDependencies" in package_json:
        if "@azure-tools/test-recorder" in package_json["devDependencies"]:
            if package_json["devDependencies"]["@azure-tools/test-recorder"] == "^2.0.0":
                return 1

    return 0


def generate_js_report() -> ScanResult:
    repo = get_repo("JS")
    target_folder = os.path.join(repo, "sdk", "**", "package.json")
    result = ScanResult("JS")

    result.packages = glob.glob(target_folder, recursive=True)

    for pkg in result.packages:
        evaluation = evaluate_js_package(pkg)
        if evaluation == 1:
            result.packages_using_proxy.append(pkg)
        elif evaluation == 2:
            result.packages_using_external.append(pkg)

    return result


def write_output(result: ScanResult) -> None:
    pass


def write_summary(results: List[ScanResult]) -> None:
    pass


def generate_python_report() -> ScanResult:
    repo = get_repo("Python")

    result = ScanResult("Python")
    result.packages = discover_targeted_packages("azure*", repo)

    for pkg in result.packages:
        evaluation = evaluate_python_package(pkg)
        if evaluation == 1:
            result.packages_using_proxy.append(pkg)
        elif evaluation == 2:
            result.packages_using_external.append(pkg)

    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="""
      Generates a markdown report that summarizes the the status of the transition to the test-proxy and externalized assets.
      """
    )
    parser.parse_args()

    python = generate_python_report()
    write_output(python)

    js = generate_js_report()
    write_output(js)

    go = generate_go_report()
    write_output(go)

    net = generate_net_report()
    write_output(go)

    write_summary([python, js, go, net])
