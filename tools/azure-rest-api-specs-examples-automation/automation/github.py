import requests
import logging
from typing import List, Dict, Any


class GitHubRepository:
    api_host: str = 'https://api.github.com'
    owner: str
    name: str
    token: str

    def __init__(self, owner: str, name: str, token):
        self.owner = owner
        self.name = name
        self.token = token

    def create_pull_request(self, title: str, head: str, base: str) -> int:
        logging.info(f'Create pull request: {head}')

        request_uri = f'{self.api_host}/repos/{self.owner}/{self.name}/pulls'
        request_body = {
            'title': title,
            'head': head,
            'base': base
        }
        pull_request_response = requests.post(request_uri,
                                              json=request_body,
                                              headers=self._headers())
        if pull_request_response.status_code == 201:
            logging.info('Pull request created')
            return pull_request_response.json()['number']
        else:
            logging.error(f'Request failed: {pull_request_response.status_code}\n{pull_request_response.json()}')
            pull_request_response.raise_for_status()

    def list_pull_requests(self) -> List[Dict[str, Any]]:
        logging.info(f'List pull requests')

        request_uri = f'{self.api_host}/repos/{self.owner}/{self.name}/pulls?per_page=100'
        pull_request_response = requests.get(request_uri,
                                             headers=self._headers())
        if pull_request_response.status_code == 200:
            logging.info('Pull request created')
            return pull_request_response.json()
        else:
            logging.error(f'Request failed: {pull_request_response.status_code}\n{pull_request_response.json()}')
            return []

    def merge_pull_request(self, pull_request: Dict):
        title = pull_request['title']
        logging.info(f'Merge pull request: {title}')

        pull_number = int(pull_request['number'])

        request_uri = f'{self.api_host}/repos/{self.owner}/{self.name}/pulls/{pull_number}/merge'
        request_body = {
            'commit_title': title,
            'merge_method': 'squash'
        }
        merge_response = requests.put(request_uri,
                                      json=request_body,
                                      headers=self._headers())
        if merge_response.status_code == 200:
            logging.info('Pull request merged')
        else:
            logging.error(f'Request failed: {merge_response.status_code}\n{merge_response.json()}')
            merge_response.raise_for_status()

    def list_releases(self, per_page: int, page: int = 1) -> List[Dict[str, Any]]:
        request_uri = f'{self.api_host}/repos/{self.owner}/{self.name}/releases'
        releases_response = requests.get(request_uri,
                                         params={'per_page': per_page, 'page': page},
                                         headers=self._headers())
        if releases_response.status_code == 200:
            return releases_response.json()
        else:
            logging.error(f'Request failed: {releases_response.status_code}\n{releases_response.json()}')
            releases_response.raise_for_status()

    def add_label(self, pull_number: int, labels: List[str]):
        request_uri = f'{self.api_host}/repos/{self.owner}/{self.name}/issues/{pull_number}/labels'
        request_body = {
            'labels': labels
        }
        add_label_response = requests.post(request_uri,
                                           json=request_body,
                                           headers=self._headers())
        if add_label_response.status_code == 200:
            logging.info('Label added')
        else:
            logging.error(f'Request failed: {add_label_response.status_code}\n{add_label_response.json()}')
            add_label_response.raise_for_status()

    def _headers(self) -> Dict[str, str]:
        return {
            'X-GitHub-Api-Version': '2022-11-28',
            'Authorization': f'token {self.token}'
        }
