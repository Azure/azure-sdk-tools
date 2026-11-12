import json
import os
import queue
import subprocess
import sys
import threading
import time
import urllib.request


TARGET = os.environ.get("TARGET_ENVIRONMENT", "preview")


def verify_runtime():
    token_result = subprocess.run(
        [
            "az", "account", "get-access-token",
            "--scope", os.environ["AZURE_SDK_KB_CLIENT_ID"] + "/.default",
            "--output", "json", "--only-show-errors",
        ],
        check=True, capture_output=True, text=True, timeout=60,
    )
    token = json.loads(token_result.stdout)["accessToken"]
    request = urllib.request.Request(
        os.environ["AZURE_SDK_KB_ENDPOINT"] + "/.auth/version",
        headers={"Authorization": "Bearer " + token},
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        version = json.load(response)
    if version.get("miseVersion") != "MISEv2":
        raise RuntimeError(TARGET + " runtime did not report MISEv2")
    print("Authenticated runtime:", json.dumps(version), flush=True)


def receive_messages(stream, messages):
    for line in stream:
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(message, dict):
            messages.put(message)
    messages.put(None)


def send(process, message):
    process.stdin.write(json.dumps({"jsonrpc": "2.0", **message}) + "\n")
    process.stdin.flush()


def rpc(process, messages, request_id, method, params):
    send(process, {"id": request_id, "method": method, "params": params})
    deadline = time.monotonic() + 300
    while True:
        message = messages.get(timeout=max(0.01, deadline - time.monotonic()))
        if message is None:
            raise RuntimeError("MCP process exited before responding to " + method)
        if message.get("id") != request_id:
            continue
        if "error" in message:
            raise RuntimeError("MCP protocol error: " + json.dumps(message["error"]))
        return message["result"]


def verify_retrieval(dll):
    process = subprocess.Popen(
        ["dotnet", dll, "mcp"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        text=True, bufsize=1,
    )
    messages = queue.Queue()
    threading.Thread(
        target=receive_messages, args=(process.stdout, messages), daemon=True,
    ).start()
    try:
        rpc(process, messages, 1, "initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "preview-retrieval-smoke", "version": "1.0"},
        })
        send(process, {"method": "notifications/initialized"})
        tools = rpc(process, messages, 2, "tools/list", {})
        name = "azsdk_typespec_retrieve_knowledge"
        if not any(tool["name"] == name for tool in tools["tools"]):
            raise RuntimeError("Knowledge retrieval tool is not exposed")
        print("Retrieval tool exposed; invoking it explicitly.", flush=True)
        result = rpc(process, messages, 3, "tools/call", {
            "name": name,
            "arguments": {
                "request": (
                    "How should I add a new optional property to an Azure TypeSpec "
                    "model in a new API version using @added? Include authoritative references."
                ),
                "typeSpecProjectRootPath": os.path.join(
                    os.environ["BUILD_SOURCESDIRECTORY"],
                    ".github", "skills", "azure-typespec-author", "evaluate",
                    "fixtures", "Microsoft.Widget", "Widget",
                ),
            },
        })
        if result.get("isError"):
            raise RuntimeError("MCP knowledge retrieval failed: " + json.dumps(result))
        outputs = []
        if isinstance(result.get("structuredContent"), dict):
            outputs.append(result["structuredContent"])
        for content in result.get("content", []):
            if content.get("type") == "text":
                try:
                    outputs.append(json.loads(content["text"]))
                except json.JSONDecodeError:
                    continue
        for output in outputs:
            if not isinstance(output, dict):
                continue
            if output.get("response_error") or output.get("responseError"):
                raise RuntimeError("Knowledge retrieval returned an error: " + json.dumps(output))
            context = output.get("context")
            if isinstance(context, str) and context.strip():
                knowledge = json.loads(context)
                if isinstance(knowledge, list) and knowledge:
                    print(
                        "PASS: pipeline identity -> live MCP -> " + TARGET + " MISEv2 -> "
                        + str(len(knowledge)) + " knowledge results.",
                        flush=True,
                    )
                    return
        shapes = [
            sorted(output) if isinstance(output, dict) else type(output).__name__
            for output in outputs
        ]
        raise RuntimeError("No non-empty knowledge list returned; output fields: " + json.dumps(shapes))
    finally:
        if process.poll() is None:
            process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=10)


if __name__ == "__main__":
    verify_runtime()
    verify_retrieval(sys.argv[1])
