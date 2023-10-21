import os, argparse, glob, json, datetime, re

from subprocess import run
from typing import List, Dict, Any

import yaml  # pyyaml
from packaging import version  # from packaging
from ci_tools.functions import (
    discover_targeted_packages,
)  # azure-sdk-tools from Azure/azure-sdk-for-python

from ci_tools.parsing import ParsedSetup

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

TABLE_LAYER: str = """|{}|{}|{}|
"""

SUMMARY_TABLE_HEADER: str = """| Language | Package Count | Using Proxy | External Recordings |
|---|---|---|---|
"""

SUMMARY_TABLE_LAYER: str = """|{}|{}|{:.0%}|{:.0%}|
"""

SUMMARY_NOTES = """
## A few notes about how this data was generated

- Markdown for these wiki pages is generated from a [single python script.](https://github.com/Azure/azure-sdk-tools/tree/main/tools/assets-automation/assets-reporting/generate_assets_report.py)
  - Within the script follow `generate_<language>_report()` definition to understand how the data for that language was obtained.
- The `Package Count` for each language is NOT the actual total count of packages within each monorepo. It is the count of packages that are slated to transition _at some point_. 
- Where applicable, counts only include `track 2` packages, upholding the previous point about "intended to transition eventually."
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
    else:
        command = [
            "git",
            "pull",
            "origin",
            "main"
        ]
        run(command, cwd= os.path.join(generated_folder, target_folder))

    print(YES)

    return target_folder


def evaluate_python_package(package_path: str) -> int:
    service_dir, _ = os.path.split(package_path)
    recordings_folder = os.path.join(package_path, "tests", "recordings")
    recordings_glob = os.path.join(recordings_folder, "*.json")
    assets_json = os.path.join(package_path, "assets.json")

    details = ParsedSetup.from_path(package_path)

    if not (
        any(["azure-core" in req for req in details.requires])
        or any(["azure-mgmt-core" in req for req in details.requires])
    ):
        return 0

    # only examine packages that currently have recordings (and ensure that ones transitioned to external aren't ignored)
    if not os.path.exists(recordings_folder) and not os.path.exists(assets_json):
        return 0

    # if there is an assets.json present at root, we are done. it's transitioned.
    if os.path.exists(assets_json):
        return 2

    # otherwise, we have to check the recordings for yml (vcrpy) or json (test-proxy)
    test_proxy_files = glob.glob(recordings_glob)
    if test_proxy_files:
        return 1

    return 0


def generate_python_report() -> ScanResult:
    language = "Python"
    repo = get_repo(language)
    print(f"Evaluating repo for {language} @ {repo}", end="...")

    result = ScanResult(language)

    results = [pkg for pkg in discover_targeted_packages("azure*", repo) if "-nspkg" not in pkg]

    to_be_removed = []

    for pkg in results:
        evaluation = evaluate_python_package(pkg)

        if evaluation == 0:
            to_be_removed.append(pkg)
        elif evaluation == 1:
            result.packages_using_proxy.append(os.path.basename(pkg))
        elif evaluation == 2:
            result.packages_using_proxy.append(os.path.basename(pkg))
            result.packages_using_external.append(os.path.basename(pkg))

    result.packages = sorted(
        set([os.path.basename(pkg) for pkg in results]) - set([os.path.basename(pkg) for pkg in to_be_removed])
    )

    print("done.")
    return result


def evaluate_go_package(package_path: str) -> int:
    evaluation = 0
    possible_recordings_dir = os.path.join(package_path, "testdata", "recordings")
    possible_assets = os.path.join(package_path, "assets.json")

    # only examine packages that currently have recordings (and ensure that ones transitioned to external aren't ignored)
    if not os.path.exists(possible_recordings_dir) and not os.path.exists(possible_assets):
        return 0

    if os.path.exists(possible_recordings_dir):
        evaluation = 1

    if os.path.exists(possible_assets):
        evaluation = 2

    return evaluation


# evaluate by finding a testdata/recordings
def generate_go_report() -> ScanResult:
    language = "Go"

    repo_root = get_repo(language)

    print(f"Evaluating repo for {language} @ {repo_root}", end="...")

    result = ScanResult(language)
    sdk_path = os.path.join(repo_root, "sdk")

    exclusions = [os.path.join("testdata", "perf", "go.mod"), "template", "samples", "internal", "azcore"]

    packages = glob.glob(os.path.join(repo_root, "sdk", "**", "go.mod"), recursive=True)
    packages = [os.path.dirname(pkg) for pkg in packages if not any([x in pkg for x in exclusions])]

    result.packages = sorted(set([pkg.replace(sdk_path + os.sep, "") for pkg in packages]))

    for pkg in packages:
        evaluation = evaluate_go_package(pkg)

        if evaluation == 0:
            result.packages.remove(pkg.replace(sdk_path + os.sep, ""))            
        elif evaluation == 1:
            result.packages_using_proxy.append(pkg.replace(sdk_path + os.sep, ""))
        elif evaluation == 2:
            result.packages_using_proxy.append(pkg.replace(sdk_path + os.sep, ""))
            result.packages_using_external.append(pkg.replace(sdk_path + os.sep, ""))

    print("done.")

    return result


def evaluate_net_package(csproj_path: str) -> int:
    evaluation = 0
    found_recorded_testcase = False
    possible_test_directory = os.path.join(os.path.dirname(csproj_path), "..", "tests")
    possible_project_assets_json = os.path.join(os.path.dirname(csproj_path), "..", "assets.json")
    possible_solution_assets_json = os.path.join(os.path.dirname(csproj_path), "..", "..", "assets.json")
    session_records = os.path.join(possible_test_directory, "SessionRecords")
    package_name = os.path.splitext(os.path.basename(csproj_path))[0]

    if not os.path.exists(possible_test_directory):
        return 0

    # for Azure.*, only examine packages with recorded tests. EG with existing SessionRecords or an existing assets.json
    if not os.path.exists(session_records) and not (
        os.path.exists(possible_project_assets_json) or os.path.exists(possible_solution_assets_json)
    ):
        return 0

    # For mgmt, you should find a reference to ManagementRecordedTestBase in projects using test proxy:
    #  https://grep.app/search?q=managementrecordedtestbase&filter[repo][0]=Azure/azure-sdk-for-net&filter[path][0]=sdk/
    # For data plane, you should find RecordedTestBase:
    #  https://grep.app/search?q=recordedtestbase&filter[repo][0]=Azure/azure-sdk-for-net&filter[path][0]=sdk/

    find = "RecordedTestBase"
    if "ResourceManager" in package_name:
        find = "ManagementRecordedTestBase"

    test_files = glob.glob(os.path.join(possible_test_directory, "**", "*.cs"), recursive=True)

    for testfile in test_files:
        try:
            with open(testfile, "r", encoding="utf-8") as f:
                content = f.read()

                if find in content:
                    evaluation = 1
        except:
            pass

    if os.path.exists(possible_project_assets_json) or os.path.exists(possible_solution_assets_json):
        evaluation = 2

    return evaluation


def net_trim_path(solution_path: str) -> str:
    return os.path.splitext(os.path.basename(solution_path))[0]


def generate_net_report() -> ScanResult:
    language = "net"
    result = ScanResult("." + language.upper())
    repo = get_repo(language)

    print(f"Evaluating repo for {language} @ {repo}", end="...")

    #                                                     <service>
    #                                                         |<package>
    #                                                         |    |
    all_azure_projects = glob.glob(os.path.join(repo, "sdk", "*", "*", "src", "*Azure.*.csproj"), recursive=True)

    to_be_removed = []
    for csproj in all_azure_projects:
        evaluation = evaluate_net_package(csproj)

        if evaluation == 0:
            to_be_removed.append(csproj)
        elif evaluation == 1:
            result.packages_using_proxy.append(net_trim_path(csproj))
        elif evaluation == 2:
            result.packages_using_proxy.append(net_trim_path(csproj))
            result.packages_using_external.append(net_trim_path(csproj))

    result.packages = sorted(
        set([net_trim_path(csproj) for csproj in all_azure_projects])
        - set([net_trim_path(csproj) for csproj in to_be_removed])
    )

    print("done.")

    return result


def evaluate_cpp_package(package_path: str) -> int:
    evaluation = 0

    possible_assets_json = os.path.join(package_path, "..", "assets.json")

    if False:
        evaluation = 1

    if os.path.exists(possible_assets_json):
        evaluation = 2

    return evaluation


def generate_cpp_report() -> ScanResult:
    language = "CPP"
    result = ScanResult(language)
    repo_root = get_repo(language)

    print(f"Evaluating repo for {language} @ {repo_root}", end="...")

    exclusions = [os.path.join("vcpkg", "vcpkg.json"), "template", os.path.join("sdk", "core")]

    packages = glob.glob(os.path.join(repo_root, "sdk", "**", "vcpkg.json"), recursive=True)
    packages = [os.path.dirname(pkg) for pkg in packages if not any([x in pkg for x in exclusions])]

    result.packages = sorted([os.path.basename(pkg) for pkg in packages])

    for pkg in packages:
        evaluation = evaluate_cpp_package(pkg)

        if evaluation == 1:
            result.packages_using_proxy.append(os.path.basename(pkg))
        elif evaluation == 2:
            result.packages_using_proxy.append(os.path.basename(pkg))
            result.packages_using_external.append(os.path.basename(pkg))

    print("done.")
    return result


def resolve_java_test_directory(package_path: str) -> str:
    singular = os.path.join(os.path.dirname(package_path), "src", "test")
    plural = os.path.join(os.path.dirname(package_path), "src", "tests")

    if os.path.exists(singular):
        return singular
    elif os.path.exists(plural):
        return plural
    else:
        return ""


def evaluate_java_package(package_path: str) -> int:
    possible_test_directory = resolve_java_test_directory(package_path)
    possible_assets_location = os.path.join(os.path.dirname(package_path),'assets.json')

    if os.path.exists(possible_assets_location):
        return 2
    
    if not possible_test_directory:
        return -1

    test_files = glob.glob(os.path.join(possible_test_directory, "**", "*.java"), recursive=True)

    # we only will search the test_files if there are actual session-records present
    session_glob = os.path.join(possible_test_directory, "**", "session-records")
    session_records = glob.glob(session_glob, recursive=True)

    if not session_records:
        return -1

    for testfile in test_files:
        try:
            with open(testfile, "r", encoding="utf-8") as f:
                content = f.read()

                if "extends TestProxyTestBase" in content:
                    return 1
        except:
            pass

    return 0


def generate_java_report() -> ScanResult:
    language = "Java"
    result = ScanResult(language)
    repo_root = get_repo(language)

    print(f"Evaluating repo for {language} @ {repo_root}", end="...")

    # enforce looking under individual package dir, and not service dir
    packages = glob.glob(os.path.join(repo_root, "sdk", "*", "*", "pom.xml"), recursive=True)

    # we don't care about packages that start with 'microsoft-' as they are track 1 and will never migrate
    packages = [package for package in packages if not "microsoft-" in os.path.dirname(package)]
    packages = [package for package in packages if not "azure-communication-callingserver" in os.path.dirname(package)]
    packages = [package for package in packages if not "azure-maps-elevation" in os.path.dirname(package)]
    packages = [package for package in packages if not "azure-verticals-agrifood-farming" in os.path.dirname(package)]
   
    result.packages = sorted([os.path.basename(os.path.dirname(pkg)) for pkg in packages])

    for pkg in packages:
        evaluation = evaluate_java_package(pkg)

        if evaluation == -1:
            result.packages.remove(os.path.basename(os.path.dirname(pkg)))
        elif evaluation == 1:
            result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
        elif evaluation == 2:
            result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
            result.packages_using_external.append(os.path.basename(os.path.dirname(pkg)))

    result.packages = sorted(set(result.packages))
    print("done.")

    return result


def evaluate_js_package(package_path: str) -> int:
    with open(package_path, "r", encoding="utf-8") as f:
        package_json = json.load(f)

    assets_json = os.path.join(os.path.dirname(package_path), "assets.json")
    if os.path.exists(assets_json):
        return 2

    if "devDependencies" in package_json:
        if "@azure-tools/test-recorder" in package_json["devDependencies"]:
            version_spec = package_json["devDependencies"]["@azure-tools/test-recorder"]
            if version_spec[0] == "^":
                version_spec = version_spec[1:]

            if version.parse(version_spec) >= version.parse("2.0.0"):
                return 1

    return 0


def e_startswith(input: str, prefixes: List[str]) -> bool:
    return any([input.startswith(fix) for fix in prefixes])


def e_endswith(input: str, postfixes: List[str]) -> bool:
    return any([input.endswith(fix) for fix in postfixes])


def e_directory_in(input_dir: str, directory_patterns: List[str]) -> bool:
    return any([subdir in input_dir for subdir in directory_patterns])


def js_package_included(package_path: str) -> bool:
    package_name = os.path.basename(os.path.dirname(package_path))

    excluded_packages = [
        "samples-react",
        "sample-react",
        "mock-hub", "abort-controller",
        "logger",
        "samples-express",
        "samples-browser", "samples-react",
        "event-hubs-track-1",
        "opentelemetry-instrumentation-azure-sdk",
        "monitor-opentelemetry-exporter",
        "service-bus-v1",
        "service-bus-v7",
        "app",
        "perf",
        "service-bus",
        "eventhubs-checkpointstore-blob",
        "eventhubs-checkpointstore-tables",
        "schema-registry-avro",
        "api-management-custom-widgets-scaffolder",
        "storage-internal-avro",
        "web-pubsub-express",
    ]

    excluded_package_postfixes = ["-track-1", "-common"]

    excluded_package_prefixes = ["@azure/core-", "core-"]

    # exclude any packages that have these paths in them
    excluded_directories = [
        os.path.join("sdk", "identity", "identity", "test"),
        os.path.join("sdk", "test-utils"),
        os.path.join("sdk", "core"),
        "samples",
    ]

    # only include packages with a test folder alongside
    has_test_folder = os.path.exists(os.path.join(os.path.dirname(package_path), "test"))

    # insure we don't include amqp packages (they cant convert to test-proxy)
    amqp_package = False
    with open(package_path, "r", encoding="utf-8") as f:
        package_json = json.load(f)
    if "dependencies" in package_json:
        if "@azure/core-amqp" in package_json["dependencies"]:
            amqp_package = True

    return (
        "samples" not in os.path.normpath(package_path).split(os.sep)
        and package_name not in excluded_packages
        and not e_startswith(package_name, excluded_package_prefixes)
        and not e_endswith(package_name, excluded_package_postfixes)
        and not e_directory_in(package_path, excluded_directories)
        and not amqp_package
        and has_test_folder
    )


def generate_js_report() -> ScanResult:
    language = "JS"
    repo = get_repo(language)
    print(f"Evaluating repo for {language} @ {repo}", end="...")

    target_folder = os.path.join(repo, "sdk", "**", "package.json")
    result = ScanResult(language)

    results = glob.glob(target_folder, recursive=True)

    result.packages = sorted(
        set([os.path.basename(os.path.dirname(pkg)) for pkg in results if js_package_included(pkg)])
    )

    excluded = set(sorted([os.path.basename(os.path.dirname(pkg)) for pkg in results if not js_package_included(pkg)]))

    for pkg in results:
        evaluation = evaluate_js_package(pkg)
        if evaluation == 1:
            result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
        elif evaluation == 2:
            result.packages_using_proxy.append(os.path.basename(os.path.dirname(pkg)))
            result.packages_using_external.append(os.path.basename(os.path.dirname(pkg)))

    print("done.")
    return result


def generate_detailed_table(origin: ScanResult, package_set: List[str]) -> str:
    result = TABLE_HEADER
    for package in package_set:
        transitioned = YES if package in origin.packages_using_proxy else NO
        externalized = YES if package in origin.packages_using_external else NO

        table_row = TABLE_LAYER.format(package.replace("\\", "/"), transitioned, externalized)
        result += table_row

    return result


def generate_summary_table(results: List[ScanResult]) -> str:
    result = SUMMARY_TABLE_HEADER
    # Language | Package Count | Using Proxy | External Recordings
    for language in results:
        result += SUMMARY_TABLE_LAYER.format(
            language.language,
            len(language.packages),
            (len(language.packages_using_proxy) / float(len(language.packages))),
            (len(language.packages_using_external) / float(len(language.packages))),
        )

    return result


def write_output(result: ScanResult) -> None:
    with open(result.language.lower() + ".md", "w", encoding="utf-8") as f:
        date = datetime.date.today()

        # leaving this commented, as the level of detail doesn't assist the report
        # time_of_day = datetime.datetime.today().strftime("%I:%M%p")
        # @{time_of_day} {datetime.datetime.today().astimezone().tzname()}
        f.writelines(f"# {result.language} Transition Details as of {date}")

        if result.packages:
            # batch two sets
            batch_size = (len(result.packages) // 2) + (len(result.packages) % 2)

            table_set_1 = result.packages[0:batch_size]
            table_set_2 = result.packages[batch_size:]

            document_addition = DOCUMENT.format(
                generate_detailed_table(result, table_set_1), generate_detailed_table(result, table_set_2)
            )

            f.write(document_addition)


def write_summary(results: List[ScanResult]) -> None:
    with open("summary.md", "w", encoding="utf-8") as f:
        date = datetime.date.today()
        # leaving this commented, as the level of detail doesn't assist the report
        # time_of_day = datetime.datetime.today().strftime("%I:%M%p")
        # @{time_of_day} {datetime.datetime.today().astimezone().tzname()}
        f.writelines(f"# Test-Proxy overall progress per language - {date}" + os.linesep)

        summary = generate_summary_table(results)

        f.write(summary)

        f.write(SUMMARY_NOTES)


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
    write_output(net)

    cpp = generate_cpp_report()
    write_output(cpp)

    java = generate_java_report()
    write_output(java)

    write_summary(
        [
            python,
            js,
            go,
            net,
            cpp,
            java
        ]
    )
