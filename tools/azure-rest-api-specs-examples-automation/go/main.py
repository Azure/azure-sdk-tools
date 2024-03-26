import sys
import urllib.parse
import os
from os import path
import json
import argparse
import logging
import dataclasses
from typing import List

from models import GoExample, GoVetResult
from validate import GoVet


script_path: str = '.'
tmp_path: str

original_file_key = '// Generated from example definition: '


@dataclasses.dataclass(eq=True, frozen=True)
class Release:
    tag: str
    package: str
    version: str


@dataclasses.dataclass(eq=True)
class GoExampleMethodContent:
    example_relative_path: str = None
    content: List[str] = None
    line_start: int = None
    line_end: int = None

    def is_valid(self) -> bool:
        return self.example_relative_path is not None


@dataclasses.dataclass(eq=True)
class AggregatedGoExample:
    methods: List[GoExampleMethodContent]
    class_opening: List[str] = None


def is_aggregated_go_example(lines: List[str]) -> bool:
    # check metadata to see if the sample file is a candidate for example extraction

    for line in lines:
        if line.strip().startswith(original_file_key):
            return True
    return False


def parse_original_file(original_file: str) -> str:
    if original_file.startswith('https://'):
        spec_main_segment = 'https://github.com/Azure/azure-rest-api-specs/tree/main/'
        if original_file.startswith(spec_main_segment):
            original_file = original_file[len(spec_main_segment):]
        else:
            specification_index = original_file.find('specification/')
            if specification_index != -1:
                original_file = original_file[specification_index:]
            else:
                logging.error(f'Parse relative path from URI {original_file} failed')
                original_file = None
    return original_file


def get_go_example_method(lines: List[str], start: int) -> GoExampleMethodContent:
    # extract one example method, start from certain line number

    original_file = None
    go_example_method = GoExampleMethodContent()
    for index in range(len(lines)):
        if index < start:
            continue

        line = lines[index]
        if line.strip().startswith(original_file_key):
            original_file = line.strip()[len(original_file_key):]
            original_file = parse_original_file(original_file)
        elif line.startswith('func '):
            # begin of method
            go_example_method.example_relative_path = original_file
            go_example_method.line_start = index
        elif line.startswith('}'):
            # end of method
            go_example_method.line_end = index + 1
            break

    if go_example_method.is_valid():
        # backtrace to include comments before the method declaration
        for index in range(go_example_method.line_start - 1, start - 1, -1):
            line = lines[index]
            if line.strip().startswith('//'):
                go_example_method.line_start = index
            else:
                break
        go_example_method.content = lines[go_example_method.line_start:go_example_method.line_end]

    return go_example_method


def break_down_aggregated_go_example(lines: List[str]) -> AggregatedGoExample:
    # break down sample Go to multiple examples

    aggregated_go_example = AggregatedGoExample([])
    go_example_method = get_go_example_method(lines, 0)
    line_start = go_example_method.line_start
    line_end = go_example_method.line_end
    while go_example_method.is_valid():
        aggregated_go_example.methods.append(go_example_method)
        line_end = go_example_method.line_end
        go_example_method = get_go_example_method(lines, go_example_method.line_end)
    aggregated_go_example.class_opening = lines[0:line_start]
    aggregated_go_example.class_closing = lines[line_end:]
    return aggregated_go_example


def format_go(lines: List[str]) -> List[str]:
    # format example as Go code

    new_lines = []
    skip_head = True
    for line in lines:
        if not skip_head:
            new_lines.append(line)
        else:
            # start with package
            if line.startswith('package '):
                new_lines.append(line)
                skip_head = False

    return new_lines


def process_go_example(filepath: str) -> List[GoExample]:
    # process aggregated Go sample to examples

    filename = path.basename(filepath)
    logging.info(f'Processing Go aggregated sample: {filename}')

    with open(filepath, encoding='utf-8') as f:
        lines = f.readlines()

    go_examples = []
    if is_aggregated_go_example(lines):
        aggregated_go_example = break_down_aggregated_go_example(lines)
        for go_example_method in aggregated_go_example.methods:
            if go_example_method.is_valid():
                logging.info(f'Processing Go example: {go_example_method.example_relative_path}')

                # re-construct the example class, from example method
                example_lines = aggregated_go_example.class_opening + go_example_method.content

                example_filepath = go_example_method.example_relative_path
                example_dir, example_filename = path.split(example_filepath)

                example_lines = format_go(example_lines)

                filename = example_filename.split('.')[0]
                # use the examples-go folder for Go example
                md_dir = (example_dir + '-go') if example_dir.endswith('/examples') \
                    else example_dir.replace('/examples/', '/examples-go/')

                go_example = GoExample(filename, md_dir, ''.join(example_lines))
                go_examples.append(go_example)

    return go_examples


def validate_go_examples(go_module: str, go_mod_filepath: str, go_examples: List[GoExample]) -> GoVetResult:
    # batch validate Go examples

    go_mod = None
    if path.isfile(go_mod_filepath):
        with open(go_mod_filepath, encoding='utf-8') as f:
            go_mod = f.read()

    go_vet = GoVet(tmp_path, go_module, go_mod, go_examples)
    go_vet_result = go_vet.vet()

    return go_vet_result


def generate_examples(release: Release, sdk_examples_path: str, go_examples: List[GoExample]) -> List[str]:
    # generate code and metadata from Go examples

    files = []
    for go_example in go_examples:
        escaped_release_tag = urllib.parse.quote(release.tag, safe='')
        doc_link = f'https://github.com/Azure/azure-sdk-for-go/blob/{escaped_release_tag}/' \
                   f'{release.package}/README.md'
        files.extend(write_code_to_file(sdk_examples_path, go_example.target_dir, go_example.target_filename, '.go',
                                        go_example.content, doc_link))
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


def create_go_examples(release: Release,
                       go_module: str, go_mod_filepath: str,
                       sdk_examples_path: str, go_examples_path: str) -> (bool, List[str]):
    go_paths = []
    for root, dirs, files in os.walk(go_examples_path):
        for name in files:
            filepath = path.join(root, name)
            if path.splitext(filepath)[1] == '.go' and filepath.endswith('_test.go'):
                go_paths.append(filepath)

    logging.info(f'Processing SDK examples: {release.package}')
    go_examples = []
    for filepath in go_paths:
        go_examples += process_go_example(filepath)

    files = []
    if go_examples:
        logging.info('Validating SDK examples')
        go_vet_result = validate_go_examples(go_module, go_mod_filepath, go_examples)

        if go_vet_result.succeeded:
            files = generate_examples(release, sdk_examples_path, go_vet_result.examples)
        else:
            logging.error('Validation failed')

        return go_vet_result.succeeded, files
    else:
        logging.info('SDK examples not found')
        return True, files


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

    go_module_major_suffix = '' if release.version.startswith('v0.') or release.version.startswith('v1.')\
        else f'/{release.version.split(".")[0]}'
    go_module = f'github.com/Azure/azure-sdk-for-go/{release.package}{go_module_major_suffix}@{release.version}'

    go_examples_relative_path = release.package
    go_examples_path = path.join(sdk_path, go_examples_relative_path)
    go_mod_filepath = path.join(sdk_path, release.package, 'go.mod')

    succeeded, files = create_go_examples(release, go_module, go_mod_filepath, sdk_examples_path, go_examples_path)

    with open(output_json_path, 'w', encoding='utf-8') as f_out:
        output = {
            'status': 'succeeded' if succeeded else 'failed',
            'name': go_module,
            'files': files
        }
        json.dump(output, f_out, indent=2)


if __name__ == '__main__':
    main()
