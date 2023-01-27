import os, argparse, glob, json, datetime, time

from subprocess import run
from typing import List, Dict, Any

import yaml  # pyyaml
from ci_tools.functions import (
    discover_targeted_packages,
)  # azure-sdk-tools from azure-sdk-for-python

generated_folder = os.path.abspath(os.path.join(os.path.abspath(__file__), "..", "generated"))

TABLE_HEADER: str = """| Package | Using Proxy | External Recordings |
|---|---|---|
"""

TABLE_LAYER: str = """|{}|{}|{}|
"""

YES = "✅"
NO = "❌"

DOCUMENT: str = """
<table>
<tr>
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
BATCH_SIZE = TABLE_HEIGHT * 2


class ScanResult:
    def __init__(self, language: str):
        self.language = language
        self.packages: List[str] = []
        self.packages_using_proxy: List[str] = []
        self.packages_using_external: List[str] = []


def get_repo(language: str) -> str:
    where = f"https://github.com/azure/azure-sdk-for-{language.lower()}"
    target_folder = os.path.join(generated_folder, language)
    print(f"Cloning repo for {language} from {where}", end="...")

    if not os.path.exists(target_folder):
        os.makedirs(target_folder)

        command = [
            "git",
            "clone",
            "--depth",
            "1",
            "--branch",
            "main",
            where,
            target_folder,
        ]
        run(command, cwd=generated_folder)
    print(YES)

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


def generate_python_report() -> ScanResult:
    language = "Python"
    repo = get_repo(language)
    print(f"Evaluating repo for {language} @ {repo}", end="...")

    try:
        result = ScanResult(language)

        results = discover_targeted_packages("azure*", repo)

        # filter the results here
        result.packages = [os.path.basename(pkg) for pkg in results]

        for pkg in results:
            evaluation = evaluate_python_package(pkg)
            if evaluation == 1:
                result.packages_using_proxy.append(os.path.basename(pkg))
            elif evaluation == 2:
                result.packages_using_external.append(os.path.basename(pkg))

        print(YES)
    except Exception as e:
        print(NO)

    return result


def evaluate_go_package(package_path: str) -> int:
    pass


# evaluate by finding a testdata/recordings
def generate_go_report() -> ScanResult:
    result = ScanResult("Go")
    return result


def evaluate_net_package(package_path: str) -> int:
    pass


def generate_net_report() -> ScanResult:
    result = ScanResult(".NET")
    return result


def evaluate_cpp_package(package_path: str) -> int:
    pass


def generate_cpp_report() -> ScanResult:
    result = ScanResult("Cpp")
    return result


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
    language = "JS"
    repo = get_repo(language)
    print(f"Evaluating repo for {language} @ {repo}", end="...")

    try:
        target_folder = os.path.join(repo, "sdk", "**", "package.json")
        result = ScanResult(language)

        results = glob.glob(target_folder, recursive=True)

        result.packages = [
            os.path.basename(os.path.dirname(pkg))
            for pkg in results
            if "samples" not in os.path.normpath(pkg).split(os.sep)
            and os.path.basename(os.path.dirname(pkg)) != "samples-react"
        ]

        for pkg in results:
            evaluation = evaluate_js_package(pkg)
            if evaluation == 1:
                result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
            elif evaluation == 2:
                result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
                result.packages_using_external.append(os.path.basename(os.path.dirname(pkg)))
        print(YES)
    except Exception as e:
        print(NO)

    return result


def generate_detailed_table(origin: ScanResult, package_set: List[str]):
    result = TABLE_HEADER
    for package in package_set:
        transitioned = YES if package in origin.packages_using_proxy else NO
        externalized = YES if package in origin.packages_using_external else NO

        table_row = TABLE_LAYER.format(package, transitioned, externalized)
        result += table_row

    return result


def write_output(result: ScanResult) -> None:
    with open(result.language.lower() + ".md", "w", encoding="utf-8") as f:
        date = datetime.date.today()
        time_of_day = datetime.datetime.today().strftime("%I:%M%p")
        f.writelines(
            f"# {result.language} Transition Details as of {date}@{time_of_day} {datetime.datetime.today().astimezone().tzname()}"
        )

        if result.packages:
            # batch by sets of 20
            batch_size = (len(result.packages) // 2) + (len(result.packages) % 2)

            table_set_1 = result.packages[0:batch_size]
            table_set_2 = result.packages[batch_size:]

            document_addition = DOCUMENT.format(
                generate_detailed_table(result, table_set_1), generate_detailed_table(result, table_set_2)
            )

            f.write(document_addition)


# original version of write-output that had two nicely batched tables
# def write_output(result: ScanResult) -> None:
#     with open(result.language.lower() + ".md", "w", encoding="utf-8") as f:
#         date = datetime.date.today()
#         time_of_day = datetime.datetime.today().strftime("%I:%M%p")
#         f.writelines(f"# {result.language} Transition Details as of {date}@{time_of_day} {datetime.datetime.today().astimezone().tzname()}")

#         # batch by sets of 20
#         for i in range(0, len(result.packages), BATCH_SIZE):
#             packages = result.packages[i : i + BATCH_SIZE]
#             table_set_1 = packages[0:TABLE_HEIGHT]
#             table_set_2 = packages[TABLE_HEIGHT:]

#             document_addition = DOCUMENT.format(
#                 generate_detailed_table(result, table_set_1), generate_detailed_table(result, table_set_2)
#             )

#             f.write(document_addition)


def write_summary(results: List[ScanResult]) -> None:
    with open("summary.md", "w", encoding="utf-8") as f:
        f.writelines(f"# Test Proxy Transition Summary - {datetime.date.today()}")


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

    cpp = generate_cpp_report()
    write_output(cpp)

    write_summary([python, js, go, net, cpp])
