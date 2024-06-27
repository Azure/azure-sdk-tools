import os
import shutil
from os import path
import sys
import subprocess
import tempfile
import time
from datetime import timedelta, timezone
import json
import argparse
import logging
import itertools

from models import *
from github import GitHubRepository
from csv_database import CsvDatabase


github_token: str
root_path: str = '.'

csv_database: CsvDatabase

start_time_secs: float

timeout_secs: float = 45 * 60 * 60  # 45 minutes

clean_tmp_dir: bool = True
tmp_folder: str = 'tmp'
tmp_spec_folder: str = 'spec'
tmp_example_folder: str = 'example'
tmp_sdk_folder: str = 'sdk'


def load_configuration(command_line: CommandLineConfiguration) -> Configuration:
    with open(path.join(root_path, 'automation/configuration.json'), 'r', encoding='utf-8') as f_in:
        config = json.load(f_in)

    now = datetime.now(timezone.utc)
    operation_configuration = OperationConfiguration(config['sdkExample']['repository'],
                                                     command_line.build_id,
                                                     command_line.skip_processed,
                                                     command_line.persist_data,
                                                     now - timedelta(days=command_line.release_in_days), now)

    sdk_configurations = []
    for sdk_config in config['sdkConfigurations']:
        script = Script(sdk_config['script']['run'])
        release_tag = ReleaseTagConfiguration(sdk_config['releaseTag']['regexMatch'],
                                              sdk_config['releaseTag']['packageRegexGroup'],
                                              sdk_config['releaseTag']['versionRegexGroup'])
        ignored_packages = sdk_config['ignoredPackages'] if 'ignoredPackages' in sdk_config else []
        sdk_configuration = SdkConfiguration(sdk_config['name'],
                                             sdk_config['language'],
                                             sdk_config['repository'],
                                             release_tag, script, ignored_packages)
        sdk_configurations.append(sdk_configuration)

    return Configuration(operation_configuration, sdk_configurations)


def merge_pull_requests(operation: OperationConfiguration):
    logging.info('Merge pull requests')

    repo = GitHubRepository(operation.repository_owner, operation.repository_name, github_token)

    pull_requests = repo.list_pull_requests()
    for pull_request in pull_requests:
        title = pull_request['title']
        if title.startswith('[Automation]'):
            if 'labels' in pull_request and any(label['name'] == 'auto-merge' for label in pull_request['labels']):
                repo.merge_pull_request(pull_request)

                # wait a few seconds to avoid 409
                time.sleep(5)


def process_release(operation: OperationConfiguration, sdk: SdkConfiguration, release: Release,
                    report: Report):
    # process per release

    logging.info(f'Processing release: {release.tag}')

    tmp_root_path = path.join(root_path, tmp_folder)
    os.makedirs(tmp_root_path, exist_ok=True)
    tmp_path = tempfile.mkdtemp(prefix='tmp', dir=tmp_root_path)
    logging.info(f'Work directory: {tmp_path}')
    try:
        example_repo_path = path.join(tmp_path, tmp_example_folder)
        sdk_repo_path = path.join(tmp_path, tmp_sdk_folder)
        spec_repo_path = path.join(tmp_root_path, tmp_spec_folder)

        # checkout azure-rest-api-specs-examples repo
        cmd = ['git', 'clone',
               '--quiet',
               '--depth', '1',
               operation.sdk_examples_repository, example_repo_path]
        logging.info(f'Checking out repository: {operation.sdk_examples_repository}')
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=tmp_path)

        # checkout sdk repo
        cmd = ['git', 'clone',
               '-c', 'advice.detachedHead=false',
               '--quiet',
               '--depth', '1',
               '--branch', release.tag,
               sdk.repository, sdk_repo_path]
        logging.info(f'Checking out repository: {sdk.repository}')
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=tmp_path)

        # prepare input.json
        input_json_path = path.join(tmp_path, 'input.json')
        output_json_path = path.join(tmp_path, 'output.json')
        with open(input_json_path, 'w', encoding='utf-8') as f_out:
            input_json = {
                'specsPath': spec_repo_path,
                'sdkExamplesPath': example_repo_path,
                'sdkPath': sdk_repo_path,
                'tempPath': tmp_path,
                'release': {
                    'tag': release.tag,
                    'package': release.package,
                    'version': release.version
                }
            }
            logging.info(f'Input JSON for worker: {input_json}')
            json.dump(input_json, f_out, indent=2)

        # run script
        logging.info(f'Running worker: {sdk.script.run}')
        start = time.perf_counter()
        subprocess.check_call([sdk.script.run, input_json_path, output_json_path], cwd=root_path)
        end = time.perf_counter()
        logging.info(f'Worker ran: {str(timedelta(seconds=end-start))}')

        # parse output.json
        release_name = release.tag
        succeeded = True
        files = []
        if path.isfile(output_json_path):
            with open(output_json_path, 'r', encoding='utf-8') as f_in:
                output = json.load(f_in)
                logging.info(f'Output JSON from worker: {output}')
                release_name = output['name']
                succeeded = ('succeeded' == output['status'])
                files = output['files']

        if not succeeded:
            report.statuses[release.tag] = 'failed at worker'
            report.aggregated_error.errors.append(RuntimeError(f'Worker failed for release tag: {release.tag}'))
            return

        # commit and create pull request
        # check for new examples
        cmd = ['git', 'status', '--porcelain']
        logging.info('Command line: ' + ' '.join(cmd))
        output = subprocess.check_output(cmd, cwd=example_repo_path)
        if len(output) == 0:
            logging.info(f'No change to repository: {example_repo_path}')
            report.statuses[release.tag] = 'succeeded, no change'
        else:
            output_str = str(output, 'utf-8')
            logging.info(f'git status:\n{output_str}')

            # git add
            cmd = ['git', 'add', '--all']
            logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=example_repo_path)

            # find added/modified files
            cmd = ['git', 'status', '--porcelain']
            logging.info('Command line: ' + ' '.join(cmd))
            output = subprocess.check_output(cmd, cwd=example_repo_path)
            output_str = str(output, 'utf-8')
            changed_files = [file.strip()[3:] for file in output_str.splitlines()]

            # git checkout new branch
            branch = f'automation-examples_{sdk.name}_{release.tag}_{operation.build_id}'
            cmd = ['git', 'checkout', '-b', branch]
            logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=example_repo_path)

            # git commit
            title = f'[Automation] Collect examples from {sdk.name}#{release.tag}'
            logging.info(f'git commit: {title}')
            cmd = ['git',
                   '-c', 'user.name=azure-sdk',
                   '-c', 'user.email=azuresdk@microsoft.com',
                   'commit', '-m', title]
            logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=example_repo_path)

            # git push
            remote_uri = 'https://' + github_token + '@' + operation.sdk_examples_repository[len('https://'):]
            cmd = ['git', 'push', remote_uri, branch]
            # do not print this as it contains token
            # logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=example_repo_path)

            try:
                # create github pull request
                head = f'{operation.repository_owner}:{branch}'
                repo = GitHubRepository(operation.repository_owner, operation.repository_name, github_token)
                pull_number = repo.create_pull_request(title, head, 'main')
                repo.add_label(pull_number, ['auto-merge'])
            except Exception as e:
                logging.error(f'Error: {e}')
                report.statuses[release.tag] = 'failed to create pull request'
                report.aggregated_error.errors.append(e)
                return

            try:
                if operation.persist_data:
                    # commit changes to database
                    commit_database(release_name, sdk.language, release, files)
            except Exception as e:
                logging.error(f'Error: {e}')
                report.statuses[release.tag] = 'failed to update database'
                report.aggregated_error.errors.append(e)
                return

            report.statuses[release.tag] = f'succeeded, {len(changed_files)} files changed, pull number {pull_number}'

    except subprocess.CalledProcessError as e:
        logging.error(f'Call error: {e}')
        report.statuses[release.tag] = 'failed to invoke git'
        report.aggregated_error.errors.append(e)
    finally:
        if clean_tmp_dir:
            shutil.rmtree(tmp_path, ignore_errors=True)


def query_releases_in_database(language: str) -> List[Release]:
    # query local database on processed releases

    return csv_database.query_releases(language)


def commit_database(release_name: str, language: str, release: Release, changed_files: List[str]):
    # write to local database and commit to repository

    # exclude metadata JSON
    changed_files = [file for file in changed_files if not file.endswith('.json')]

    if changed_files:
        database_succeeded = csv_database.new_release(
            release_name, language, release.tag, release.package, release.version, release.date, changed_files)
        if database_succeeded:
            csv_database.dump()
            csv_database.commit(release_name)


def process_sdk(operation: OperationConfiguration, sdk: SdkConfiguration, report: Report):
    # process for sdk

    if time.time() > start_time_secs + timeout_secs:
        logging.warning(f"Timeout, skip sdk: {sdk.name}")
        return

    logging.info(f'Processing sdk: {sdk.name}')
    count = 0
    releases: List[Release] = []
    repo = GitHubRepository(sdk.repository_owner, sdk.repository_name, github_token)
    # since there is no ordering from GitHub, just get all releases (exclude draft=True), and hope paging is correct
    for page in itertools.count(start=1):
        try:
            releases_response_json = repo.list_releases(100, page)
            if len(releases_response_json) == 0:
                # no more result, we are done
                break
            count += len(releases_response_json)
            for release in releases_response_json:
                if not release['draft']:
                    published_at = datetime.fromisoformat(release['published_at'].replace('Z', '+00:00'))
                    if operation.date_start < published_at < operation.date_end:
                        release_tag = release['tag_name']
                        if re.match(sdk.release_tag.regex_match, release_tag):
                            package = re.match(sdk.release_tag.package_regex_group, release_tag).group(1)
                            version = re.match(sdk.release_tag.version_regex_group, release_tag).group(1)
                            release = Release(release_tag, package, version, published_at)
                            releases.append(release)
                            logging.info(f'Found release tag: {release.tag}')
        except Exception as e:
            report.aggregated_error.errors.append(e)
            break
    logging.info(f'Count of all releases: {count}')

    releases.sort(key=lambda r: r.date, reverse=True)
    for release in releases:
        logging.info(f'Candidate release tag: {release.tag}, on {release.date.date()}')

    processed_release_tags = set()
    if operation.skip_processed:
        processed_releases = query_releases_in_database(sdk.language)
        processed_release_tags.update([r.tag for r in processed_releases])

    processed_release_packages = set()
    for release in releases:
        if time.time() > start_time_secs + timeout_secs:
            logging.warning("Timeout, skip remaining packages")
            break

        if release.tag in processed_release_tags:
            logging.info(f'Skip processed tag: {release.tag}')
            processed_release_packages.add(release.package)
        elif release.package in processed_release_packages:
            logging.info(f'Skip processed package: {release.tag}')
        elif release.package in sdk.ignored_packages:
            logging.info(f'Skip ignored package: {release.tag}')
        else:
            process_release(operation, sdk, release, report)
            processed_release_packages.add(release.package)


def process(command_line: CommandLineConfiguration, report: Report):
    configuration = load_configuration(command_line)

    if command_line.merge_pr:
        merge_pull_requests(configuration.operation)

    # checkout azure-rest-api-specs repo
    tmp_root_path = path.join(root_path, tmp_folder)
    os.makedirs(tmp_root_path, exist_ok=True)
    spec_repo_path = path.join(tmp_root_path, tmp_spec_folder)
    spec_repo = 'https://github.com/Azure/azure-rest-api-specs'
    cmd = ['git', 'clone',
           '--quiet',
           '--depth', '1',
           spec_repo, spec_repo_path]
    logging.info(f'Checking out repository: {spec_repo}')
    logging.info('Command line: ' + ' '.join(cmd))
    subprocess.check_call(cmd, cwd=tmp_root_path)

    # checkout and load database
    global csv_database
    csv_database = CsvDatabase(tmp_root_path)
    csv_database.checkout()
    csv_database.load()

    for sdk_configuration in configuration.sdks:
        if not command_line.language or command_line.language == sdk_configuration.language:
            process_sdk(configuration.operation, sdk_configuration, report)

    if command_line.persist_data:
        csv_database.push(github_token)


def main():
    global root_path
    global github_token
    global start_time_secs

    logging.basicConfig(level=logging.INFO,
                        format='%(asctime)s [%(levelname)s] %(message)s',
                        datefmt='%Y-%m-%d %X')

    start_time_secs = time.time()

    script_path = path.abspath(path.dirname(sys.argv[0]))
    root_path = path.abspath(path.join(script_path, '..'))

    parser = argparse.ArgumentParser(description='')
    parser.add_argument('--build-id', type=str, required=True,
                        help='Build ID')
    parser.add_argument('--github-token', type=str, required=True,
                        help='GitHub token')
    parser.add_argument('--release-in-days', type=int, required=False, default=3,
                        help='Process SDK released within given days')
    parser.add_argument('--language', type=str, required=False,
                        help='Process SDK for specific language. Currently supports "java" and "go".')
    parser.add_argument('--persist-data', type=str, required=False, default='false',
                        help='Persist data about release and files to database')
    parser.add_argument('--skip-processed', type=str, required=False, default='false',
                        help='Skip SDK releases that already been processed')
    parser.add_argument('--merge-pull-request', type=str, required=False, default='false',
                        help='Merge GitHub pull request before new processing')
    args = parser.parse_args()

    github_token = args.github_token

    command_line_configuration = CommandLineConfiguration(args.build_id, args.release_in_days, args.language,
                                                          args.persist_data.lower() == 'true',
                                                          args.skip_processed.lower() == 'true',
                                                          args.merge_pull_request.lower() == 'true')

    report = Report({}, AggregatedError([]))
    process(command_line_configuration, report)

    if report.statuses:
        statuses_str = 'Statuses:'
        for tag, status in report.statuses.items():
            statuses_str += f'\n{tag}: {status}'
        logging.info(statuses_str)
    if report.aggregated_error.errors:
        raise RuntimeError(report.aggregated_error.errors)


if __name__ == '__main__':
    main()
