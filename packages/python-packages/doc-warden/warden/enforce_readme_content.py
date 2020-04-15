# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from __future__ import print_function

import os
import markdown2
import bs4
import re
from .warden_common import check_match, walk_directory_for_pattern, get_omitted_files
from .HeaderConstruct import HeaderConstruct
from docutils import core
from docutils.writers.html4css1 import Writer,HTMLTranslator
import logging

import pdb

README_PATTERNS = ['*/readme.md', '*/readme.rst', '*/README.md', '*/README.rst']

# entry point
def verify_readme_content(config):
    all_readmes = walk_directory_for_pattern(config.target_directory, README_PATTERNS, config)
    omitted_readmes = get_omitted_files(config)
    targeted_readmes = [readme for readme in all_readmes if readme not in omitted_readmes]
    known_issue_paths = config.get_known_content_issues()
    section_sorting_dict = config.required_readme_sections

    ignored_missing_readme_paths = []
    readme_results = []
    readmes_with_issues = []

    for readme in targeted_readmes:
        ext = os.path.splitext(readme)[1]
        if ext == '.rst':
            print(readme)
            readme_results.append(verify_rst_readme(readme, config, section_sorting_dict))
        else:
            print(readme)
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

    return (readme, missed_patterns)

# within the entire readme, are there any missing sections that are expected?
def find_missed_sections(html_soup, patterns):
    header_list = html_soup.find_all(re.compile('^h[1-2]$'))

    observed_failing_patterns = recursive_header_search(header_list, patterns, [])

    exit(1)
    return observed_failing_patterns

# within the entire readme, are there any missing sections that are expected?
def recursive_header_search(header_list, patterns, parent_chain=[]):
    unobserved_patterns = []

    for pattern in patterns:
        if isinstance(pattern, dict):
            parent_pattern, child_patterns =  next(iter(pattern.items()))
            matching_headers_for_pattern = match_regex_to_headers(header_list, parent_pattern, parent_chain)

            if matching_headers_for_pattern:
                print('pattern match')
            else:
                unobserved_patterns.extend((parent_pattern, parent_chain))

            parent_chain_for_children = parent_chain + [parent_pattern]
            unobserved_patterns.extend(recursive_header_search(header_list, child_patterns, parent_chain_for_children))
        else:
            matching_headers_for_pattern = match_regex_to_headers(header_list, pattern, parent_chain)
            if matching_headers_for_pattern:
                print('pattern match')
            else:
                unobserved_patterns.extend((pattern, parent_chain))

    return unobserved_patterns

# a set of headers looks like this
# h1
# h2
# h1
# h2
# h3
# h1
# any "indented" headers are children of the one above it IF the
# one above it is at a higher header level (this is actually < in comparison)
# result of above should be a web that looks like
# root
#   h1
#      h2
#   h1
#      h2
#         h3
#   h1
# we will start a search from root every time.
def generate_web(headers):
    previous_header_level = 0
    current_header = None
    root = HeaderConstruct(None, None)
    current_parent = root

    for header in headers:
        # evaluate the level
        current_level = int(header.name.replace('h', ''))

        # h1 < h2 == we need to traverse up
        if current_level < current_parent.level:
            # print("On iteration {}: {} < {}".format(numRun, current_level, current_parent.level))
            current_parent = current_parent.parent
            current_header = HeaderConstruct(header, current_parent)
            current_parent.add_child(current_header)

        # h2 > h1 == we need to indent, add the current as a child, and set parent to current
        # for the forthcoming ones headers
        elif current_level > current_parent.level:
            # print("On iteration {}: {} > {}".format(numRun, current_level, current_parent.level))
            current_header = HeaderConstruct(header, current_parent)
            current_parent.add_child(current_header)
            current_parent = current_header
        # current_header.level == current_parent.level
        # we just need to add it as a child to our current header
        else: 
            # print("On iteration {}: {} == {}".format(numRun, current_level, current_parent.level))
            current_header = HeaderConstruct(header, current_parent)
            current_parent.add_child(current_header)

    pdb.set_trace()
    
    return root

# checks multiple header strings against a single configured pattern
def match_regex_to_headers(headers, pattern, parent_chain):
    # example
    # pattern = D
    # parent_chain = A B 
    # we're looking for a header set that follows A -> B -> D 
    # time to do a search!
    target_headers = parent_chain + [ pattern ]
    header_web = generate_web(headers)
    available_headers = header_web.children
    
    print("Available Headers = {}".format(available_headers))

    while(target_headers and available_headers):
        current_target = target_headers[0]
        target_headers.remove(target_headers[0])

        print("Current target {}".format(current_target))

        # check each of the available nodes (on start, it'll be children of root)
        for header in available_headers:
            print("evaluating {} against regex {}".format(header.tag.get_text(), current_target))
            match = re.search(current_target, header.tag.get_text())
            print(match)

            if match:
                # if we have more to go
                if target_headers:
                    available_headers.extend(header.children)
                    continue
                else:
                    return header # it doesn't really matter what we return here
                    # given that we're only really testing that it exists

    return []

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
