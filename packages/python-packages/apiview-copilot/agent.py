# Refactored CLI chat to use FastAPI endpoints in app.py

import os
import requests
import logging
from dotenv import load_dotenv


load_dotenv(override=True)

# Configure logging to file
logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(levelname)s - %(message)s",
    handlers=[
        logging.FileHandler("agent_cli.log", mode="a", encoding="utf-8"),
    ],
)

APP_NAME = os.getenv("AZURE_APP_NAME")
API_ENDPOINT = f"https://{APP_NAME}.azurewebsites.net/agent/chat"


def chat():
    print("Interactive API Review Agent Chat (via HTTP API). Type 'exit' to quit.")
    BLUE = "\033[94m"
    GREEN = "\033[92m"
    RESET = "\033[0m"
    thread_id = ""
    messages = []
    while True:
        user_input = input(f"{GREEN}You:{RESET} ")
        if user_input.strip().lower() in {"exit", "quit"}:
            print("Exiting chat.")
            logging.info("User exited chat.")
            break
        # Append the new user message in ThreadMessageOptions format
        messages.append({"role": "user", "content": user_input})
        payload = {
            "thread_id": thread_id if thread_id is not None else "",
            "messages": messages,
        }
        logging.info(f"Sending payload: {payload}")
        try:
            resp = requests.post(API_ENDPOINT, json=payload, timeout=300)
            logging.info(f"POST {API_ENDPOINT} status={resp.status_code}")
            if resp.status_code == 200:
                data = resp.json()
                thread_id = data.get("thread_id", thread_id)
                # Append the agent's response to the messages list for context
                agent_response = data.get("response", "")
                if agent_response:
                    messages.append({"role": "assistant", "content": agent_response})
                logging.info(f"Received response: {data}")
                print(f"{BLUE}Agent:{RESET} {agent_response}\n")
            else:
                logging.error(f"Error {resp.status_code}: {resp.text}")
                print(f"{BLUE}Agent:{RESET} [Error {resp.status_code}] {resp.text}\n")
        except Exception as e:
            logging.exception("Request error")
            print(f"{BLUE}Agent:{RESET} [Request error] {e}\n")


if __name__ == "__main__":
    chat()
