"""Interactive CLI to send mock Teams messages to the local server or local agent.

Usage:
    python scripts/mock_teams_chat.py
    python scripts/mock_teams_chat.py --local
    python scripts/mock_teams_chat.py --local --user-id "29:orgid:abc-123" --user-name "Alice"
    python scripts/mock_teams_chat.py --user-id "29:orgid:abc-123" --user-name "Alice" --tenant azure_sdk_qa_bot
    python scripts/mock_teams_chat.py --server http://localhost:8089 --conversation-id my-conv-1

Starts an interactive chat loop. Type messages and see agent responses.
Type 'quit' or 'exit' to stop, '/new' to start a new conversation,
'/tenant <id>' to switch tenants, '/user <id>' to switch user.

Modes:
  Default:  Sends ChatRequest to the backend server (/agent/chat)
  --local:  Sends Responses protocol messages directly to the local agent (port 8088)
            Injects [memory_scope] and tenant context as system messages.
"""

import argparse
import asyncio
import json
import re
import sys
import uuid

import httpx

TENANTS = [
    "azure_sdk_qa_bot",
    "python_channel_qa_bot",
    "dotnet_channel_qa_bot",
    "golang_channel_qa_bot",
    "java_channel_qa_bot",
    "javascript_channel_qa_bot",
    "general_qa_bot",
    "azure_sdk_onboarding",
    "azure_typespec_authoring",
    "api_spec_review_bot",
]


def _sanitize_scope(raw: str) -> str:
    return re.sub(r"[^A-Za-z0-9_-]", "", raw)[:256]


def _resolve_memory_scope(user_id: str, default: str = "azure-sdk-qa-bot") -> str:
    if user_id and user_id.strip():
        return _sanitize_scope(f"user_{user_id.strip()}")
    return default


def _print_banner(args):
    mode = "LOCAL AGENT" if args.local else "SERVER"
    target = args.server
    print("=" * 60)
    print(f"  Mock Teams Chat Client  [{mode}]")
    print("=" * 60)
    print(f"  Target:          {target}")
    print(f"  User ID:         {args.user_id}")
    print(f"  User Name:       {args.user_name}")
    print(f"  Tenant:          {args.tenant}")
    if not args.local:
        print(f"  Conversation ID: {args.conversation_id}")
        print(f"  Conv Type:       {args.conversation_type}")
    else:
        print(f"  Memory Scope:    {_resolve_memory_scope(args.user_id)}")
    print("=" * 60)
    print()
    print("Commands:")
    print("  /new               Start a new conversation")
    print("  /tenant <id>       Switch tenant")
    print("  /user <id> [name]  Switch user")
    print("  /save <content>    Save a message via /conversation/save")
    print("  /status            Show current settings")
    print("  /tenants           List available tenants")
    print("  quit / exit        Exit")
    print()


async def main():
    parser = argparse.ArgumentParser(
        description="Mock Teams chat client for the agent server."
    )
    parser.add_argument(
        "--local",
        action="store_true",
        help="Call local agent directly (port 8088) instead of server",
    )
    parser.add_argument(
        "--server",
        default=None,
        help="Server/agent base URL (default: auto based on --local)",
    )
    parser.add_argument("--user-id", default="mock-user-001", help="Teams user ID")
    parser.add_argument(
        "--user-name", default="MockUser", help="Teams user display name"
    )
    parser.add_argument(
        "--tenant", default="azure_sdk_qa_bot", choices=TENANTS, help="Tenant ID"
    )
    parser.add_argument(
        "--conversation-id",
        default=None,
        help="Conversation ID (auto-generated if omitted)",
    )
    parser.add_argument(
        "--conversation-type", default="teams_channel", help="Conversation type"
    )
    parser.add_argument(
        "--full-context", action="store_true", help="Request full context in response"
    )
    args = parser.parse_args()

    if args.server is None:
        args.server = "http://localhost:8088" if args.local else "http://localhost:8089"

    if not args.conversation_id:
        args.conversation_id = f"mock-conv-{uuid.uuid4().hex[:8]}"

    _print_banner(args)

    async with httpx.AsyncClient(timeout=120.0) as client:
        while True:
            try:
                user_input = input(f"[{args.user_name}] > ").strip()
            except (EOFError, KeyboardInterrupt):
                print("\nBye!")
                break

            if not user_input:
                continue

            if user_input.lower() in ("quit", "exit"):
                print("Bye!")
                break

            if user_input == "/new":
                args.conversation_id = f"mock-conv-{uuid.uuid4().hex[:8]}"
                print(f"  New conversation: {args.conversation_id}\n")
                continue

            if user_input.startswith("/tenant "):
                new_tenant = user_input.split(maxsplit=1)[1].strip()
                if new_tenant in TENANTS:
                    args.tenant = new_tenant
                    print(f"  Switched tenant: {args.tenant}\n")
                else:
                    print(f"  Unknown tenant. Use /tenants to list.\n")
                continue

            if user_input.startswith("/user "):
                parts = user_input.split(maxsplit=2)
                args.user_id = parts[1].strip()
                if len(parts) > 2:
                    args.user_name = parts[2].strip()
                print(f"  Switched user: {args.user_id} ({args.user_name})\n")
                continue

            if user_input == "/status":
                mode = "LOCAL AGENT" if args.local else "SERVER"
                print(f"  Mode:            {mode}")
                print(f"  Target:          {args.server}")
                print(f"  User ID:         {args.user_id}")
                print(f"  User Name:       {args.user_name}")
                print(f"  Tenant:          {args.tenant}")
                if args.local:
                    print(f"  Memory Scope:    {_resolve_memory_scope(args.user_id)}")
                else:
                    print(f"  Conversation ID: {args.conversation_id}")
                    print(f"  Conv Type:       {args.conversation_type}")
                print()
                continue

            if user_input == "/tenants":
                for t in TENANTS:
                    marker = " <--" if t == args.tenant else ""
                    print(f"  {t}{marker}")
                print()
                continue

            if user_input.startswith("/save "):
                save_content = user_input.split(maxsplit=1)[1].strip()
                if save_content:
                    await _save_message(client, args, save_content)
                else:
                    print("  Usage: /save <message content>\n")
                continue

            # Build and send request
            if args.local:
                await _send_local(client, args, user_input)
            else:
                await _send_server(client, args, user_input)


async def _save_message(client: httpx.AsyncClient, args, content: str):
    """Send a ConversationMessage to /conversation/save (triggers episode extraction)."""
    from datetime import datetime, timezone

    payload = {
        "id": f"msg-{uuid.uuid4().hex[:8]}",
        "channel_id": f"channel-{args.tenant}",
        "sender_role": "user",
        "sender_id": args.user_id,
        "sender_name": args.user_name,
        "content": content,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "conversation_id": args.conversation_id,
        "conversation_type": args.conversation_type,
        "tenant_id": args.tenant,
    }

    url = f"{args.server}/conversation/save"
    try:
        resp = await client.post(url, json=payload)
        if resp.status_code == 200:
            print(f"  Saved message to conversation {args.conversation_id}")
            print(f"  (Background episode extraction triggered)\n")
        else:
            print(f"\n  ERROR {resp.status_code}: {resp.text}\n")
    except httpx.ConnectError:
        print(f"\n  ERROR: Cannot connect to {url}. Is the server running?\n")
    except Exception as e:
        print(f"\n  ERROR: {e}\n")


async def _send_server(client: httpx.AsyncClient, args, user_input: str):
    """Send ChatRequest to the backend server."""
    payload = {
        "tenant_id": args.tenant,
        "conversation_id": args.conversation_id,
        "conversation_type": args.conversation_type,
        "message": {
            "role": "user",
            "content": user_input,
            "user_id": args.user_id,
            "user_name": args.user_name,
        },
    }
    if args.full_context:
        payload["with_full_context"] = True

    url = f"{args.server}/agent/chat"
    try:
        resp = await client.post(url, json=payload)
        if resp.status_code == 200:
            data = resp.json()
            print(f"\n[Agent] {data.get('answer', '(no answer)')}")
            refs = data.get("references")
            if refs:
                print("\n  References:")
                for r in refs:
                    print(f"    - [{r.get('title', '')}]({r.get('link', '')})")
            routed = data.get("route_tenant")
            if routed:
                print(f"  (Routed to: {routed})")
            print()
        else:
            print(f"\n  ERROR {resp.status_code}: {resp.text}\n")
    except httpx.ConnectError:
        print(f"\n  ERROR: Cannot connect to {url}. Is the server running?\n")
    except Exception as e:
        print(f"\n  ERROR: {e}\n")


async def _send_local(client: httpx.AsyncClient, args, user_input: str):
    """Send Responses protocol request directly to the local agent."""
    memory_scope = _resolve_memory_scope(args.user_id)
    input_messages = [
        {
            "type": "message",
            "role": "system",
            "content": f"[tenant_context] original_tenant_id={args.tenant}",
        },
        {
            "type": "message",
            "role": "system",
            "content": f"[memory_scope] value={memory_scope}",
        },
        {"type": "message", "role": "user", "content": user_input},
    ]

    payload = {"input": input_messages}

    url = f"{args.server}/responses"
    try:
        resp = await client.post(url, json=payload)
        if resp.status_code == 200:
            data = resp.json()
            # Extract output_text from Responses protocol response
            output_text = data.get("output_text", "")
            if not output_text:
                # Fallback: look in output items for message content
                for item in data.get("output", []):
                    if item.get("type") == "message":
                        for part in item.get("content", []):
                            if part.get("type") == "output_text":
                                output_text += part.get("text", "")
            print(f"\n[Agent] {output_text or '(no output)'}")
            print(f"  Memory scope: {memory_scope}")
            print()
        else:
            print(f"\n  ERROR {resp.status_code}: {resp.text}\n")
    except httpx.ConnectError:
        print(f"\n  ERROR: Cannot connect to {url}. Is the local agent running (F5)?\n")
    except Exception as e:
        print(f"\n  ERROR: {e}\n")


if __name__ == "__main__":
    asyncio.run(main())
