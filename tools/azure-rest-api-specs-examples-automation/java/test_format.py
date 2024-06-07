import unittest
from os import path

from modules import JavaExample
from format import JavaFormat


class TestJavaFormat(unittest.TestCase):

    def test_example(self):
        tmp_path = path.abspath('.')
        maven_path = path.abspath('./javaformat')
        java_format = JavaFormat(tmp_path, maven_path)
        code = '''class Main {}
'''
        result = java_format.format([JavaExample('', '', code)])
        self.assertTrue(result.succeeded)
        self.assertEqual('''class Main {
}
''', result.examples[0].content)
