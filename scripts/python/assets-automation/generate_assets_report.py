import os, argparse

from subprocess import run
from typing import List, Dict, Any

from ci_tools.functions import discover_targeted_packages


generated_folder = os.path.abspath(os.path.join(os.path.abspath(__file__), "..", "generated"))


class ScanResult:
    def __init__(self, language: str):
        self.language = language
        self.packages: List[str] = []
        self.packages_using_proxy: List[str] = []
        self.packages_using_external: List[str] = []


LANGUAGES = {
    "Net": "https://github.com/azure/azure-sdk-for-net",
    "Java": "https://github.com/azure/azure-sdk-for-java",
    "Python": "https://github.com/azure/azure-sdk-for-python",
    "Java": "https://github.com/azure/azure-sdk-for-java",
    "C++": "https://github.com/azure/azure-sdk-for-cpp",
    "C": "https://github.com/azure/azure-sdk-for-c",
    "iOS": "https://github.com/azure/azure-sdk-for-ios",
    "Android": "https://github.com/azure/azure-sdk-for-android",
}

LANGUAGE_FUNCTIONS = {}


def get_repo(language: str) -> str:
    target_folder = os.path.join(generated_folder, language)

    if not os.path.exists(target_folder):
        os.makedirs(target_folder)

        command = ["git", "clone", "--depth", "1", "--branch", "main", LANGUAGES[language], target_folder]

        run(command, cwd=generated_folder)

    return target_folder


def evaluate_python_package(package_path: str) -> str:
    service_dir, _ = os.path.split(package_path)

    pass


def generate_python_report() -> ScanResult:
    repo = get_repo("Python")

    result = ScanResult("Python")
    result.packages = discover_targeted_packages("azure*", repo)
    
    for pkg in result.packages:
        evaluation = evaluate_python_package(pkg)
        # 0 == no detected
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
    # parser.add_argument("-t", "--target", help="The targeted language", required=True)

    generate_python_report()
