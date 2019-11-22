# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from __future__ import print_function

import os
import markdown2
import bs4
import re
from .warden_common import get_omitted_files
from docutils import core
from docutils.writers.html4css1 import Writer,HTMLTranslator
import pathlib
import logging

from .index_packages import get_python_package_info, get_js_package_info, get_java_package_info, get_net_packages_info

# entry point
def verify_changelog_content(config):
    pkg_list = []
    omitted_changelogs = get_omitted_files(config)
    known_issue_paths = config.get_known_content_issues()
    missing_changelog = []
    empty_release_notes = []
    changelog_results = []


    if config.scan_language == 'net':
        pkg_list = get_net_packages_info(config)

    if config.scan_language == 'java':
        pkg_list = get_java_package_info(config)

    if config.scan_language == 'js':
        pkg_list = get_js_package_info(config)

    if config.scan_language == 'python':
        pkg_list = get_python_package_info(config)

    for pkg in pkg_list:
        changelog_ext = os.path.splitext(pkg.relative_changelog_location)[1]
        pkg_changelog = os.path.normpath(os.path.join(config.target_directory, pkg.relative_changelog_location))

        if os.path.isfile(pkg_changelog) and pkg_changelog not in omitted_changelogs:
            if changelog_ext == '.rst':
                changelog_results.append(verify_rst_changelog(pkg_changelog, config, pkg.package_version))
            else:
                changelog_results.append(verify_md_changelog(pkg_changelog, config, pkg.package_version))

    for changelog_tuple in changelog_results:
        if changelog_tuple[0] in known_issue_paths:
            continue
        if changelog_tuple[1]['curr_pkg_version'] != changelog_tuple[1]['latest_version_entry']:
            missing_changelog.append(changelog_tuple)
        elif len(changelog_tuple[1]['latest_release_notes']) == 0:
            empty_release_notes.append(changelog_tuple)

    return missing_changelog, empty_release_notes, pkg_list

# parse rst to html, check for presence of appropriate version
def verify_rst_changelog(changelog, config, pkg_version):
    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = rst_to_html(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    changelog_check_result = verify_latest_section(html_soup, pkg_version)

    return changelog, changelog_check_result

# parse md to html, check for presence of the latest version
def verify_md_changelog(changelog, config, pkg_version):
    if config.verbose_output:
        print('Examining content in {}'.format(changelog))

    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = markdown2.markdown(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    changelog_check_result = verify_latest_section(html_soup, pkg_version)

    return changelog, changelog_check_result

# within the entire readme, is the current version present
def verify_latest_section(html_soup, pkg_version):
    changelog_check_result = {
        'curr_pkg_version' : pkg_version,
        'latest_version_entry' : html_soup.h2.text,
        'latest_release_notes' : list()
    }

    if changelog_check_result['latest_version_entry'] == pkg_version:
        for sibling in html_soup.h2.next_siblings:
            if sibling.name == 'h2':
                break
            elif sibling.name == 'ul':
                for child in sibling.children:
                    if child.name is not None:
                        changelog_check_result['latest_release_notes'].append(child.text)

    return changelog_check_result

# boilerplate for translating RST
class HTMLFragmentTranslator(HTMLTranslator):
    def __init__(self, document):
        HTMLTranslator.__init__(self, document)
        self.head_prefix = ['','','','','']
        self.body_prefix = []
        self.body_suffix = []
        self.stylesheet = []
    def astext(self):
        return ''.join(self.body)

html_fragment_writer = Writer()
html_fragment_writer.translator_class = HTMLFragmentTranslator

# utilize boilerplate
def rst_to_html(input_rst):
    return core.publish_string(input_rst, writer = html_fragment_writer)