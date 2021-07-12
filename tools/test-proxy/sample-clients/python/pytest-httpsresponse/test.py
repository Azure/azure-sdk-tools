import unittest
import requests
from RecordedByProxy import RecordedByProxy

import pdb


class TryTestProxy(unittest.TestCase):
    @RecordedByProxy
    def test_request(self):
        # arrange
        request_url = "https://bing.com"

        # act
        result = requests.get(request_url)

        # assert
        self.assertEqual(result.status_code, 200)
