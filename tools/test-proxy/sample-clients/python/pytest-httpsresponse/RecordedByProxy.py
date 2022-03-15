import requests
import functools
import os
from contextlib import contextmanager
import pdb

try:
    # py3
    import urllib.parse as url_parse
except:
    # py2
    import urlparse as url_parse

PROXY_URL = "http://localhost:5000"
RECORDING_START_URL = "{}/record/start".format(PROXY_URL)
RECORDING_STOP_URL = "{}/record/stop".format(PROXY_URL)
PLAYBACK_START_URL = "{}/playback/start".format(PROXY_URL)
PLAYBACK_STOP_URL = "{}/playback/stop".format(PROXY_URL)
TEST_FILE_FORMAT = "recordings/{}.txt"


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

def get_proxy_netloc():
    parsed_result = url_parse.urlparse(PROXY_URL)
    return {"scheme": parsed_result.scheme, "netloc": parsed_result.netloc}

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

        if os.getenv("AZURE_RECORD_MODE") == "record":
            result = requests.post(
                RECORDING_START_URL, json={"x-recording-file": test_id}, verify=False
            )
            recording_id = result.headers["x-recording-id"]
        elif os.getenv("AZURE_RECORD_MODE") == "playback":
            result = requests.post(
                PLAYBACK_START_URL,
                headers={"Content-Type": "application/json"},
                json={"x-recording-file": test_id},
                verify=False,
            )
            recording_id = result.headers["x-recording-id"]

        def transform_args(*args, **kwargs):
            copied_positional_args = list(args)
            request = copied_positional_args[0]
            parsed_result = url_parse.urlparse(request)
            
            headers = {}
            if "headers" in kwargs:
                headers = kwargs["headers"]
            else:
                kwargs["headers"] = headers

            # we do not want to verify, otherwise https to the local server will fail
            kwargs["verify"] = False

            # in recording, we want to forward the request with record mode of record
            if os.getenv("AZURE_RECORD_MODE") == "record":
                upstream_url = copied_positional_args[0]
                headers["x-recording-upstream-base-uri"] = "{}://{}".format(parsed_result.scheme, parsed_result.netloc)
                headers["x-recording-id"] = recording_id
                headers["x-recording-mode"] = "record"
                copied_positional_args[0] = PROXY_URL

            # otherwise we want to forward the request with record mode of playback
            elif os.getenv("AZURE_RECORD_MODE") == "playback":
                upstream_url = copied_positional_args[0]
                headers["x-recording-upstream-base-uri"] = "{}://{}".format(parsed_result.scheme, parsed_result.netloc)
                headers["x-recording-id"] = recording_id
                headers["x-recording-mode"] = "playback"
                copied_positional_args[0] = PROXY_URL

            return tuple(copied_positional_args), kwargs

        with patch_requests_func(transform_args):
            value = func(*args, **kwargs)

        if os.getenv("AZURE_RECORD_MODE") == "record":
            result = requests.post(
                RECORDING_STOP_URL,
                headers={"x-recording-id": recording_id, "x-recording-save": "true"},
                verify=False,
            )
            write_recording_id(test_id, recording_id)
        elif os.getenv("AZURE_RECORD_MODE") == "playback":
            result = requests.post(
                PLAYBACK_STOP_URL,
                headers={"x-recording-id": recording_id},
                verify=False,
            )

        return value

    return record_wrap
