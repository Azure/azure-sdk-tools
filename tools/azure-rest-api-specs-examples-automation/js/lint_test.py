import unittest
from os import path

from lint import JsLint
from models import JsExample


class TestJsLint(unittest.TestCase):

    def test_example(self):
        code = '''const { createDefaultHttpClient, createPipelineRequest } = require("@azure/core-rest-pipeline");

const httpClient = createDefaultHttpClient();
httpClient.sendRequest(createPipelineRequest("https://httpbin.org/"));
'''

        tmp_path = path.abspath('.')
        js_examples = [JsExample('code', '', code)]
        js_lint = JsLint(tmp_path, '@azure/core-rest-pipeline@1.8.1',
                         path.join(tmp_path, 'lint', 'package.json'),
                         path.join(tmp_path, 'lint', '.eslintrc.json'),
                         js_examples)
        result = js_lint.lint()
        self.assertTrue(result.succeeded)
