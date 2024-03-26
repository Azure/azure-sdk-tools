import sys
import urllib.parse
import os
from os import path
import glob
import json
import argparse
import logging
import dataclasses
from typing import List, Optional


script_path: str = '.'
tmp_path: str

original_file_key: str = '# x-ms-original-file: '

module_relative_path: str = ''


@dataclasses.dataclass(eq=True, frozen=True)
class Release:
    tag: str
    package: str
    version: str


@dataclasses.dataclass(eq=True)
class PythonExample:
    target_filename: str
    target_dir: str
    content: str


def parse_python_example(lines: List[str]) -> Optional[PythonExample]:
    # check metadata to see if the sample file is a candidate for example extraction

    example_relative_path = None
    for line in lines:
        if line.strip().startswith(original_file_key):
            example_relative_path = line.strip()[len(original_file_key):]
            break

    example = None
    if example_relative_path:
        example_dir, example_filename = path.split(example_relative_path)
        target_dir = (example_dir + '-python') if example_dir.endswith('/examples') \
            else example_dir.replace('/examples/', '/examples-python/')
        filename = example_filename.split('.')[0]

        first_line_index = 0
        for index, line in enumerate(lines):
            if line.strip() and not line.strip().startswith('# '):
                first_line_index = index
                break

        lines = lines[first_line_index:]

        example = PythonExample(filename, target_dir, ''.join(lines))

    return example


def process_python_example(filepath: str) -> List[PythonExample]:
    # process aggregated Python sample to examples

    filename = path.basename(filepath)
    logging.info(f'Processing Python sample: {filename}')

    with open(filepath, encoding='utf-8') as f:
        lines = f.readlines()

    python_examples = []
    python_example = parse_python_example(lines)
    if python_example:
        python_examples.append(python_example)

    return python_examples


def generate_examples(release: Release, sdk_examples_path: str, python_examples: List[PythonExample]) -> List[str]:
    # generate code and metadata from Python examples

    global module_relative_path

    files = []
    for python_example in python_examples:
        escaped_release_tag = urllib.parse.quote(release.tag, safe='')
        doc_link = f'https://github.com/Azure/azure-sdk-for-python/blob/{escaped_release_tag}/' \
                   f'{module_relative_path}/README.md'

        files.extend(write_code_to_file(sdk_examples_path, python_example.target_dir,
                                        python_example.target_filename, '.py', python_example.content, doc_link))
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


def create_python_examples(release: Release,
                           js_module: str,
                           sdk_examples_path: str, js_examples_path: str) -> (bool, List[str]):
    python_paths = []
    for root, dirs, files in os.walk(js_examples_path):
        for name in files:
            filepath = path.join(root, name)
            if path.splitext(filepath)[1] == '.py':
                python_paths.append(filepath)

    logging.info(f'Processing SDK examples: {release.package}')
    python_examples = []
    for filepath in python_paths:
        python_examples += process_python_example(filepath)

    files = []
    if python_examples:
        logging.info('Writing SDK examples')
        files = generate_examples(release, sdk_examples_path, python_examples)

        return True, files
    else:
        logging.info('SDK examples not found')
        return True, files


def get_module_relative_path(sdk_name: str, sdk_path: str) -> str:
    global module_relative_path
    candidate_sdk_paths = glob.glob(path.join(sdk_path, f'sdk/*/{sdk_name}'))
    if len(candidate_sdk_paths) > 0:
        candidate_sdk_paths = [path.relpath(p, sdk_path) for p in candidate_sdk_paths]
        logging.info(
            f'Use first item of {candidate_sdk_paths} for SDK folder')
        module_relative_path = candidate_sdk_paths[0]
    else:
        raise RuntimeError(f'Source folder not found for SDK {sdk_name}')
    return module_relative_path


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

    js_module = f'{release.package}@{release.version}'

    module_relative_path_local = get_module_relative_path(release.package, sdk_path)
    python_examples_relative_path = path.join(module_relative_path_local, 'generated_samples')
    python_examples_path = path.join(sdk_path, python_examples_relative_path)

    succeeded, files = create_python_examples(release, js_module, sdk_examples_path, python_examples_path)

    with open(output_json_path, 'w', encoding='utf-8') as f_out:
        output = {
            'status': 'succeeded' if succeeded else 'failed',
            'name': js_module,
            'files': files
        }
        json.dump(output, f_out, indent=2)


if __name__ == '__main__':
    main()
