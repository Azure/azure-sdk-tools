import unittest

from main import get_package_name


class TestMain(unittest.TestCase):

    def test_sdk_package(self):
        self.assertEqual(
            "azure-resourcemanager-storage", get_package_name("com.azure.resourcemanager+azure-resourcemanager-storage")
        )
        self.assertEqual("azure-resourcemanager-storage", get_package_name("azure-resourcemanager-storage"))
