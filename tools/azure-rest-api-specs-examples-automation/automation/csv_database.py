from os import path
import logging
import csv
import subprocess
import dataclasses
from datetime import datetime
import re
from typing import List, Union

from github import GitHubRepository
from models import Release


example_repo: str = 'https://github.com/Azure/azure-rest-api-specs-examples'
csvdb_folder: str = 'csvdb'
metadata_branch: str = 'metadata'


@dataclasses.dataclass(eq=True)
class DatabaseInternal:
    rows: List[List]
    next_id: int = 1

    def __init__(self, reader: csv.DictReader):
        next(reader, None)  # skip header row

        self.rows = []
        for row in reader:
            self.rows.append(row)
            self.next_id = max(self.next_id, int(row[0]) + 1)

    def append(self, row) -> str:
        # insert a row, return the id of the row

        row_id = str(self.next_id)
        row.insert(0, row_id)
        self.next_id += 1
        self.rows.append(row)
        return row_id


class CsvDatabase:
    work_dir: str
    example_metadata_path: str
    index_file_path: str
    list_file_path: str

    release_db: DatabaseInternal
    file_db: DatabaseInternal

    branch: str = None
    date_str: str

    def __init__(self, work_dir: str):
        self.work_dir = work_dir
        self.example_metadata_path = path.join(self.work_dir, csvdb_folder)

        self.index_file_path = path.join(self.example_metadata_path, 'java-library-example-index.csv')
        self.list_file_path = path.join(self.example_metadata_path, 'java-library-example-list.csv')

    def checkout(self):
        # checkout metadata branch from azure-rest-api-specs-examples repo
        cmd = ['git', 'clone',
               '--quiet',
               '--depth', '1',
               '--branch', metadata_branch,
               example_repo, self.example_metadata_path]
        logging.info(f'Checking out repository: {example_repo}, branch {metadata_branch}')
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=self.work_dir)

    def load(self):
        with open(self.index_file_path, 'r', newline='') as csv_file:
            csv_reader = csv.reader(csv_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
            self.release_db = DatabaseInternal(csv_reader)

        with open(self.list_file_path, 'r', newline='') as csv_file:
            csv_reader = csv.reader(csv_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
            self.file_db = DatabaseInternal(csv_reader)

    def dump(self):
        with open(self.index_file_path, 'w', newline='') as csv_file:
            csv_writer = csv.writer(csv_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
            csv_writer.writerow(['id', 'name', 'language', 'tag', 'package', 'version', 'date_epoch', 'date'])
            for row in self.release_db.rows:
                csv_writer.writerow(row)

        with open(self.list_file_path, 'w', newline='') as csv_file:
            csv_writer = csv.writer(csv_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
            csv_writer.writerow(['id', 'file', 'release_id'])
            for row in self.file_db.rows:
                csv_writer.writerow(row)

    def commit(self, tag):
        if not self.branch:
            # git checkout new branch
            self.date_str = datetime.now().strftime('%Y-%m-%d')
            self.branch = f'automation-metadata-{self.date_str}'
            cmd = ['git', 'checkout', '-b', self.branch]
            logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=self.example_metadata_path)

        # git add
        cmd = ['git', 'add', 'java-library-example-index.csv']
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=self.example_metadata_path)

        cmd = ['git', 'add', 'java-library-example-list.csv']
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=self.example_metadata_path)

        # git commit
        title = f'[Automation] Update metadata on {tag}'
        logging.info(f'git commit: {title}')
        cmd = ['git',
               '-c', 'user.name=azure-sdk',
               '-c', 'user.email=azuresdk@microsoft.com',
               'commit', '-m', title]
        logging.info('Command line: ' + ' '.join(cmd))
        subprocess.check_call(cmd, cwd=self.example_metadata_path)

    def push(self, github_token: str):
        if self.branch:
            title = f'[Automation] Update metadata on {self.date_str}'
            # git push
            remote_uri = 'https://' + github_token + '@' + example_repo[len('https://'):]
            cmd = ['git', 'push', remote_uri, self.branch]
            # do not print this as it contains token
            # logging.info('Command line: ' + ' '.join(cmd))
            subprocess.check_call(cmd, cwd=self.example_metadata_path)

            # create github pull request
            owner = _repository_owner(example_repo)
            name = _repository_name(example_repo)
            head = f'{owner}:{self.branch}'
            repo = GitHubRepository(owner, name, github_token)
            pull_number = repo.create_pull_request(title, head, metadata_branch)
            repo.add_label(pull_number, ['auto-merge'])
            logging.info(f'succeeded, pull number {pull_number}')

    def new_release(self, name: str, language: str, tag: str, package: str, version: str, date: datetime,
                    files: List[str]) -> bool:
        # add a new release and all the example files
        # return false, if release already exists in DB

        release_id = self._query_release(name, language)
        if release_id:
            logging.warning(f'Release already exists for {language}#{name}')
            return False

        date_epoch = int(date.timestamp())
        date_str = datetime.fromtimestamp(date_epoch).strftime('%m/%d/%Y')

        release_id = self.release_db.append([name, language, tag, package, version, date_epoch, date_str])

        # remove 'file' that already in DB -- maintain column 'file' be unique
        self.file_db.rows = [row for row in self.file_db.rows if row[1] not in files]
        for file in files:
            self.file_db.append([file, release_id])

        return True

    def query_releases(self, language: str) -> List[Release]:
        # query processed releases

        releases = []
        for row in self.release_db.rows:
            if language == row[2]:
                date = datetime.fromtimestamp(int(row[6]))
                releases.append(Release(row[3], row[4], row[5], date))
        return releases

    def _query_release(self, name: str, language: str) -> Union[str, None]:
        for row in self.release_db.rows:
            if name == row[1] and language == row[2]:
                return row[0]
        return None


def _repository_owner(repository: str) -> str:
    return re.match(r'https://github.com/([^/:]+)/.*', repository).group(1)


def _repository_name(repository: str) -> str:
    return re.match(r'https://github.com/[^/:]+/(.*)', repository).group(1)
