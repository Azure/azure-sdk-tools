import json
import os
from subprocess import check_call

from apistubgen import console_entry_point

current_dir = os.path.abspath(os.path.join(__file__, ".."))
module_file = os.path.join(current_dir, "fake_module")
json_path = os.path.join(current_dir, "test-module_python.json")


class TestIntegration(object):
    def _remove_json_file(self, json_file=json_path):
        os.remove(json_file)

    def set_up(self):
        check_call(["apistubgen", "--pkg-path", module_file, "--out-path", current_dir])
        with open(json_path, 'r') as j:
            return json.loads(j.read())

    def test_basic_module(self):
        apiview_json = self.set_up()

        print(apiview_json)

        assert apiview_json["Name"] == "test-module"
        assert apiview_json["PackageName"] == "test-module"

        nav = apiview_json["Navigation"][0]

        assert len(apiview_json["Diagnostics"]) == 0

        self._remove_json_file()