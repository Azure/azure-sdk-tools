import argparse
import time
from urllib.error import URLError
from urllib.request import urlopen


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--timeout", type=float, default=300)
    args = parser.parse_args()
    deadline = time.monotonic() + args.timeout
    last_error = None
    while time.monotonic() < deadline:
        try:
            with urlopen("http://127.0.0.1:8089/openapi.json", timeout=5) as response:
                if response.status == 200:
                    print("Agent server is responsive on port 8089")
                    return
        except URLError as error:
            last_error = error
        time.sleep(2)
    raise TimeoutError(f"Agent server did not become responsive: {last_error}")


if __name__ == "__main__":
    main()
