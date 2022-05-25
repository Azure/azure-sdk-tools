# Python Record/Playback out of Proc

Python does not have a universal methodology for submitting requests. There is indeed a `requests` builtin in py3+, but there is _no requirement_ that packages actually use it to fire their requests. As a result, the recommended methodology is create a decorator that will be added to each test function. This decorator should monkeypatch the appropriate requests function that actually makes your REST requests.

Again, this sample highlights a monkeypatch of `requests` functionality. A similar monkeypatch for `trio` and `aiohttp` is present [here.](https://github.com/Azure/azure-sdk-for-python/blob/main/tools/azure-sdk-tools/devtools_testutils/aio/proxy_testcase_async.py)

The example implementation in `test.py` highlights one such monkeypatching methodology. Adjust it fit the needs of your specific project.

## How to use

As with all python development, it is recommended that you create a virtual environment prior to the below invocations.

0. Run the test proxy client from VS2019. (.NET Core 5)
1. `cd` to this directory
2. `pip install -r requirements.txt`
3. Set the environment variable `AZURE_RECORD_MODE` to `Record` or `Playback` (capitalization does not matter)
4. Run `pytest test.py`
    a. Note that invoking in `Playback` mode prior to invoking in `Record` mode will result in an error. You won't have the recordings locally saved!

## Suggestions for your implementation

1. Leverage the `@RecordedByProxy` decorator on your test function.
2. Make it the _very last_ decorator that you apply to the test function. In playback mode all your outgoing requests will be intercepted!
