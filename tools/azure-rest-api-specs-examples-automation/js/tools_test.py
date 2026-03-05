import unittest

from tools import publish_samples


class TestTools(unittest.TestCase):

    @unittest.skip("require a local clone of https://github.com/Azure/azure-sdk-for-js")
    def test_publish_samples(self):
        sdk_path = "c:/github/azure-sdk-for-js"
        module_relative_path = "sdk/advisor/arm-advisor"

        publish_samples(sdk_path, module_relative_path)
