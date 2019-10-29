# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function
import argparse
import yaml
import os
import requests
from requests.exceptions import HTTPError

class PackageInfo():
    def __init__(self, package_id = '',
            package_version = '',
            relative_package_location = '',
            relative_readme_location = '',
            relative_changelog_location = '',
            repository_args = []):

        # variables strictly related to the package itself
        self.package_id = package_id
        self.package_version = package_version
        self.relative_package_location = relative_package_location
        self.relative_readme_location = relative_readme_location
        self.relative_changelog_location = relative_changelog_location

        # leveraged for formatting the link out to the appropriate package manager
        self.repository_args = repository_args

    # is there a changelog present?
    def show_changelog(self):
        return len(self.relative_readme_location) > 0

    # is there a readme present?
    def show_readme(self):
        return len(self.relative_changelog_location) > 0

    # test a remote URL. True if sucessful
    def test_url(self, configuration):

        # leverage test URL if it exists
        if configuration.get_repository_details().get('TestUrl', None):
            url = self.get_formatted_repo_test_url(configuration)
        else:
            url = self.get_formatted_repository_url(configuration)

        try:
            response = requests.get(url)
            if response.status_code == 200:
                return True
        except Exception as err:
            if config.verbose_output:
                print(err)
        return False

    # get the base template URL from the configuration, then fill in the elements
    # from repository_args if necessary
    def get_formatted_repository_url(self, configuration):
        repo_url_template = configuration.get_repository_details()['URL']

        # pull in the package id
        repo_url_template = repo_url_template.format(package_id = self.package_id, package_version = self.package_version)

        if configuration.get_repository_details().get('UrlTransformationFunction', None):
            transformer = configuration.get_repository_details()['UrlTransformationFunction']
        else:
            transformer = lambda input_string: input_string

        # pull in any additional elements through repository_args
        for index, additional_argument in enumerate(self.repository_args):
            repo_url_template = repo_url_template.replace('[' + str(index) + ']', transformer(self.repository_args[index]))

        # possibly need to url encode here.
        return repo_url_template

    def get_formatted_repo_test_url(self, configuration):
        repo_url_template = configuration.get_repository_details()['TestUrl']

        # pull in the package id
        repo_url_template = repo_url_template.format(package_id = self.package_id, package_version = self.package_version)

        if configuration.get_repository_details()['TestUrlTransformationFunction']:
            transformer = configuration.get_repository_details()['TestUrlTransformationFunction']
        else:
            transformer = lambda input_string: input_string

        # pull in any additional elements through repository_args
        for index, additional_argument in enumerate(self.repository_args):
            repo_url_template = repo_url_template.replace('[' + str(index) + ']', transformer(self.repository_args[index]))

        # possibly need to url encode here.
        return repo_url_template

    def get_repository_link_text(self, configuration):
        repo_text_template = configuration.get_repository_details()['Text']

        # pull in the package id
        repo_text_template = repo_text_template.format(package_id = self.package_id, package_version = self.package_version)

        return repo_text_template
