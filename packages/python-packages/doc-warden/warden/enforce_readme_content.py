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

README_PATTERNS = ['*/readme.md', '*/readme.rst', '*/README.md', '*/README.rst']
CODE_FENCE_REGEX = r"\`\`\`([\s\S\n]*?)\`\`\`"

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

    return (readme, missed_patterns)

# parse md to html, check for presence of appropriate sections
def verify_md_readme(readme, config, section_sorting_dict):
    if config.verbose_output:
        print('Examining content in {}'.format(readme))

    with open(readme, 'r', encoding="utf-8-sig") as f:
        readme_content = f.read()
        
    # we need to sanitize to remove the fenced code blocks. The reasoning here is that markdown2 is having issues
    # parsing the pygments style that we use with github.
    sanitized_html_content = re.sub(CODE_FENCE_REGEX, "", readme_content, flags=re.MULTILINE)
    html_readme_content = markdown2.markdown(sanitized_html_content)
    html_soup = bs4.BeautifulSoup(html_readme_content, "html.parser")

    missed_patterns = find_missed_sections(html_soup, config.required_readme_sections)

    return (readme, missed_patterns)

# within the entire readme, are there any missing sections that are expected?
def find_missed_sections(html_soup, patterns):
    header_list = html_soup.find_all(re.compile('^h[1-4]$'))

    flattened_patterns = flatten_pattern_config(patterns)
    header_index = generate_header_index(header_list, flattened_patterns)
    observed_failing_patterns = recursive_header_search(header_index, patterns, [])

    return observed_failing_patterns

# gets a distinct set of ALL patterns present in a config. This is
# important because this allows us to precalculate which patterns a given header tag will match
def flatten_pattern_config(patterns):
    observed_patterns = []

    for pattern in patterns:
        if isinstance(pattern, dict):
            parent_pattern, child_patterns =  next(iter(pattern.items()))

            if child_patterns:
                observed_patterns.extend(flatten_pattern_config(child_patterns))
            observed_patterns.extend([parent_pattern])
        else:
            observed_patterns.extend([pattern])

    return list(set(observed_patterns))

# recursive solution that walks all the rules and generates rule chains from them to test 
# that the tree actually contains sets of headers that meet the required sections
def recursive_header_search(header_index, patterns, parent_pattern_chain=[]):
    unobserved_patterns = []

    if patterns:
        for pattern in patterns:
            if isinstance(pattern, dict):
                parent_pattern, child_patterns =  next(iter(pattern.items()))

                if not match_regex_to_headers(header_index, parent_pattern_chain + [parent_pattern]):
                    unobserved_patterns.append(parent_pattern_chain + [parent_pattern])

                parent_chain_for_children = parent_pattern_chain + [parent_pattern]
                unobserved_patterns.extend(recursive_header_search(header_index, child_patterns, parent_chain_for_children))
            else:
                if not match_regex_to_headers(header_index, parent_pattern_chain + [pattern]):
                    unobserved_patterns.append((parent_pattern_chain + [pattern]))

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
# this function examines a serial set of <h> tags and generates
# an index that allows us to interrogate a specific header for it's containing
# headers.
def generate_header_index(header_constructs, patterns):
    previous_header_level = 0
    current_header = None
    root = HeaderConstruct(None, None)
    current_parent = root
    header_index = []
    previous_node_level = 0

    for index, header in enumerate(header_constructs):
        # evaluate the level
        current_level = int(header.name.replace('h', ''))

        # h1 < h2 == we need to traverse up
        if current_level < current_parent.level:
            current_parent = current_parent.get_parent_by_level(current_level)
            current_header = HeaderConstruct(header, current_parent, patterns)

        # h2 > h1 == we need to indent, add the current as a child, and set parent to current
        # for the forthcoming headers
        elif current_level > current_parent.level:
            current_header = HeaderConstruct(header, current_parent, patterns)

            # only set current_parent if there are children below, which NECESSITATES that 
            # the very next header must A) exist and B) be > current_level
            if index + 1 < len(header_constructs):
                if int(header_constructs[index+1].name.replace('h', '')) > current_level:
                    current_parent = current_header

        # current_header.level == current_parent.level
        # we just need to add it as a child to our current header
        else: 
            if previous_node_level > current_parent.level:
                current_parent = current_parent.get_parent_by_level(current_level)
            current_header = HeaderConstruct(header, current_parent, patterns)

        previous_node_level = current_level

        # always add the header to the node index, we will use it later
        header_index.append(current_header)

    return header_index

# checks the node index for a specific pattern or chain
# [^Getting started$, Install Package] is an example of a required set
def match_regex_to_headers(header_index, target_patterns):
    # we should only be firing this for a "leaf" aka the END of the chain we're looking for, so the last element
    # will always get popped first before we recurse across the rest
    current_target = target_patterns.pop()
    matching_headers = [header for header in header_index if current_target in header.matching_patterns]

    # check all the leaf node parents for the matches. we don't want to artificially constrain though
    # so we have to assume that a rule can match multiple children
    for matching_leaf_header in matching_headers:
        if target_patterns:
            result = check_header_parents(matching_leaf_header, target_patterns[:])
        else:
            return re.search(current_target, matching_leaf_header.get_tag_text())

        if result:
            return matching_leaf_header
        else:
            continue

    return None

# recursively ensure that a header_construct has parents that match the required headers
# the search ALLOWS GAPS, so a match will still be found if
#
# h1
#    h2 (matching header)
#        h3 (unmatched parent header, but this is ok)
#           h4 (matching header)
def check_header_parents(header_construct, required_parent_headers):
    if required_parent_headers:
        target_parent = required_parent_headers.pop()

        new_parent = header_construct.check_parents_for_pattern(target_parent)

        if new_parent:
            if required_parent_headers:
                check_header_parents(header_construct, required_parent_headers)
            else:
                return True
        else:
            return False
    else:
        return False

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
