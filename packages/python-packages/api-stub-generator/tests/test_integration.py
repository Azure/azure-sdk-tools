import os
import subprocess

current_dir = os.path.abspath(__file__)
module_file = os.path.join(current_dir, "test_module")

class TestIntegration(object):

    def test_basic_module(self):
        subprocess.call(["apistubgen", ])