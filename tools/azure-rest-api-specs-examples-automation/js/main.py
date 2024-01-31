import sys
import urllib.parse
import os
from os import path
import glob
import json
import argparse
import logging
import dataclasses
from typing import List
from enum import Enum

from models import JsExample, JsLintResult
from lint import JsLint


script_path: str = '.'
tmp_path: str

original_file_key: str = '* x-ms-original-file: '

module_relative_path: str = ''


class PackageType(Enum):
    HLC = '@azure/arm-'
    RLC = '@azure-rest/arm-'


@dataclasses.dataclass(eq=True, frozen=True)
class Release:
    tag: str
    package: str
    version: str


@dataclasses.dataclass(eq=True)
class JsExampleMethodContent:
    example_relative_path: str = None
    content: List[str] = None
    line_start: int = None
    line_end: int = None

    def is_valid(self) -> bool:
        return self.example_relative_path is not None


@dataclasses.dataclass(eq=True)
class AggregatedJsExample:
    methods: List[JsExampleMethodContent]
    class_opening: List[str] = None


def is_aggregated_js_example(lines: List[str]) -> bool:
    # check metadata to see if the sample file is a candidate for example extraction

    for line in lines:
        if line.strip().startswith(original_file_key):
            return True
    return False


def get_js_example_method(lines: List[str], start: int, aggregated_with_main: bool) -> JsExampleMethodContent:
    # extract one example method, start from certain line number

    original_file = None
    js_example_method = JsExampleMethodContent()
    for index in range(len(lines)):
        if index < start:
            continue

        line = lines[index]
        if line.strip().startswith(original_file_key):
            original_file = line.strip()[len(original_file_key):]
        elif line.startswith('async function '):
            # begin of method
            js_example_method.example_relative_path = original_file
            js_example_method.line_start = index
        elif '.catch(console.error);' in line \
                or (index > 0 and line.startswith(');') and 'console.error' in lines[index-1]):
            # end of method
            js_example_method.line_end = index + 1
            break
        elif aggregated_with_main and '}' == line.rstrip():
            js_example_method.line_end = index + 1
            break

    if js_example_method.is_valid():
        backtrace_comments(js_example_method, lines, start)

    return js_example_method


def backtrace_comments(js_example_method: JsExampleMethodContent, lines: List[str], start: int):
    # backtrace to include comments before the method declaration

    block_comment = False
    for index in range(js_example_method.line_start - 1, start - 1, -1):
        line = lines[index]
        if block_comment:
            if line.strip().startswith('/*'):
                js_example_method.line_start = index
                block_comment = False
                break
        else:
            if line.strip().startswith('//'):
                js_example_method.line_start = index
            elif line.strip().startswith('*/'):
                js_example_method.line_start = index
                block_comment = True
            else:
                break
    js_example_method.content = lines[js_example_method.line_start:js_example_method.line_end]


def break_down_aggregated_js_example(lines: List[str]) -> AggregatedJsExample:
    # break down sample Js to multiple examples

    # check if it is new style with "main()"
    aggregated_with_main = len([s for s in lines if 'async function main()' in s]) > 0

    aggregated_js_example = AggregatedJsExample([])
    js_example_method = get_js_example_method(lines, 0, aggregated_with_main)
    line_start = js_example_method.line_start
    line_end = js_example_method.line_end
    while js_example_method.is_valid():
        aggregated_js_example.methods.append(js_example_method)
        line_end = js_example_method.line_end
        js_example_method = get_js_example_method(lines, js_example_method.line_end, aggregated_with_main)
    aggregated_js_example.class_opening = lines[0:line_start]
    aggregated_js_example.class_closing = lines[line_end:]

    if aggregated_with_main:
        # remove "dotenv.config()"
        aggregated_js_example.class_opening = [s for s in aggregated_js_example.class_opening
                                               if 'require("dotenv").config();' not in s]

    return aggregated_js_example


def format_js(lines: List[str]) -> List[str]:
    # format example as Js code

    new_lines = []
    skip_head = True
    for line in lines:
        if not skip_head:
            # use new class name
            new_lines.append(line)
        else:
            # start with require
            if 'require(' in line:
                new_lines.append(line)
                skip_head = False

    return new_lines


def process_js_example(filepath: str, package_type: PackageType) -> List[JsExample]:
    # process aggregated Js sample to examples

    filename = path.basename(filepath)
    logging.info(f'Processing Js aggregated sample: {filename}')

    with open(filepath, encoding='utf-8') as f:
        lines = f.readlines()

    example_folder_extension = get_example_folder_extension(package_type)

    js_examples = []
    if is_aggregated_js_example(lines):
        aggregated_js_example = break_down_aggregated_js_example(lines)
        for js_example_method in aggregated_js_example.methods:
            if js_example_method.is_valid():
                logging.info(f'Processing Js example: {js_example_method.example_relative_path}')

                # re-construct the example class, from example method
                example_lines = aggregated_js_example.class_opening + js_example_method.content

                example_filepath = js_example_method.example_relative_path
                example_dir, example_filename = path.split(example_filepath)

                example_lines = format_js(example_lines)

                filename = example_filename.split('.')[0]
                # use the examples-js folder for Js example
                md_dir = (example_dir + '-' + example_folder_extension) if example_dir.endswith('/examples') \
                    else example_dir.replace('/examples/', f'/examples-{example_folder_extension}/')

                js_example = JsExample(filename, md_dir, ''.join(example_lines))
                js_examples.append(js_example)

    return js_examples


def validate_js_examples(js_module: str, package_json_path: str, js_examples: List[JsExample]) -> JsLintResult:
    # batch validate Js examples

    global script_path

    lint_config_path = path.join(script_path, 'lint', '.eslintrc.json')
    js_lint = JsLint(tmp_path, js_module, package_json_path, lint_config_path, js_examples)
    js_lint_result = js_lint.lint()

    return js_lint_result


def generate_examples(release: Release, sdk_examples_path: str, js_examples: List[JsExample]) -> List[str]:
    # generate code and metadata from Js examples

    global module_relative_path

    files = []
    for js_example in js_examples:
        escaped_release_tag = urllib.parse.quote(release.tag, safe='')
        doc_link = f'https://github.com/Azure/azure-sdk-for-js/blob/{escaped_release_tag}/' \
                   f'{module_relative_path}/README.md'

        files.extend(write_code_to_file(sdk_examples_path, js_example.target_dir, js_example.target_filename, '.js',
                                        js_example.content, doc_link))
    return files


def write_code_to_file(sdk_examples_path: str, target_dir: str, filename_root: str, filename_ext: str,
                       code_content: str, sdk_url: str) -> List[str]:
    # write code file and metadata file

    code_filename = filename_root + filename_ext
    metadata_filename = filename_root + '.json'

    metadata_json = {'sdkUrl': sdk_url}

    target_dir_path = path.join(sdk_examples_path, target_dir)
    os.makedirs(target_dir_path, exist_ok=True)

    code_file_path = path.join(target_dir_path, code_filename)
    with open(code_file_path, 'w', encoding='utf-8') as f:
        f.write(code_content)
    logging.info(f'Code written to file: {code_file_path}')

    metadata_file_path = path.join(target_dir_path, metadata_filename)
    with open(metadata_file_path, 'w', encoding='utf-8') as f:
        json.dump(metadata_json, f)
    logging.info(f'Metadata written to file: {metadata_file_path}')

    return [path.join(target_dir, code_filename),
            path.join(target_dir, metadata_filename)]


def create_js_examples(release: Release,
                       js_module: str,
                       sdk_examples_path: str, js_examples_path: str) -> (bool, List[str]):
    js_paths = []
    for root, dirs, files in os.walk(js_examples_path):
        for name in files:
            filepath = path.join(root, name)
            if path.splitext(filepath)[1] == '.js' and filepath.endswith('Sample.js'):
                js_paths.append(filepath)

    package_type = get_package_type(release)
    logging.info(f'Processing SDK examples: {release.package}')
    js_examples = []
    for filepath in js_paths:
        js_examples += process_js_example(filepath, package_type)

    files = []
    if js_examples:
        logging.info('Validating SDK examples')
        package_json_path = path.join(js_examples_path, 'package.json')
        js_lint_result = validate_js_examples(js_module, package_json_path, js_examples)

        if js_lint_result.succeeded:
            files = generate_examples(release, sdk_examples_path, js_lint_result.examples)
        else:
            logging.error('Validation failed')

        return js_lint_result.succeeded, files
    else:
        logging.info('SDK examples not found')
        return True, files


def get_module_relative_path(sdk_name: str, package_type: PackageType, sdk_path: str) -> str:
    global module_relative_path
    sdk_prefix = 'arm-'
    sdk_suffix = '-rest' if package_type is PackageType.RLC else ''
    module_relative_path = path.join('sdk', sdk_name, sdk_prefix + sdk_name + sdk_prefix)
    if not path.isdir(path.join(sdk_path, module_relative_path)):
        candidate_sdk_readmes = glob.glob(path.join(sdk_path, f'sdk/*/{sdk_prefix}{sdk_name}{sdk_suffix}'))
        if len(candidate_sdk_readmes) > 0:
            candidate_sdk_readmes = [path.relpath(p, sdk_path) for p in candidate_sdk_readmes]
            logging.info(
                f'SDK folder {module_relative_path} not found, use first item of f{candidate_sdk_readmes}')
            module_relative_path = candidate_sdk_readmes[0]
        else:
            raise RuntimeError(f'Source folder not found for SDK {sdk_prefix}{sdk_name}{sdk_suffix}')
    return module_relative_path


def get_sample_version(release_version: str) -> str:
    version = 'v' + release_version.split('.')[0]
    if '-beta' in release_version:
        version += '-beta'
    return version


def get_package_type(release: Release) -> PackageType:
    if release.package.startswith(PackageType.HLC.value):
        return PackageType.HLC
    else:
        return PackageType.RLC


def get_example_folder_extension(package_type: PackageType) -> str:
    return 'js' if package_type is PackageType.HLC else 'js-rlc'


def main():
    global script_path
    global tmp_path

    logging.basicConfig(level=logging.INFO,
                        format='%(asctime)s [%(levelname)s] %(message)s',
                        datefmt='%Y-%m-%d %X')

    script_path = path.abspath(path.dirname(sys.argv[0]))

    parser = argparse.ArgumentParser(description='Requires 2 arguments, path of "input.json" and "output.json".')
    parser.add_argument('paths', metavar='path', type=str, nargs=2,
                        help='path of "input.json" or "output.json"')
    args = parser.parse_args()
    input_json_path = args.paths[0]
    output_json_path = args.paths[1]
    with open(input_json_path, 'r', encoding='utf-8') as f_in:
        config = json.load(f_in)

    sdk_path = config['sdkPath']
    sdk_examples_path = config['sdkExamplesPath']
    tmp_path = config['tempPath']

    release = Release(config['release']['tag'],
                      config['release']['package'],
                      config['release']['version'])

    package_type = get_package_type(release)
    if package_type is PackageType.HLC:
        sdk_name = release.package[len(PackageType.HLC.value):]
    else:
        sdk_name = release.package[len(PackageType.RLC.value):]

    js_module = f'{release.package}@{release.version}'
    sample_version = get_sample_version(release.version)

    module_relative_path_local = get_module_relative_path(sdk_name, package_type, sdk_path)
    js_examples_relative_path = path.join(module_relative_path_local,
                                          'samples', sample_version, 'javascript')
    js_examples_path = path.join(sdk_path, js_examples_relative_path)

    succeeded, files = create_js_examples(release, js_module, sdk_examples_path, js_examples_path)

    with open(output_json_path, 'w', encoding='utf-8') as f_out:
        output = {
            'status': 'succeeded' if succeeded else 'failed',
            'name': js_module,
            'files': files
        }
        json.dump(output, f_out, indent=2)


if __name__ == '__main__':
    main()
