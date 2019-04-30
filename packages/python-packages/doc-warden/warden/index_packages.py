# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function

from .warden_common import check_match, walk_directory_for_pattern, get_omitted_files, get_java_package_roots, get_net_packages, get_python_package_roots, get_js_package_roots, find_alongside_file, find_below_file, parse_pom, is_java_pom_package_pom, find_above_file
from .PackageInfo import PackageInfo
import json
import os
import ast
from jinja2 import Template
import xml.etree.ElementTree as ET
import textwrap
import re
import fnmatch

# python 3 transitioned StringIO to be part of `io` module. 
# python 2 needs the old version however
try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

# chose to go with in-line templates. reasoning here is that the whitespace is a bit more important for markdown
# given that all the content fitting on a single line is important, leveraging the format string method makes for a 
# much simpler to maintain template
PKGID_COL = ' [`{{ pkg.package_id }}`]( {{ pkg.relative_package_location }} )'
RM_COL = ' {% if len(pkg.relative_readme_location) > 0 %}[Readme]({{ pkg.relative_readme_location }}){% else %} N/A {% endif %} '
CL_COL = ' {% if len(pkg.relative_changelog_location) > 0 %}[Changelog]({{ pkg.relative_changelog_location }}){% else %} N/A {% endif %} '
GROUPID_COL = ' `{{ pkg.repository_args[0] }}` '
REPO_COL = ' `[Repo Link]( {{ pkg.relative_package_location }}` )'
PUBLISH_COL = ' {% if pkg.test_url(config) %}[{{ pkg.get_repository_link_text(config) }}]( {{ pkg.get_formatted_repository_url(config) }} ){% else %} N/A {% endif %} '
COLUMN_LOOP = '{% for pkg in packages %}'
COLUMN_TEMPLATE = '{0}|{1}|{2}|{3}|{4}|'.format(COLUMN_LOOP, PKGID_COL, RM_COL, CL_COL, PUBLISH_COL)
JAVA_COLUMN_TEMPLATE = '{0}|{1}|{2}|{3}|{4}|'.format(COLUMN_LOOP, PKGID_COL, GROUPID_COL, RM_COL, PUBLISH_COL)

OUTPUT_HEADER = """
# Package Index - {{ title }}

| Package Id     | Readme    | Changelog                 | Published Url       |
|----------------|-----------|---------------------------|---------------------|
"""

JAVA_OUTPUT_HEADER = """
# Package Index - {{ title }}

| Package Id     | GroupId   | Readme    | Published Url       |
|----------------|-----------|-----------|---------------------|
"""

COLUMN_OUTPUT_FOOTER = """
{% endfor %}
"""

# model for "context" of the output template
#  title = <repo-name> (target directory dirname() perhaps
#  packages = [ PackageInfo, PackageInfo, ... ]
#  len = wrapper for len(string)
OUTPUT_TEMPLATE = OUTPUT_HEADER + COLUMN_TEMPLATE + COLUMN_OUTPUT_FOOTER
JAVA_OUTPUT_TEMPLATE = JAVA_OUTPUT_HEADER + JAVA_COLUMN_TEMPLATE + COLUMN_OUTPUT_FOOTER

# given a set of package roots (omitted packages are included for indexing), extract a set of common metadata
# to use when generated a package index.
def index_packages(configuration):
    language_selector = {
        'python': get_python_package_info,
        'js': get_js_package_info,
        'java': get_java_package_info,
        'net': get_net_packages_info
    }

    return language_selector.get(configuration.scan_language.lower(), unrecognized_option)(configuration)

# leverages python AST to parse arguments to `setup.py` and return a list of all indexed packages 
# within the target directory
def get_python_package_info(config):
    pkg_list = []
    pkg_locations, ignored_pkg_locations = get_python_package_roots(config)

    for pkg_file in (pkg_locations + ignored_pkg_locations):
        pkg_id, version = parse_setup(config, pkg_file)

        # package is badly formatted setup. ignore.
        if pkg_id is None:
            continue

        changelog = find_below_file('changelog.md', pkg_file)
        if changelog is None:
            changelog = find_below_file('history.rst', pkg_file)

        readme = find_below_file('readme.md', pkg_file)
        if readme is None:
            readme = find_below_file('readme.rst', pkg_file)

        if changelog:
            changelog_relpath = webify_relative_path(os.path.relpath(changelog, config.target_directory))
        else:
            changelog_relpath = ''

        if readme:
            readme_relpath = webify_relative_path(os.path.relpath(readme, config.target_directory))
        else:
            readme_relpath = ''

        pkg_location = webify_relative_path(os.path.relpath(pkg_file, config.target_directory))

        if(pkg_id not in config.package_indexing_exclusion_list):
            pkg_list.append(PackageInfo(
                package_id = pkg_id, 
                package_version = version, 
                relative_package_location = pkg_location,
                relative_readme_location = readme_relpath or '',
                relative_changelog_location = changelog_relpath or '',
                repository_args = []
                ))

    return pkg_list

# leverages JSON parsing of any packages.json files and returns a list of all indexed packages found
# within the target directory
def get_js_package_info(config):
    pkg_list = []
    pkg_locations, ignored_pkg_locations = get_js_package_roots(config)

    for pkg_file in (pkg_locations + ignored_pkg_locations):
        with open(pkg_file, 'r') as read_file:
            pkg_json = json.load(read_file)

        target_directory = os.path.dirname(pkg_file)

        changelog = find_below_file('changelog.md', pkg_file)
        readme = find_below_file('readme.md', pkg_file)

        if changelog:
            changelog_relpath = webify_relative_path(os.path.relpath(changelog, config.target_directory))
        else:
            changelog_relpath = ''

        if readme:
            readme_relpath = webify_relative_path(os.path.relpath(readme, config.target_directory))
        else:
            readme_relpath = ''

        pkg_location = webify_relative_path(os.path.relpath(pkg_file, config.target_directory))

        if(pkg_json['name'] not in config.package_indexing_exclusion_list):
            pkg_list.append(PackageInfo(
                package_id = pkg_json['name'], 
                package_version = pkg_json['version'], 
                relative_package_location = pkg_location,
                relative_readme_location = readme_relpath or '',
                relative_changelog_location = changelog_relpath or '',
                repository_args = []
                ))

    return pkg_list

# given a pom file, maven `groupId` is usually present at the same level as the `artifactId`
# however, this is not always the case. When this instance occurs, we instead look for the parent `groupId`
def resolve_java_group_id(pom_root):
    parent_id_root = pom_root.find('parent')
    group_id_root = pom_root.find('groupId')

    # there isn't a groupId at the same level as version and artifactId
    if group_id_root is None:
        if parent_id_root:
            parent_group_id = parent_id_root.find('groupId')
            if parent_group_id:
                return parent_group_id.text
    else:
        return group_id_root.text
    return ''

# parses all pom files within the target directory, and returns a list of `jar` packages found within
def get_java_package_info(config):
    pkg_list = []
    pkg_locations, ignored_pkg_locations = get_java_package_roots(config)
    for pkg_file in (pkg_locations + ignored_pkg_locations):
        with open(pkg_file, 'r') as read_file:
            root = parse_pom(pkg_file)

        target_directory = os.path.dirname(pkg_file)

        changelog = find_below_file('changelog.md', pkg_file)
        readme = find_below_file('readme.md', pkg_file)

        if changelog:
            changelog_relpath = webify_relative_path(os.path.relpath(changelog, config.target_directory))
        else:
            changelog_relpath = ''

        if readme:
            readme_relpath = webify_relative_path(os.path.relpath(readme, config.target_directory))
        else:
            readme_relpath = ''

        pkg_location = webify_relative_path(os.path.relpath(pkg_file, config.target_directory))

        artifact_root = root.find('artifactId')
        version_root = root.find('version')
        group_id = resolve_java_group_id(root)

        if artifact_root is None or version_root is None or not group_id:
            if config.verbose_output:
                print("{} has is missing a version, artifactId, or groupId".format(pkg_file))
            continue

        if(artifact_root.text not in config.package_indexing_exclusion_list):
            pkg_list.append(PackageInfo(
                package_id = artifact_root.text,
                package_version = version_root.text,
                relative_package_location = pkg_location,
                relative_readme_location = readme_relpath or '',
                relative_changelog_location = changelog_relpath or '',
                repository_args = [group_id]
                ))
    return pkg_list

# finds .net packages (non-test CSProjs) and attempts to correlate the packageinfo details
# returns a list of all `packages` found within the target directory.
def get_net_packages_info(config):
    pkg_list = []
    pkg_locations, ignored_pkg_locations = get_net_packages(config)

    for pkg_file in (pkg_locations + ignored_pkg_locations):
        pkg_name = os.path.splitext(os.path.basename(pkg_file))[0]
        if(pkg_name not in config.package_indexing_exclusion_list):
            changelog = find_above_file('changelog.md', pkg_file, config.get_package_indexing_traversal_stops() + [os.path.normpath(config.target_directory)], net_early_exit)
            readme = find_above_file('readme.md', pkg_file, config.get_package_indexing_traversal_stops() + [os.path.normpath(config.target_directory)], net_early_exit)

            if changelog:
                changelog_relpath = webify_relative_path(os.path.relpath(changelog, config.target_directory))
            else:
                changelog_relpath = ''

            if readme:
                readme_relpath = webify_relative_path(os.path.relpath(readme, config.target_directory))
            else:
                readme_relpath = ''

            pkg_location = webify_relative_path(os.path.relpath(pkg_file, config.target_directory))

            pkg_list.append(PackageInfo(
                package_id = pkg_name, 
                package_version = '', 
                relative_package_location = pkg_location,
                relative_readme_location = readme_relpath or '',
                relative_changelog_location = changelog_relpath or '',
                repository_args = []
                ))

    return pkg_list

# used after scanning a directory for readme. If this returns true,
# we shouldn't traverse higher up the tree.
def net_early_exit(path):
    if path is None:
        return False

    rule = re.compile(fnmatch.translate('*.sln'))

    for file in os.listdir(path):
        if rule.match(file):
            return True

    return False

# windows outputs paths with `\`, but that really needs to be `/` to work as a url
# given that this is a cross-platform package, we will manually handle this here.
def webify_relative_path(path):
    path_corrected = path.replace('\\', '/')
    return path_corrected

# entrypoint for rendering the packages.md
# handles the template selection and execution
def render(config, pkg_list):
    language_selector = {
        'python': OUTPUT_TEMPLATE,
        'js': OUTPUT_TEMPLATE,
        'java': JAVA_OUTPUT_TEMPLATE,
        'net': OUTPUT_TEMPLATE
    }

    template = language_selector.get(config.scan_language.lower(), unrecognized_option)
    render_template(config, pkg_list, template)

# implementation of the jinja2 template substitution. given a packagelist, generates
# packages.md rows.
def render_template(config, pkg_list, template):
    template = Template(template)
    pkg_list.sort(key=lambda x: x.package_id)

    get_len = lambda string: len(string)

    rendered_template = template.render(title=os.path.basename(config.target_directory), packages=pkg_list, config=config, len=get_len)

    with open(config.package_index_output_location, 'w') as packages_file:
        packages_file.write(rendered_template)

def unrecognized_option(configuration):
    print('Argument {} provided is not a supported language.'.format(configuration.scan_language))
    exit(1)

# opens setup.py and leverages AST to intercept the parameters TO setup.py 
# this easily allows us to examine the values that may originate from outside this file (like VERSION)
def parse_setup(config, setup_filename):
    mock_setup = textwrap.dedent('''\
    def setup(*args, **kwargs):
        __setup_calls__.append((args, kwargs))
    ''')

    parsed_mock_setup = ast.parse(mock_setup, filename=setup_filename)
    with open(setup_filename, 'rt') as setup_file:
        try:
            parsed = ast.parse(setup_file.read())
        except:
            if config.verbose_output:
                print('{} was unparsable.'.format(setup_filename))
            return None, None
        for index, node in enumerate(parsed.body[:]):
            if (
                not isinstance(node, ast.Expr) or
                not isinstance(node.value, ast.Call) or
                not hasattr(node.value.func, 'id') or
                node.value.func.id != 'setup'
            ):
                continue
            parsed.body[index:index] = parsed_mock_setup.body
            break

    fixed = ast.fix_missing_locations(parsed)
    codeobj = compile(fixed, setup_filename, 'exec')
    local_vars = {}
    global_vars = {'__setup_calls__': []}
    current_dir = os.getcwd()
    working_dir = os.path.dirname(setup_filename)
    os.chdir(working_dir)
    exec(codeobj, global_vars, local_vars)
    os.chdir(current_dir)
    try:
        _, kwargs = global_vars['__setup_calls__'][0]
    except:
        if config.verbose_output:
            print('{} had no kwargs'.format(setup_filename))
        return None, None

    version = kwargs['version']
    pkg_id = kwargs['name']

    return pkg_id, version
