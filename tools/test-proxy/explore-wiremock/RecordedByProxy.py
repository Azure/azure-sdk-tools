import requests
import functools
import os
from contextlib import contextmanager
import pdb

NETLOC = "localhost:5001"
PROXY_URL = "https://" + NETLOC
ADMIN_END_URL = "http://localhost:8080"
RECORDING_START_URL = "http://localhost:8080/__admin/recordings/start".format(ADMIN_END_URL)
RECORDING_STOP_URL = "http://localhost:8080/__admin/recordings/stop".format(PROXY_URL)
PLAYBACK_START_URL = "http://localhost:8080/playback/start".format(PROXY_URL)
PLAYBACK_STOP_URL = "http://localhost:8080/playback/stop".format(PROXY_URL)
TEST_FILE_FORMAT = "recordings/{}.txt"

from urllib.parse import urlparse

def write_recording_id(test_id, recording_id):
    try:
        os.makedirs("recordings")
    except:
        pass

    with open(TEST_FILE_FORMAT.format(test_id), "w") as f:
        f.write(recording_id)


def get_recording_id(test_id):
    with open(TEST_FILE_FORMAT.format(test_id), "r") as f:
        result = f.readline()

    return result.strip()


# this is the specific patching implementation that needs to be updated for whichever methodology is being used
# not everyone uses requests. Not everyone uses HttpResponse.
@contextmanager
def patch_requests_func(request_transform):
    original_func = requests.get

    def combined_call(*args, **kwargs):
        adjusted_args, adjusted_kwargs = request_transform(*args, **kwargs)
        return original_func(*adjusted_args, **adjusted_kwargs)

    requests.get = combined_call
    yield None

    requests.get = original_func


def RecordedByProxy(func):
    @functools.wraps(func)
    def record_wrap(*args, **kwargs):
        recording_id = ""
        test_id = func.__name__

        # the base URL that will be proxied is set at record start time.
        if os.getenv("AZURE_RECORD_MODE") == "record":
            
            # this is the primary issue. we have to know what the base urls will be to start recording
            result = requests.post(
                RECORDING_START_URL, data = { "targetBaseUrl": "https://example.mocklab.io" }, verify=False
            )

        def transform_args(*args, **kwargs):
            copied_positional_args = list(args)

            headers = {}
            if "headers" in kwargs:
                headers = kwargs["headers"]
            else:
                kwargs["headers"] = headers

            # we do not want to verify, otherwise https to the local server will fail
            kwargs["verify"] = False

            # in recording, we want to forward the request with record mode of record
            if os.getenv("AZURE_RECORD_MODE") == "record" or os.getenv("AZURE_RECORD_MODE") == "playback":
                upstream_url = copied_positional_args[0]
                
                # have to replace just the basename within the actual request. wiremock will match.
                updated_url = urlparse(upstream_url)._replace(netloc=NETLOC).geturl()
                copied_positional_args[0] = updated_url

            return tuple(copied_positional_args), kwargs

        with patch_requests_func(transform_args):
            value = func(*args, **kwargs)

        if os.getenv("AZURE_RECORD_MODE") == "record":
            result = requests.post(
                RECORDING_STOP_URL,
                verify=False,
            )
            write_recording_id(test_id, recording_id)
        return value

    return record_wrap
