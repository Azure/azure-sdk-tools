import unittest
import requests
from RecordedByProxy import RecordedByProxy

import pdb


class TryTestProxy(unittest.TestCase):
    @RecordedByProxy
    def test_request(self):
        # arrange
        request_url = "https://example.mocklab.io/recordables/123"

        # act
        result = requests.get(request_url)

        # assert
        self.assertEqual(result.status_code, 200)
