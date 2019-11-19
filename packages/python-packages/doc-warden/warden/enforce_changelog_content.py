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
    ignored_missing_changelog_paths = []
    changelog_results = []
    changelogs_with_issues = []

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
        pkg_changelog = os.path.join(os.path.normpath(config.target_directory), pkg.relative_changelog_location)

        if os.path.isfile(pkg_changelog) and pkg_changelog not in omitted_changelogs:
            if changelog_ext == '.rst':
                changelog_results.append(verify_rst_changelog(pkg_changelog, config, [pkg.package_version]))
            else:
                changelog_results.append(verify_md_changelog(pkg_changelog, config, [pkg.package_version]))

    for changelog_tuple in changelog_results:
        if changelog_tuple[1]:
            if changelog_tuple[0] in known_issue_paths:
                ignored_missing_changelog_paths.append(changelog_tuple)
            else:
                changelogs_with_issues.append(changelog_tuple)

    return changelogs_with_issues, ignored_missing_changelog_paths

# parse rst to html, check for presence of appropriate version
def verify_rst_changelog(changelog, config, pkg_version):
    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = rst_to_html(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    missed_patterns = find_missed_sections(html_soup, pkg_version)

    return (changelog, missed_patterns)

# parse md to html, check for presence of appropriate version
def verify_md_changelog(changelog, config, pkg_version):
    if config.verbose_output:
        print('Examining content in {}'.format(changelog))

    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = markdown2.markdown(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    missed_patterns = find_missed_sections(html_soup, pkg_version)

    return (changelog, missed_patterns)

# within the entire readme, is the current version present
def find_missed_sections(html_soup, pkg_version):
    headers = html_soup.find_all(re.compile('^h[1-2]$'))
    missed_patterns = []
    observed_patterns = []

    for header in headers:
        observed_patterns.extend(match_regex_set(header.get_text(), pkg_version))

    return list(set(pkg_version) - set(observed_patterns))

# checks a header string against a set of configured patterns
def match_regex_set(header, patterns):
    matching_patterns = []
    for pattern in patterns:
        result = re.search(pattern, header)
        if result:
            matching_patterns.append(pattern)
            break

    return matching_patterns

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