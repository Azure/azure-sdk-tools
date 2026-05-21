"""Diagnostic script to inspect and test user memory stores.

Usage:
    python scripts/dev/debug_memory.py --user-id 29:orgid:abc-def-123
    python scripts/dev/debug_memory.py --user-id alice_123 --query "What SDK does the user work with?"
    python scripts/dev/debug_memory.py --user-id alice_123 --update "I am working on ARM SDK for python"
    python scripts/dev/debug_memory.py --web                    # launch web dashboard
    python scripts/dev/debug_memory.py --web --port 9000
"""

import asyncio
import argparse
import json as _json
import re
import sys
import threading
from http.server import HTTPServer, BaseHTTPRequestHandler
from pathlib import Path
from urllib.parse import urlparse, parse_qs

from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

import config.app_config as app_config
from config.app_config import get as cfg
from utils.azure_ai_foundry import get_project_client


def _sanitize_scope(raw: str) -> str:
    return re.sub(r"[^A-Za-z0-9_-]", "", raw)[:256]


# ---------------------------------------------------------------------------
# Web dashboard server
# ---------------------------------------------------------------------------

def _run_web_server(port: int) -> None:
    """Start the episode dashboard HTTP server."""
    from azure.ai.projects.aio import AIProjectClient
    from config.tenant_config import TenantID
    from utils.azure_ai_foundry import get_credential
    from utils.azure_cosmosdb import query_episodes

    tenants = [{"id": t.value, "label": t.name.replace("_", " ").title()} for t in TenantID]
    html_path = Path(__file__).parent / "episode_dashboard.html"

    config_data = {
        "tenants": tenants,
    }

    # Background asyncio loop — Cosmos DB APIs are async
    _bg_loop = asyncio.new_event_loop()

    async def _init():
        # Trigger Cosmos DB container initialization
        from utils.azure_cosmosdb import get_episode_container
        await get_episode_container()

    def _start_bg_loop():
        asyncio.set_event_loop(_bg_loop)
        _bg_loop.run_forever()

    threading.Thread(target=_start_bg_loop, daemon=True).start()
    asyncio.run_coroutine_threadsafe(_init(), _bg_loop).result(timeout=15)

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            parsed = urlparse(self.path)
            path = parsed.path.rstrip("/")
            if not path or path == "/":
                self._serve_html()
            elif path == "/api/config":
                self._send_json(config_data)
            elif path == "/api/episodes":
                self._handle_episodes(parse_qs(parsed.query))
            else:
                self.send_error(404)

        def _serve_html(self):
            try:
                content = html_path.read_text(encoding="utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.end_headers()
                self.wfile.write(content.encode())
            except FileNotFoundError:
                self.send_error(500, f"Dashboard HTML not found: {html_path}")

        def _handle_episodes(self, params):
            tenant_id = params.get("tenant_id", [None])[0]
            thread_id = params.get("thread_id", [None])[0]
            try:
                future = asyncio.run_coroutine_threadsafe(
                    query_episodes(
                        tenant_id=tenant_id or None,
                        source_thread_id=thread_id or None,
                    ),
                    _bg_loop,
                )
                episodes = future.result(timeout=30)
                self._send_json({"episodes": episodes})
            except Exception as e:
                self._send_json({"error": str(e)}, 500)

        def _send_json(self, data, status=200):
            body = _json.dumps(data, indent=2).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Cache-Control", "no-cache")
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, fmt, *args):
            pass

    server = HTTPServer(("", port), Handler)
    print(f"\n  Episode Dashboard: http://localhost:{port}")
    print(f"  Press Ctrl+C to stop.\n")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down...")
        server.shutdown()
        _bg_loop.call_soon_threadsafe(_bg_loop.stop)


# ---------------------------------------------------------------------------
# CLI mode
# ---------------------------------------------------------------------------

def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Inspect Foundry user memory store contents.")
    parser.add_argument("--user-id", default=None, help="User ID (formats scope as user_{user_id})")
    parser.add_argument("--query", default=None, help="Search query to test contextual memory retrieval")
    parser.add_argument("--update", default=None, help="Send a test message to update memories")
    parser.add_argument("--store", default=None, help="Memory store name (default: from config)")
    parser.add_argument("--recreate", action="store_true", help="Delete and recreate the memory store")
    parser.add_argument("--web", action="store_true", help="Launch the memory dashboard web server")
    parser.add_argument("--port", type=int, default=8501, help="Port for the web server (default: 8501)")
    return parser


async def main() -> None:
    args = _build_parser().parse_args()

    await app_config.init()

    # Web dashboard mode — starts a sync HTTP server, never returns until Ctrl+C
    if args.web:
        _run_web_server(args.port)
        return

    project_client = get_project_client()

    # Determine which store to use
    if args.store:
        store_name = args.store
    else:
        store_name = cfg("MEMORY_USER_STORE_NAME", "azure-sdk-qa-bot-user-memory-store")

    # Determine scope from user ID
    user_id = getattr(args, "user_id", None)
    if not user_id and not args.recreate:
        print("ERROR: --user-id is required (e.g. --user-id alice_123)")
        return
    scope = _sanitize_scope(f"user_{user_id}") if user_id else ""

    print(f"Memory store: {store_name}")
    print(f"Scope: {scope}")
    print()

    # 1. Recreate store if requested
    if args.recreate:
        print("Deleting memory store...")
        try:
            await project_client.beta.memory_stores.delete(store_name)
            print(f"  Deleted: {store_name}")
        except Exception as e:
            print(f"  Delete skipped (may not exist): {e}")

        print("Recreating memory store...")
        from utils.azure_memory_store import ensure_user_memory_store
        try:
            store = await ensure_user_memory_store(project_client)
            if store:
                print(f"  Recreated: {store_name}")
            else:
                print("  ERROR: ensure_user_memory_store returned None")
                return
        except Exception as e:
            print(f"  ERROR: {e}")
            return

    # 2. Check the memory store exists
    try:
        store = await project_client.beta.memory_stores.get(store_name)
        print(f"Store found: {store.name} — {store.description}")
    except Exception as e:
        print(f"ERROR: Could not get memory store: {e}")
        return

    # 3. Test update if requested
    if args.update:
        print(f"\n--- Updating memories with: '{args.update}' ---")
        try:
            update_poller = await project_client.beta.memory_stores.begin_update_memories(
                name=store_name,
                scope=scope,
                items=[{"type": "message", "role": "user", "content": args.update}],
                update_delay=0,
            )
            print(f"  Update submitted (update_id={update_poller.update_id})")
            print("  Waiting for extraction to complete...")
            update_result = await update_poller.result()
            print(f"  Done! {len(update_result.memory_operations)} memory operations:")
            for op in update_result.memory_operations:
                print(f"    - {op.kind}: {op.memory_item.content}")
        except Exception as e:
            print(f"  ERROR: {e}")

    # 3. Retrieve static memories (user profile) — no query items
    print("\n--- Static memories (user profile) ---")
    try:
        static_result = await project_client.beta.memory_stores.search_memories(
            name=store_name,
            scope=scope,
        )
        if static_result.memories:
            for i, mem in enumerate(static_result.memories, 1):
                print(f"  [{i}] ID: {mem.memory_item.memory_id}")
                print(f"      Content: {mem.memory_item.content}")
                print()
        else:
            print("  (none)")
    except Exception as e:
        print(f"  ERROR: {e}")

    # 4. Search contextual memories with a query
    if args.query:
        print(f"\n--- Contextual memories for: '{args.query}' ---")
        try:
            query_result = await project_client.beta.memory_stores.search_memories(
                name=store_name,
                scope=scope,
                items=[{"type": "message", "role": "user", "content": args.query}],
            )
            if query_result.memories:
                for i, mem in enumerate(query_result.memories, 1):
                    print(f"  [{i}] ID: {mem.memory_item.memory_id}")
                    print(f"      Content: {mem.memory_item.content}")
                    print()
            else:
                print("  (none)")
        except Exception as e:
            print(f"  ERROR: {e}")

    await project_client.close()


if __name__ == "__main__":
    asyncio.run(main())
