import asyncio
import os
from pprint import pprint
import sys
import aiohttp
from typing import Optional

BASE_API_ENDPOINT = "https://apiview-gpt.azurewebsites.net"


async def generate_remote_review(*, target: str, base: Optional[str], language: str) -> str:
    """
    Sends the query to the API endpoint with the language as a path parameter and awaits the response.
    """
    api_endpoint = f"{BASE_API_ENDPOINT}/{language}"

    # Increase timeout to 5 minutes
    timeout = aiohttp.ClientTimeout(total=300)  # 5 minutes

    async with aiohttp.ClientSession(timeout=timeout) as session:
        try:
            print(f"Sending request to {api_endpoint}...")
            async with session.post(api_endpoint, json={"target": target, "base": base}) as response:
                if response.status == 200:
                    print("Request successful, waiting for response...")
                    # Already parsed as JSON by response.json()
                    return await response.json()
                else:
                    error_text = await response.text()
                    return f"Error: Received status code {response.status} from API. Details: {error_text}"
        except asyncio.TimeoutError:
            return "Error: Request timed out. The review is taking longer than expected."
        except aiohttp.ClientError as e:
            return f"Error: Failed to connect to API. Details: {e}"


def _read_input(input_value: str) -> str:
    """
    Reads input from a file if the input_value is a valid file path,
    otherwise treats it as a string query.
    """
    if os.path.isfile(input_value):
        with open(input_value, "r", encoding="utf-8") as file:
            return file.read()
    return input_value


async def main():
    if len(sys.argv) < 3:
        print("Usage: python query_app.py <file_or_query> <language>")
        sys.exit(1)

    input_value = sys.argv[1]
    language = sys.argv[2]
    query = _read_input(input_value)
    response = await generate_remote_review(query, language)
    pprint(response)


if __name__ == "__main__":
    asyncio.run(main())
