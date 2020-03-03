# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from __future__ import print_function

import os
import markdown2
import bs4
import re
from .warden_common import check_match, walk_directory_for_pattern, get_omitted_files
from docutils import core
from docutils.writers.html4css1 import Writer,HTMLTranslator
import logging

README_PATTERNS = ['*/readme.md', '*/readme.rst', '*/README.md', '*/README.rst']

# entry point
def verify_readme_content(config):
    all_readmes = walk_directory_for_pattern(config.target_directory, README_PATTERNS, config)
    omitted_readmes = get_omitted_files(config)
    targeted_readmes = [readme for readme in all_readmes if readme not in omitted_readmes]
    known_issue_paths = config.get_known_content_issues()
    section_sorting_dict = config.get_readme_sections_dictionary()

    ignored_missing_readme_paths = []
    readme_results = []
    readmes_with_issues = []

    for readme in targeted_readmes:
        ext = os.path.splitext(readme)[1]
        if ext == '.rst':
            readme_results.append(verify_rst_readme(readme, config, section_sorting_dict))
        else:
            readme_results.append(verify_md_readme(readme, config, section_sorting_dict))

    for readme_tuple in readme_results:
        if readme_tuple[1]:
            if readme_tuple[0] in known_issue_paths:
                ignored_missing_readme_paths.append(readme_tuple)
            else:
                readmes_with_issues.append(readme_tuple)

    return readmes_with_issues, ignored_missing_readme_paths

# parse rst to html, check for presence of appropriate sections
def verify_rst_readme(readme, config, section_sorting_dict):
    with open(readme, 'r', encoding="utf-8") as f:
        readme_content = f.read()
    html_readme_content = rst_to_html(readme_content)
    html_soup = bs4.BeautifulSoup(html_readme_content, "html.parser")

    missed_patterns = find_missed_sections(html_soup, config.required_readme_sections)
    missed_patterns.sort(key=lambda pattern: section_sorting_dict[pattern])

    return (readme, missed_patterns)

# parse md to html, check for presence of appropriate sections
def verify_md_readme(readme, config, section_sorting_dict):
    if config.verbose_output:
        print('Examining content in {}'.format(readme))

    with open(readme, 'r', encoding="utf-8") as f:
        readme_content = f.read()
    html_readme_content = markdown2.markdown(readme_content)
    html_soup = bs4.BeautifulSoup(html_readme_content, "html.parser")

    missed_patterns = find_missed_sections(html_soup, config.required_readme_sections)
    missed_patterns.sort(key=lambda pattern: section_sorting_dict[pattern])

    return (readme, missed_patterns)

# within the entire readme, are there any missing sections that are expected?
def find_missed_sections(html_soup, patterns):
    headers = html_soup.find_all(re.compile('^h[1-2]$'))
    missed_patterns = []
    observed_patterns = []

    for header in headers:
        observed_patterns.extend(match_regex_set(header.get_text(), patterns))

    return list(set(patterns) - set(observed_patterns))

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
