from os import path
import platform
import tempfile
import subprocess
import logging
import shutil
from typing import List

from models import JsExample, JsLintResult


OS_WINDOWS = platform.system().lower() == 'windows'


def check_call(cmd: List[str], work_dir: str):
    logging.info('Command line: ' + ' '.join(cmd))
    subprocess.check_call(cmd, cwd=work_dir)


class JsLint:
    tmp_path: str
    module: str
    modules: List[str]
    package_json_path: str
    lint_config_path: str
    examples: List[JsExample]

    def __init__(self, tmp_path: str, module: str, package_json_path: str, lint_config_path: str,
                 examples: List[JsExample]):
        self.tmp_path = tmp_path
        self.module = module
        self.package_json_path = package_json_path
        self.lint_config_path = lint_config_path
        self.examples = examples

    def lint(self) -> JsLintResult:
        if not path.isfile(self.package_json_path):
            logging.error(f'package.json file not found: {self.package_json_path}')
            return JsLintResult(False, [])

        with tempfile.TemporaryDirectory(dir=self.tmp_path) as tmp_dir_name:
            # write examples to js files
            filename_no = 1
            for example in self.examples:
                filename = 'code' + str(filename_no) + '.js'
                filename_no += 1

                filepath = path.join(tmp_dir_name, filename)

                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(example.content)

            # lint js files
            try:
                # package
                logging.info('Initialize package')
                shutil.copy2(self.package_json_path, tmp_dir_name)
                # eslint config
                shutil.copy2(self.lint_config_path, tmp_dir_name)

                npm_cmd = 'npm' + ('.cmd' if OS_WINDOWS else '')
                npx_cmd = 'npx' + ('.cmd' if OS_WINDOWS else '')

                cmd = [npm_cmd, 'install', self.module, '--save', '--save-exact']
                check_call(cmd, tmp_dir_name)

                cmd = [npm_cmd, 'install', 'eslint', '--save-dev']
                check_call(cmd, tmp_dir_name)

                with open(path.join(tmp_dir_name, 'package.json'), encoding='utf-8') as f:
                    content = f.read()
                    logging.info(f'package.json\n{content}')

                logging.info('Run eslint')
                # eslint
                cmd = [npx_cmd, 'eslint', '--ext', '.js', '.']
                check_call(cmd, tmp_dir_name)
            except subprocess.CalledProcessError as error:
                logging.error(f'Call error: {error}')
                return JsLintResult(False, [])

            return JsLintResult(True, self.examples)
