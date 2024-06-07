from os import path
import tempfile
import subprocess
import logging
from typing import List

from models import DotNetExample, DotNetBuildResult


def check_call(cmd: List[str], work_dir: str):
    logging.info('Command line: ' + ' '.join(cmd))
    subprocess.check_call(cmd, cwd=work_dir)


class DotNetBuild:
    tmp_path: str
    module: str
    module_version: str
    examples: List[DotNetExample]

    def __init__(self, tmp_path: str, module: str, module_version: str, examples: List[DotNetExample]):
        self.tmp_path = tmp_path
        self.module = module
        self.module_version = module_version
        self.examples = examples

    def build(self) -> DotNetBuildResult:
        with tempfile.TemporaryDirectory(dir=self.tmp_path) as tmp_dir_name:
            # format and validate go files
            current_example = None
            try:
                logging.info('Initialize project')
                # project
                cmd = ['dotnet', 'new', 'console', '--name', 'example', '--output', '.']
                check_call(cmd, tmp_dir_name)

                cmd = ['dotnet', 'add', 'package', 'Azure.Identity']
                check_call(cmd, tmp_dir_name)

                # cmd = ['dotnet', 'add', 'package', 'Azure.ResourceManager']
                # check_call(cmd, tmp_dir_name)

                cmd = ['dotnet', 'add', 'package', self.module, '--version', self.module_version]
                check_call(cmd, tmp_dir_name)

                with open(path.join(tmp_dir_name, 'example.csproj'), encoding='utf-8') as f:
                    content = f.read()
                    logging.info(f'csproj\n{content}')

                # build per example
                filename = 'Program.cs'
                filepath = path.join(tmp_dir_name, filename)
                file_no = 0
                max_file_count = 10  # TODO: for now, only build for first 10 examples
                for example in self.examples:
                    current_example = example
                    file_no += 1
                    if file_no > max_file_count:
                        break

                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(example.content)

                    cmd = ['dotnet', 'clean', '--nologo', '--verbosity', 'quiet']
                    check_call(cmd, tmp_dir_name)

                    cmd = ['dotnet', 'build', '--no-restore', '--nologo', '--verbosity', 'quiet']
                    check_call(cmd, tmp_dir_name)

            except subprocess.CalledProcessError as error:
                logging.error(f'Call error: {error}')
                if current_example:
                    logging.error(f'Program.cs\n{current_example.content}')
                return DotNetBuildResult(False, [])

            return DotNetBuildResult(True, self.examples)
