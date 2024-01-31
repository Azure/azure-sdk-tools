from os import path
import tempfile
import subprocess
import re
import logging
from typing import List

from models import GoExample, GoVetResult


def check_call(cmd: List[str], work_dir: str):
    logging.info('Command line: ' + ' '.join(cmd))
    subprocess.check_call(cmd, cwd=work_dir)


class GoVet:
    tmp_path: str
    module: str
    modules: List[str]
    golang_version: str
    examples: List[GoExample]

    def __init__(self, tmp_path: str, module: str, go_mod: str, examples: List[GoExample]):
        self.tmp_path = tmp_path
        self.module = module
        self.examples = examples

        match = re.search(r'go ([.0-9]*)', go_mod, re.MULTILINE)
        if match:
            self.golang_version = match.group(1)

        self.modules = []
        match = re.search(r'github\.com/Azure/azure-sdk-for-go/sdk/azcore (v[.\-\w]*)', go_mod, re.MULTILINE)
        if match:
            self.modules.append('github.com/Azure/azure-sdk-for-go/sdk/azcore@' + match.group(1))
        match = re.search(r'github\.com/Azure/azure-sdk-for-go/sdk/azidentity (v[.\-\w]*)', go_mod, re.MULTILINE)
        if match:
            self.modules.append('github.com/Azure/azure-sdk-for-go/sdk/azidentity@' + match.group(1))

    def vet(self) -> GoVetResult:
        with tempfile.TemporaryDirectory(dir=self.tmp_path) as tmp_dir_name:
            # write examples to go files
            filename_no = 1
            for example in self.examples:
                filename = 'code' + str(filename_no) + '.go'
                filename_no += 1

                filepath = path.join(tmp_dir_name, filename)

                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(example.content)

            # format and validate go files
            try:
                logging.info('Initialize mod')
                # mod
                cmd = ['go', 'mod', 'init', 'm']
                check_call(cmd, tmp_dir_name)

                if self.golang_version:
                    cmd = ['go', 'mod', 'edit', '-go', self.golang_version]
                    check_call(cmd, tmp_dir_name)

                cmd = ['go', 'mod', 'edit', '-require', self.module]
                check_call(cmd, tmp_dir_name)

                for module in self.modules:
                    cmd = ['go', 'mod', 'edit', '-require', module]
                    check_call(cmd, tmp_dir_name)

                cmd = ['go', 'mod', 'tidy']
                check_call(cmd, tmp_dir_name)

                with open(path.join(tmp_dir_name, 'go.mod'), encoding='utf-8') as f:
                    content = f.read()
                    logging.info(f'go.mod\n{content}')

                logging.info('Run goimports')
                # goimports
                cmd = ['go', 'install', 'golang.org/x/tools/cmd/goimports@latest']
                check_call(cmd, tmp_dir_name)

                cmd = ['goimports', '-w', '.']
                check_call(cmd, tmp_dir_name)

                logging.info('Build and vet')
                # build and vet
                cmd = ['go', 'build']
                check_call(cmd, tmp_dir_name)

                cmd = ['go', 'vet']
                check_call(cmd, tmp_dir_name)
            except subprocess.CalledProcessError as error:
                logging.error(f'Call error: {error}')
                return GoVetResult(False, [])

            # read formatted examples from go files
            formatted_examples = []
            filename_no = 1
            for example in self.examples:
                filename = 'code' + str(filename_no) + '.go'
                filename_no += 1

                filepath = path.join(tmp_dir_name, filename)

                with open(filepath, encoding='utf-8') as f:
                    content = f.read()
                    formatted_examples.append(GoExample(example.target_filename, example.target_dir, content))

            return GoVetResult(True, formatted_examples)
