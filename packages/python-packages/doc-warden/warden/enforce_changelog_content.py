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
import logging

from .index_packages import get_python_package_info, get_js_package_info, get_java_package_info, get_net_package_info

# entry point
def verify_changelog_content(config, pkg_list):
    omitted_changelogs = get_omitted_files(config)
    known_issue_paths = config.get_known_content_issues()
    missing_changelog = []
    empty_release_notes = []
    changelog_results = []

    for pkg in pkg_list:
        if pkg.relative_changelog_location == '': continue
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

    return missing_changelog, empty_release_notes

# parse rst to html, check for presence of appropriate version
def verify_rst_changelog(changelog, config, pkg_version):
    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = rst_to_html(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    changelog_check_result = verify_latest_rst_section(html_soup, pkg_version)

    return changelog, changelog_check_result

# parse md to html, check for presence of the latest version
def verify_md_changelog(changelog, config, pkg_version):
    if config.verbose_output:
        print('Examining content in {}'.format(changelog))

    with open(changelog, 'r', encoding="utf-8") as f:
        changelog_content = f.read()
    html_changelog_content = markdown2.markdown(changelog_content)
    html_soup = bs4.BeautifulSoup(html_changelog_content, "html.parser")

    changelog_check_result = verify_latest_md_section(html_soup, pkg_version)

    return changelog, changelog_check_result

# get details from latest changelog entry
def verify_latest_rst_section(html_soup, pkg_version):
    changelog_check_result = {
        'curr_pkg_version' : pkg_version,
        'latest_version_entry' : '',
        'latest_release_notes' : list()
    }

    latest_entry = html_soup.find('div', id='unreleased')
    if latest_entry == None: latest_entry = html_soup.find('div', id='id1')
    if latest_entry == None: latest_entry = html_soup.find('div', id='release-history')

    if latest_entry != None:
        for entry in latest_entry:
            if entry.name == 'h1' or entry.name == 'h2':
                changelog_check_result['latest_version_entry'] = entry.text.split(' ')[0]
            if changelog_check_result['latest_version_entry'] == pkg_version:
                if entry.name == 'p':
                    changelog_check_result['latest_release_notes'].append(entry.text)
                if entry.name == 'ul':
                    for child in entry.children:
                        if child.string != '\n': changelog_check_result['latest_release_notes'].append(child.string)

    return changelog_check_result

# get details from latest changelog entry
def verify_latest_md_section(html_soup, pkg_version):
    changelog_check_result = {
        'curr_pkg_version' : pkg_version,
        'latest_version_entry' : '',
        'latest_release_notes' : list()
    }

    latest_version_pattern = r'{0}.*'.format(pkg_version)

    latest_version_tag = html_soup.find('h2', text=re.compile(latest_version_pattern))
    if latest_version_tag is None: return changelog_check_result
    changelog_check_result['latest_version_entry'] = latest_version_tag.text.split(' ')[0]
    for sibling in latest_version_tag.next_siblings:
        if sibling.name == 'h2':
            break
        elif sibling.name == 'ul':
           for child in sibling.children:
                if child.name is not None: changelog_check_result['latest_release_notes'].append(child.text)
        else:
            if sibling.name is not None: changelog_check_result['latest_release_notes'].append(sibling.text)

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