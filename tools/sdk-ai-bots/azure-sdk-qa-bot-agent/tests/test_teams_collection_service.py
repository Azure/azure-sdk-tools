import asyncio
import copy
import json
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import AsyncMock, Mock, patch
from urllib.parse import quote

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.teams_collection_service import (
    TeamsCollectionService,
    continuation_token,
    thread_partition,
)


TENANT = "72f988bf-86f1-41af-91ab-2d7cd011db47"
TENANT_KEY = "typespec_channel_qa_bot"
CHANNEL = {"teamId": "7ccc31f0-b371-450b-a73c-48f5a31a9b96",
           "channelId": "19:test@thread.tacv2", "tenantKey": TENANT_KEY}
CONNECTION = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/test/providers/Microsoft.Web/connections/teams"


def next_link(channel, message_id=None, token="next"):
    path = f"/teams/{channel['teamId']}/channels/{quote(channel['channelId'], safe='')}/messages"
    if message_id:
        path = "/v1.0" + path + f"/{message_id}/replies"
    else:
        path = "/beta" + path
    return f"https://logic-apis-westus2.azure-apim.net/apim/teams/connection{path}?$skiptoken={quote(token, safe='')}"


def graph_message(values):
    message = {
        "messageType": "message",
        "createdDateTime": "2026-09-01T00:00:00Z",
        "from": {"user": {"id": "user-id", "displayName": "Test User"}},
        "body": {"contentType": "html", "content": "<p>body</p>"},
        "webUrl": "https://teams.microsoft.com/l/message/thread/id",
        **values,
    }
    return message


def config_with(**overrides):
    return {"channels": [CHANNEL], "tenantId": TENANT, "maxPages": 100, **overrides}


async def _aiter(rows):
    for row in rows:
        yield copy.deepcopy(row)


class MemoryStore:
    """Stand-in for CosmosMessageStore with the same create/update contract."""

    def __init__(self):
        self.items = {}
        self.creates = 0
        self.updates = 0
        self.validated = 0
        self.conflicts = set()
        self.races = set()

    def seed(self, document):
        self.items[(document["conversation_partition"], document["id"])] = copy.deepcopy(document)

    async def validate(self):
        self.validated += 1

    async def read_thread(self, partition):
        return {document_id: {"id": document_id, "content": document["content"]}
                for (stored, document_id), document in self.items.items() if stored == partition}

    async def create(self, document):
        key = (document["conversation_partition"], document["id"])
        if document["id"] in self.conflicts or key in self.items:
            return False
        self.items[key] = copy.deepcopy(document)
        self.creates += 1
        return True

    async def update_content(self, document_id, partition, content):
        key = (partition, document_id)
        if document_id in self.races or key not in self.items:
            return False
        self.items[key]["content"] = content
        self.updates += 1
        return True


class FakeMessageContainer:
    def __init__(self, documents):
        self.documents = [copy.deepcopy(document) for document in documents]

    def query_items(self, query, parameters=None, partition_key=None):
        values = {parameter["name"]: parameter["value"] for parameter in (parameters or [])}
        if "STARTSWITH" in query:
            return _aiter([
                {"partition": document["conversation_partition"], "ts": document["_ts"]}
                for document in self.documents
                if document["conversation_partition"].startswith(values["@prefix"])
            ])
        return _aiter([document for document in self.documents
                       if document["conversation_partition"] == partition_key])


class FakeSummaryContainer:
    def __init__(self, documents=None):
        self.documents = [copy.deepcopy(document) for document in (documents or [])]
        self.upserts = []
        self.deletes = []

    def query_items(self, query, parameters=None, partition_key=None):
        return _aiter([document for document in self.documents
                       if document["channel_id"] == partition_key])

    async def upsert_item(self, body):
        self.upserts.append(copy.deepcopy(body))
        self.documents = [document for document in self.documents
                          if document["id"] != body["id"]] + [copy.deepcopy(body)]

    async def delete_item(self, item, partition_key):
        from azure.cosmos import exceptions

        remaining = [document for document in self.documents
                     if not (document["id"] == item
                             and document["channel_id"] == partition_key)]
        if len(remaining) == len(self.documents):
            raise exceptions.CosmosResourceNotFoundError(status_code=404)
        self.deletes.append((item, partition_key))
        self.documents = remaining


class FakeProcessor:
    version = "v1"

    def __init__(self, decision=None):
        self.calls = []
        self.decision = decision or {
            "status": "included", "exclusion_reason": None,
            "qa": {"title": "Question", "question": "How?", "answer": "Do this."},
        }

    def is_current(self, processing, digest):
        return bool(
            isinstance(processing, dict)
            and processing.get("processor") == "teams-channel-qa-summary"
            and processing.get("processor_version") == self.version
            and processing.get("source_content_hash") == digest
        )

    async def process(self, channel, post, replies, digest):
        self.calls.append((channel, post, replies, digest))
        # The real processor stamps an offset-style time, so the fake does too.
        return {"processor": "teams-channel-qa-summary", "processor_version": self.version,
                "source_content_hash": digest, "schema_version": 1,
                "processed_at": datetime.now(timezone.utc).isoformat(), **self.decision}


def stored_message(partition, message_id, content, *, ts=1, role="user",
                   created_at="2026-09-01T00:00:00Z", link=None):
    return {"id": message_id, "tenant_id": TENANT_KEY, "sender_role": role,
            "sender_id": "user-id", "sender_name": "Test User", "content": content,
            "created_at": created_at, "conversation_type": "teams_channel",
            "conversation_partition": partition, "document_type": "conversation_message",
            "extra_info": {"channel_id": CHANNEL["channelId"], "message_link": link},
            "_ts": ts}


class TeamsCollectionTests(unittest.IsolatedAsyncioTestCase):
    def test_cli_rejects_local_collection(self):
        from io import StringIO
        from scripts.deploy_teams_collection import main

        with patch.object(sys, "argv", ["deploy_teams_collection.py", "run"]), \
                patch("sys.stderr", new_callable=StringIO) as stderr:
            with self.assertRaises(SystemExit) as failure:
                main()
        self.assertEqual(failure.exception.code, 2)
        self.assertIn("invalid choice: 'run'", stderr.getvalue())

    async def test_cosmos_initialization_failure_closes_client_and_allows_retry(self):
        from azure.cosmos.exceptions import CosmosHttpResponseError
        from utils import azure_cosmosdb

        for error in (CosmosHttpResponseError(status_code=403, message="forbidden"), asyncio.CancelledError()):
            with self.subTest(error=type(error).__name__):
                failed_client = AsyncMock()
                failed_client.__aenter__.side_effect = error
                successful_client = AsyncMock()
                with patch.object(azure_cosmosdb, "_client", None), \
                        patch.object(azure_cosmosdb, "_get_endpoint", return_value="https://account.documents.azure.com"), \
                        patch.object(azure_cosmosdb, "get_credential"), \
                        patch.object(azure_cosmosdb, "cfg", side_effect=lambda key, default: default), \
                        patch.object(azure_cosmosdb, "CosmosClient", side_effect=[failed_client, successful_client]) as factory:
                    with self.assertRaises(type(error)) as failure:
                        await azure_cosmosdb._get_client()
                    self.assertIs(failure.exception, error)
                    failed_client.close.assert_awaited_once()
                    self.assertIsNone(azure_cosmosdb._client)
                    self.assertIs(await azure_cosmosdb._get_client(), successful_client)
                    self.assertIs(await azure_cosmosdb._get_client(), successful_client)
                    self.assertEqual(factory.call_count, 2)
                    successful_client.__aenter__.assert_awaited_once()
                    successful_client.close.assert_not_awaited()
                    await azure_cosmosdb.close_cosmos_client()
                    successful_client.__aexit__.assert_awaited_once_with(None, None, None)
                    self.assertIsNone(azure_cosmosdb._client)

    async def test_backfill_reaches_service_and_store_through_logic_app(self):
        import httpx
        from types import SimpleNamespace
        from unittest.mock import Mock
        from services.teams_collection_service import backfill_configured_channels

        store = MemoryStore()
        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={"operation": "messages", "data": {
            "value": [graph_message({
                "id": "root", "subject": "Need help",
                "replies": [graph_message({"id": "reply", "replyToId": "root"})],
            })],
        }})
        client.__aenter__.return_value = client
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        url = "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"
        settings = Mock(return_value=url)
        with patch("utils.azure_cosmosdb.get_conversation_message_container", new=AsyncMock()), \
                patch("services.teams_collection_service.CosmosMessageStore", return_value=store), \
                patch("utils.azure_credential.get_credential", return_value=credential), \
                patch("services.teams_collection_service.httpx.AsyncClient", return_value=client):
            result = await backfill_configured_channels(config_with(), settings)
        self.assertEqual(result["messagesCreated"], 2)
        self.assertEqual(result["threadsRead"], 1)
        self.assertEqual(store.validated, 1)
        settings.assert_called_once_with("TEAMS_COLLECTION_LOGIC_APP_URL", "")
        self.assertEqual(client.post.call_args.args[0], url)
        self.assertEqual(client.post.call_args.kwargs["json"], {
            "teamId": CHANNEL["teamId"], "channelId": CHANNEL["channelId"],
            "operation": "messages"})
        credential.get_token.assert_awaited_once_with("https://management.core.windows.net/.default")

    async def test_backfill_can_target_one_channel_and_override_start_time(self):
        from services.teams_collection_service import select_channels

        second = {**CHANNEL, "channelId": "19:second@thread.tacv2"}
        channels = [CHANNEL, second]
        self.assertEqual(select_channels(channels, None), channels)
        self.assertEqual(select_channels(channels, second["channelId"]), [second])
        with self.assertRaisesRegex(ValueError, "allowlist"):
            select_channels(channels, "19:unknown@thread.tacv2")

    async def _settled_backfill(self, service, job_id):
        from models.teams_backfill import TeamsBackfillStatus

        for _ in range(500):
            job = service.get(job_id)
            if job is None or job.status is not TeamsBackfillStatus.running:
                return job
            await asyncio.sleep(0.01)
        raise AssertionError("Backfill job never left the running state.")

    async def test_server_accepts_one_backfill_at_a_time_and_reports_its_outcome(self):
        import httpx
        import server
        from services.teams_backfill_service import TeamsBackfillService

        release = asyncio.Event()
        entered = asyncio.Event()
        calls = []

        async def runner(config, settings, channel_id, start_time):
            calls.append((config, settings, channel_id, start_time))
            entered.set()
            await release.wait()
            return {"messagesCreated": 3}

        config_path = (
            Path(__file__).resolve().parents[1] / "config/teams_collection_config.json"
        )
        channel = json.loads(config_path.read_text())["channels"][0]["channelId"]
        service = TeamsBackfillService(config_path, runner)
        with patch.object(server, "_teams_backfill_service", service):
            transport = httpx.ASGITransport(app=server.app)
            async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
                accepted = await client.post(
                    "/teams/backfill",
                    json={"channel_id": channel, "start_time": "2026-01-01T00:00:00Z"},
                )
                self.assertEqual(accepted.status_code, 202, accepted.text)
                job_id = accepted.json()["job_id"]
                self.assertEqual(accepted.json()["status"], "running")
                self.assertIsNone(accepted.json()["summary"])
                await asyncio.wait_for(entered.wait(), 5)

                busy = await client.post("/teams/backfill", json={})
                self.assertEqual(busy.status_code, 409)

                release.set()
                job = await self._settled_backfill(service, job_id)
                self.assertEqual(job.status.value, "succeeded")
                finished = await client.get("/teams/backfill/" + job_id)
                self.assertEqual(finished.status_code, 200)
                self.assertEqual(finished.json()["summary"], {"messagesCreated": 3})
                self.assertIsNone(finished.json()["error"])
                self.assertIsNotNone(finished.json()["completed_at"])

                self.assertEqual(
                    (await client.get("/teams/backfill/missing-job")).status_code, 404)
                for payload in ({"channel_id": "19:unknown@thread.tacv2"},
                                {"start_time": "2026-01-01"}):
                    with self.subTest(payload=payload):
                        rejected = await client.post("/teams/backfill", json=payload)
                        self.assertEqual(rejected.status_code, 422, rejected.text)

        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][2:], (channel, "2026-01-01T00:00:00Z"))
        self.assertEqual(calls[0][0]["channels"][0]["channelId"], channel)

    async def test_backfill_job_reports_failure_without_echoing_raw_error_content(self):
        from models.teams_backfill import TeamsBackfillRequest
        from services.teams_backfill_service import TeamsBackfillService

        config_path = (
            Path(__file__).resolve().parents[1] / "config/teams_collection_config.json"
        )
        service = TeamsBackfillService(
            config_path, AsyncMock(side_effect=RuntimeError("DO_NOT_LOG"))
        )
        with self.assertLogs("services.teams_backfill_service", level="ERROR"):
            accepted = await service.start(TeamsBackfillRequest())
            job = await self._settled_backfill(service, accepted.job_id)
            self.assertEqual(job.status.value, "failed")
            self.assertNotIn("DO_NOT_LOG", job.error)
            self.assertIsNotNone(job.completed_at)
            # A failed run releases the single-flight guard for the next one.
            retried = await service.start(TeamsBackfillRequest())
            self.assertEqual(retried.status.value, "running")
            self.assertEqual(
                (await self._settled_backfill(service, retried.job_id)).status.value, "failed"
            )

    async def test_cli_dispatches_remote_routine_and_closes_credential(self):
        from types import SimpleNamespace
        from scripts.deploy_teams_collection import execute_routine

        config = config_with()
        endpoint = "https://account.services.ai.azure.com/api/projects/project"
        arguments = SimpleNamespace(
            command="routine-dispatch",
            project_endpoint=endpoint,
            appconfig_endpoint=None,
        )
        for fails in (False, True):
            with self.subTest(fails=fails), \
                    patch("config.app_config.init", new=AsyncMock()) as initialize, \
                    patch("scripts.deploy_teams_collection.routine_request", new=AsyncMock(
                        return_value={"dispatch_id": "dispatch"},
                        side_effect=RuntimeError("dispatch failed") if fails else None,
                    )) as dispatch, \
                    patch("utils.azure_credential.get_credential"), \
                    patch("utils.azure_credential.close_credential", new=AsyncMock()) as close_credential:
                if fails:
                    with self.assertRaisesRegex(RuntimeError, "dispatch failed"):
                        await execute_routine(arguments, config)
                else:
                    self.assertEqual(
                        await execute_routine(arguments, config),
                        {"dispatch_id": "dispatch"},
                    )
            initialize.assert_not_awaited()
            self.assertEqual(dispatch.call_args.args[:3], (config, "routine-dispatch", endpoint))
            close_credential.assert_awaited_once()

    async def test_summarize_failure_propagates_without_raw_error_content(self):
        from agents.teams_collection_agent.init import TeamsCollectionAgent

        summarize = AsyncMock(side_effect=RuntimeError("DO_NOT_LOG"))
        agent = TeamsCollectionAgent(summarize)
        with self.assertRaises(RuntimeError) as failure, self.assertLogs(
            "agents.teams_collection_agent.init", level="ERROR"
        ):
            await agent.run('{"operation": "summarize"}')
        self.assertNotIn("DO_NOT_LOG", str(failure.exception))
        self.assertIn("summarize", str(failure.exception))

    async def test_hosted_http_returns_before_work_and_exposes_final_response(self):
        import httpx
        from agents.teams_collection_agent.init import TeamsCollectionAgent, create_server

        release = asyncio.Event()
        completed = asyncio.Event()

        async def summarize(request):
            await release.wait()
            completed.set()
            return {"threadsSummarized": 2}

        server = create_server(TeamsCollectionAgent(summarize))
        async with server.router.lifespan_context(server):
            async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server), base_url="http://test") as client:
                try:
                    response = await asyncio.wait_for(client.post(
                        "/responses", json={"input": {"operation": "summarize"}}), 5)
                    self.assertEqual(response.status_code, 200, response.text)
                    self.assertIn(response.json()["status"], ("queued", "in_progress"))
                    self.assertTrue(response.json()["id"].startswith("caresp_"))
                    self.assertFalse(completed.is_set())
                finally:
                    release.set()
                await asyncio.wait_for(completed.wait(), 5)
                rejected = await client.post("/responses", json={"input": "collect everything"})
                self.assertEqual(rejected.status_code, 400)
                self.assertNotIn("collect everything", rejected.text)
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server), base_url="http://test") as client:
            final = await client.get("/responses/" + response.json()["id"])
            self.assertEqual(final.status_code, 200)
            self.assertEqual(final.json()["status"], "completed")
            self.assertEqual(json.loads(final.json()["output"][0]["content"][0]["text"]),
                             {"threadsSummarized": 2})

    async def test_hosted_agent_only_dispatches_summarize_requests(self):
        from agents.teams_collection_agent.init import TeamsCollectionAgent
        from agent_framework_foundry_hosting import ResponsesHostServer

        summarize = AsyncMock(return_value={"threadsSummarized": 4})
        agent = TeamsCollectionAgent(summarize)

        response = await agent.run('{"operation": "summarize"}')
        self.assertEqual(json.loads(response.text), {"threadsSummarized": 4})
        updates = [update async for update in agent.run(
            '{"operation": "summarize", "channelId": "19:test@thread.tacv2"}', stream=True)]
        self.assertEqual(json.loads(updates[0].text), {"threadsSummarized": 4})
        self.assertEqual(summarize.await_args.args[0], {
            "operation": "summarize", "channelId": "19:test@thread.tacv2"})
        with self.assertRaisesRegex(ValueError, "summarize"):
            await agent.run('{"operation": "backfill"}')
        self.assertEqual(summarize.await_count, 2)
        self.assertIsNotNone(ResponsesHostServer(agent))

    def test_operation_requests_reject_free_text_and_unknown_fields(self):
        from services.teams_operations import operation_input, parse_operation

        self.assertEqual(parse_operation({"operation": "summarize"}), {"operation": "summarize"})
        self.assertEqual(
            operation_input({"channelId": "19:test@thread.tacv2", "operation": "summarize"}),
            '{"channelId":"19:test@thread.tacv2","operation":"summarize"}',
        )
        for value in (None, "", "collect", "[]", {"operation": "delete"},
                      {"operation": "backfill"},
                      {"operation": "summarize", "startTime": "2026-01-01T00:00:00Z"},
                      {"operation": "summarize", "channelId": " "},
                      {"operation": "summarize", "unexpected": 1}):
            with self.subTest(value=value), self.assertRaises(ValueError):
                parse_operation(value)

    async def test_only_summarization_is_scheduled_and_the_routine_starts_paused(self):
        import httpx
        from types import SimpleNamespace
        from scripts.deploy_teams_collection import routine_request, routine_definition

        config = json.loads(
            (Path(__file__).resolve().parents[1] / "config/teams_collection_config.json").read_text()
        )
        self.assertNotIn("routines", config)
        self.assertEqual(config["routine"]["name"], "teams-channel-qa-summary")
        definition = routine_definition(config)
        self.assertFalse(definition["enabled"])
        self.assertEqual(definition["authorization"], {"identity": "agent"})
        self.assertEqual(definition["triggers"]["schedule"]["cron_expression"], "0 0 * * 0")
        self.assertEqual(definition["action"]["input"], '{"operation":"summarize"}')
        with self.assertRaisesRegex(ValueError, "No summarization routine"):
            routine_definition({"channels": []})

        client = AsyncMock()
        client.get.return_value = httpx.Response(404)
        client.put.return_value = httpx.Response(201, json={"name": "teams-channel-qa-summary", "enabled": False})
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        endpoint = "https://account.services.ai.azure.com/api/projects/project"
        await routine_request(config, "routine-create", endpoint, credential, client)
        self.assertEqual(client.put.call_args.args[0], endpoint + "/routines/teams-channel-qa-summary")
        self.assertEqual(client.put.call_args.kwargs["json"], definition)
        self.assertEqual(client.put.call_args.kwargs["params"], {"api-version": "v1"})
        self.assertEqual(
            client.put.call_args.kwargs["headers"]["Foundry-Features"],
            "Routines=V2Preview",
        )
        client.get.return_value = httpx.Response(200, json=definition)
        client.post.return_value = httpx.Response(202, json={"dispatch_id": "dispatch"})
        await routine_request(config, "routine-dispatch", endpoint, credential, client)
        self.assertTrue(client.post.call_args.args[0].endswith(":dispatch_async"))
        client.get.return_value = httpx.Response(200, json={"action": {"agent_name": "another-agent"}})
        with self.assertRaisesRegex(ValueError, "different agent"):
            await routine_request(config, "routine-enable", endpoint, credential, client)

    def test_cli_no_longer_exposes_backfill_and_rejects_untrusted_endpoints(self):
        import scripts.deploy_teams_collection as cli

        for removed in ("start_backfill", "backfill_status", "execute_backfill",
                        "_agent_responses", "_response_result"):
            with self.subTest(removed=removed):
                self.assertFalse(hasattr(cli, removed))
        for endpoint in ("http://account.services.ai.azure.com/api/projects/project",
                         "https://account.example.com/api/projects/project",
                         "https://account.services.ai.azure.com/api/projects/project/routines"):
            with self.subTest(endpoint=endpoint), self.assertRaisesRegex(
                ValueError, "Foundry project endpoint"
            ):
                cli._validate_project_endpoint(endpoint)
        self.assertEqual(
            cli._principal_id("F8EC2208-D3C9-4271-B787-07C26651E1C6", "--backfill-principal-id"),
            "f8ec2208-d3c9-4271-b787-07c26651e1c6",
        )
        for value in (None, "", "not-a-guid"):
            with self.subTest(value=value), self.assertRaisesRegex(
                ValueError, "--backfill-principal-id"
            ):
                cli._principal_id(value, "--backfill-principal-id")

    def test_template_enforces_identity_channel_allowlist_and_read_only_actions(self):
        project = Path(__file__).resolve().parents[1]
        template = json.loads((project / "pipelines/teams-collection/template.json").read_text())
        container = next(resource for resource in template["resources"]
                         if resource["type"].endswith("/containers"))
        workflow = next(resource for resource in template["resources"]
                        if resource["type"] == "Microsoft.Logic/workflows")
        self.assertEqual(container["properties"]["resource"]["id"], "teams-qa-summaries")
        self.assertEqual(container["properties"]["resource"]["partitionKey"]["paths"], ["/channel_id"])
        access = workflow["properties"]["accessControl"]["triggers"]
        self.assertEqual(access["sasAuthenticationPolicy"]["state"], "Disabled")
        policies = access["openAuthenticationPolicies"]["policies"]
        self.assertEqual(list(policies), ["backfill"])
        self.assertEqual({claim["name"] for claim in policies["backfill"]["claims"]},
                         {"iss", "aud", "oid"})
        claims = {
            claim["name"]: claim["value"] for claim in policies["backfill"]["claims"]
        }
        self.assertEqual(claims["aud"], "https://management.core.windows.net")
        self.assertEqual(claims["oid"], "[parameters('backfillPrincipalId')]")
        definition = workflow["properties"]["definition"]
        self.assertEqual(list(definition["triggers"]), ["manual"])
        self.assertEqual(definition["triggers"]["manual"]["operationOptions"], "EnableSchemaValidation")
        authorize = definition["actions"]["Authorize_channel"]
        self.assertIn("contains(parameters('allowedChannels')", authorize["expression"])
        cases = authorize["actions"]["Select_operation"]["cases"]
        for name, operation, version in (("messages", "GetMessagesFromChannel", "/beta/"),
                                         ("replies", "ListRepliesToMessage", "/v1.0/")):
            action = cases[name]["actions"][operation]
            self.assertEqual(action["type"], "ApiConnection")
            self.assertEqual(action["inputs"]["method"], "get")
            self.assertTrue(action["inputs"]["path"].startswith(version))
            self.assertIn("$skiptoken", action["inputs"]["queries"])
            self.assertEqual(action["runtimeConfiguration"]["secureData"]["properties"], ["inputs", "outputs"])
        self.assertIn('$expand', cases["messages"]["actions"]["GetMessagesFromChannel"]["inputs"]["queries"])
        self.assertIn('$top', cases["messages"]["actions"]["GetMessagesFromChannel"]["inputs"]["queries"])
        for name, operation, filter_name, return_name in (
                ("messages", "GetMessagesFromChannel", "Filter_messages", "Return_messages"),
                ("replies", "ListRepliesToMessage", "Filter_replies", "Return_replies")):
            actions = cases[name]["actions"]
            filter_action = actions[filter_name]
            self.assertEqual(filter_action["type"], "Query")
            self.assertEqual(filter_action["inputs"]["from"], f"@body('{operation}')?['value']")
            self.assertEqual(filter_action["inputs"]["where"],
                             "@equals(item()?['messageType'], 'message')")
            self.assertEqual(filter_action["runtimeConfiguration"]["secureData"]["properties"],
                             ["inputs", "outputs"])
            self.assertEqual(filter_action["runAfter"], {operation: ["Succeeded"]})
            response = actions[return_name]
            self.assertEqual(response["runAfter"], {filter_name: ["Succeeded"]})
            self.assertEqual(
                response["inputs"]["body"]["data"],
                f"@setProperty(body('{operation}'), 'value', body('{filter_name}'))",
            )

    def test_environment_parameter_files_match_collection_config(self):
        project = Path(__file__).resolve().parents[1]
        config = json.loads((project / "config/teams_collection_config.json").read_text())
        allowed_channels = [
            f"{channel['teamId']}|{channel['channelId']}" for channel in config["channels"]
        ]
        expected = {
            "dev": ("azuresdkqabot-dev-teams-collection", "azure-sdk-qa-bot-dev",
                    "azuresdkqabot-dev-db"),
            "test": ("azuresdkqabot-test-teams-collection", "azure-sdk-qa-bot",
                     "azuresdkqabot-db"),
            "prod": ("azuresdkqabot-teams-collection", "azure-sdk-qa-bot",
                     "azuresdkqabot-db"),
        }
        for environment, (workflow_name, connection_group, cosmos_account) in expected.items():
            with self.subTest(environment=environment):
                parameter_file = project / (
                    f"pipelines/teams-collection/parameters.azure_sdk.{environment}.json"
                )
                parameters = json.loads(parameter_file.read_text())["parameters"]
                self.assertEqual(parameters["logicAppName"]["value"], workflow_name)
                self.assertIn(f"/resourceGroups/{connection_group}/",
                              parameters["teamsConnectionResourceId"]["value"])
                self.assertEqual(parameters["tenantId"]["value"], config["tenantId"])
                self.assertEqual(parameters["allowedChannels"]["value"], allowed_channels)
                self.assertEqual(parameters["cosmosAccountName"]["value"], cosmos_account)
                self.assertNotIn("collectorPrincipalId", parameters)
                self.assertNotIn("backfillPrincipalId", parameters)

    def test_template_grants_metadata_message_and_summary_data_access(self):
        project = Path(__file__).resolve().parents[1]
        template = json.loads((project / "pipelines/teams-collection/template.json").read_text())
        definitions = [resource for resource in template["resources"]
                       if resource["type"].endswith("/sqlRoleDefinitions")]
        self.assertEqual(len(definitions), 1)
        self.assertEqual(template["variables"]["metadataRoleName"],
                         "41cd8d60-edff-4171-9ff9-e7e1810d045b")
        self.assertEqual(definitions[0]["properties"]["type"], "CustomRole")
        self.assertEqual(definitions[0]["properties"]["permissions"], [
            {"dataActions": ["Microsoft.DocumentDB/databaseAccounts/readMetadata"]}])
        assignments = [resource for resource in template["resources"]
                       if resource["type"].endswith("/sqlRoleAssignments")]
        self.assertEqual(len(assignments), 3)
        metadata, summaries, messages = assignments
        self.assertEqual(metadata["properties"]["scope"], "[variables('cosmosAccountResourceId')]")
        self.assertIn("variables('metadataRoleName')", metadata["properties"]["roleDefinitionId"])
        self.assertEqual(summaries["properties"]["scope"],
                         "[concat(variables('cosmosAccountResourceId'), '/dbs/azure-sdk-qa-bot/colls/teams-qa-summaries')]")
        self.assertEqual(messages["properties"]["scope"],
                         "[concat(variables('cosmosAccountResourceId'), '/dbs/azure-sdk-qa-bot/colls/conversation-messages')]")
        self.assertIn("00000000-0000-0000-0000-000000000002",
                      summaries["properties"]["roleDefinitionId"])
        # Backfill moved to the backend server, so the agent only reads messages.
        self.assertIn("00000000-0000-0000-0000-000000000001",
                      messages["properties"]["roleDefinitionId"])
        for assignment in assignments:
            self.assertEqual(assignment["properties"]["principalId"], "[parameters('collectorPrincipalId')]")
            self.assertIn("guid(", assignment["name"])

    def test_collection_deployment_resolves_identity_and_sanitizes_callback_url(self):
        from types import SimpleNamespace
        from scripts.deploy_teams_collection import (
            _appconfig_name,
            _collector_principal_id,
            _sas_free_callback_url,
        )

        principal = "00000000-0000-0000-0000-000000000003"
        self.assertEqual(
            _collector_principal_id(SimpleNamespace(
                instance_identity={"principal_id": principal, "client_id": principal}
            )),
            principal,
        )
        self.assertEqual(
            _appconfig_name("https://azuresdkqabot-dev-config.azconfig.io"),
            "azuresdkqabot-dev-config",
        )
        self.assertEqual(
            _sas_free_callback_url(
                "https://prod-01.westus2.logic.azure.com/workflows/id/"
                "triggers/manual/paths/invoke"
                "?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=secret"
            ),
            "https://prod-01.westus2.logic.azure.com/workflows/id/"
            "triggers/manual/paths/invoke?api-version=2016-10-01",
        )
        for value in (
            "https://example.com/triggers/manual/paths/invoke?api-version=2016-10-01",
            "https://prod-01.westus2.logic.azure.com/triggers/manual/paths/invoke?sig=secret",
        ):
            with self.subTest(value=value), self.assertRaises(ValueError):
                _sas_free_callback_url(value)

    def test_collection_reuses_the_shared_agent_and_logic_app_pipelines(self):
        """Collection has no pipeline of its own; it extends the two shared ones."""
        project = Path(__file__).resolve().parents[1]
        agent_pipeline = (project / "pipelines/agent-cd.yml").read_text()
        logicapp_pipeline = (project / "pipelines/logicapp-cd.yml").read_text()

        self.assertFalse((project / "pipelines/teams-collection-cd.yml").exists())

        self.assertIn("- teams_collection_agent", agent_pipeline)
        self.assertIn("python scripts/deploy_hosted_agent.py", agent_pipeline)
        self.assertNotIn("deploy_teams_collection.py", agent_pipeline)

        self.assertIn("- teams-collection", logicapp_pipeline)
        self.assertIn("python scripts/deploy_teams_collection.py deploy", logicapp_pipeline)
        self.assertIn(
            "--appconfig-endpoint \"$(AZURE_APPCONFIG_ENDPOINT)\"", logicapp_pipeline
        )
        # The chat template must stay behind its own branch of the selector.
        self.assertIn(
            "${{ if eq(parameters.workflow, 'chat') }}", logicapp_pipeline
        )
        self.assertIn(
            "${{ if eq(parameters.workflow, 'teams-collection') }}", logicapp_pipeline
        )
        self.assertNotIn("deploy_hosted_agent.py", logicapp_pipeline)

    def test_normalization_matches_the_realtime_workflow(self):
        from services.teams_collection_service import normalized_content, split_subject

        forwarded = graph_message({
            "id": "root", "subject": "  Need help  ",
            "body": {"content": '<p>See <attachment id="a1"></attachment></p>'},
            "attachments": [
                {"id": "a1", "contentType": "forwardedMessageReference",
                 "content": json.dumps({"body": {"content": "<p>forwarded</p>"}})},
                {"id": "a2", "contentType": "reference", "content": "ignored"},
            ],
        })
        self.assertEqual(normalized_content(forwarded),
                         "title: Need help\n\n<p>See <p>forwarded</p></p>")
        self.assertEqual(split_subject(normalized_content(forwarded)),
                         ("Need help", "<p>See <p>forwarded</p></p>"))
        self.assertEqual(
            normalized_content(graph_message({
                "id": "reply", "body": {"content": "<p>plain</p>"}})),
            "<p>plain</p>",
        )
        self.assertEqual(split_subject("<p>plain</p>"), (None, "<p>plain</p>"))
        unparsable = graph_message({
            "id": "root", "subject": None,
            "body": {"content": '<attachment id="a1"></attachment>'},
            "attachments": [{"id": "a1", "contentType": "forwardedMessageReference",
                             "content": "not json"}],
        })
        self.assertEqual(normalized_content(unparsable), "not json")

    async def test_backfill_writes_one_document_per_message_in_the_bot_shape(self):
        fetch = AsyncMock(return_value={"value": [graph_message({
            "id": "root", "subject": "Need help",
            "replies": [graph_message({
                "id": "reply", "replyToId": "root",
                "createdDateTime": "2026-09-01T01:00:00Z",
                "from": {"application": {"id": "bot-id", "displayName": "QA Bot"}},
            })],
        })]})
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store).backfill([CHANNEL])

        partition = thread_partition(CHANNEL["channelId"], "root")
        self.assertEqual(result, {"channelsCompleted": 1, "threadsRead": 1, "messagesRead": 2,
                                  "messagesCreated": 2, "messagesUpdated": 0,
                                  "messagesUnchanged": 0, "messagesSkipped": 0})
        root = store.items[(partition, "root")]
        reply = store.items[(partition, "reply")]
        self.assertEqual(root, {
            "id": "root", "tenant_id": TENANT_KEY, "sender_role": "user",
            "sender_id": "user-id", "sender_name": "Test User",
            "content": "title: Need help\n\n<p>body</p>",
            "created_at": "2026-09-01T00:00:00Z",
            "conversation_id": f"{CHANNEL['channelId']};messageid=root",
            "conversation_type": "teams_channel",
            "extra_info": {"channel_id": CHANNEL["channelId"],
                           "message_link": "https://teams.microsoft.com/l/message/thread/id"},
            "conversation_partition": partition,
            "document_type": "conversation_message",
        })
        self.assertEqual(reply["conversation_partition"], partition)
        self.assertEqual(reply["conversation_id"], root["conversation_id"])
        self.assertEqual(reply["sender_role"], "system")
        self.assertEqual(reply["sender_name"], "QA Bot")
        self.assertEqual(reply["content"], "<p>body</p>")
        self.assertNotIn("should_reply", root)
        self.assertNotIn("trace_id", root)

    async def test_existing_messages_are_kept_and_only_edited_content_is_updated(self):
        root = graph_message({
            "id": "root", "subject": "Need help",
            "body": {"content": '<p>See <attachment id="a1"></attachment></p>'},
            "attachments": [{"id": "a1", "contentType": "forwardedMessageReference",
                             "content": json.dumps({"body": {"content": "<p>quoted</p>"}})}],
        })
        reply = graph_message({"id": "reply", "replyToId": "root",
                               "body": {"content": "<p>original</p>"}})
        edited = {**reply, "body": {"content": "<p>edited</p>"}}
        edited_root = {**root, "body": {"content": "<p>updated</p>"}}
        fetch = AsyncMock(side_effect=[
            {"value": [{**root, "replies": [reply]}]},
            {"value": [{**root, "replies": [reply]}]},
            {"value": [{**edited_root, "replies": [edited]}]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store)

        partition = thread_partition(CHANNEL["channelId"], "root")
        created = await service.backfill([CHANNEL])
        imported = store.items[(partition, "root")]["content"]
        unchanged = await service.backfill([CHANNEL])
        updated = await service.backfill([CHANNEL])

        self.assertEqual(created["messagesCreated"], 2)
        self.assertEqual(imported, "title: Need help\n\n<p>See <p>quoted</p></p>")
        self.assertEqual(unchanged["messagesUnchanged"], 2)
        self.assertEqual(unchanged["messagesCreated"], 0)
        self.assertEqual(updated["messagesUpdated"], 2)
        self.assertEqual(store.creates, 2)
        self.assertEqual(store.updates, 2)
        self.assertEqual(store.items[(partition, "reply")]["content"], "<p>edited</p>")
        self.assertEqual(store.items[(partition, "root")]["content"],
                         "title: Need help\n\n<p>updated</p>")

    async def test_bot_messages_and_concurrent_writes_are_never_overwritten(self):
        root = graph_message({"id": "root", "body": {"content": "<p>edited</p>"}})
        fetch = AsyncMock(return_value={"value": [{**root, "replies": []}]})
        store = MemoryStore()
        partition = thread_partition(CHANNEL["channelId"], "root")
        store.seed(stored_message(partition, "bot-answer", "<p>bot answer</p>", role="system"))
        store.seed({**stored_message(partition, "root", "<p>original</p>"),
                    "should_reply": True, "trace_id": "trace"})
        store.races.add("root")

        result = await TeamsCollectionService(fetch, store).backfill([CHANNEL])

        self.assertEqual(result["messagesRead"], 1)
        self.assertEqual(result["messagesSkipped"], 1)
        self.assertEqual(result["messagesUpdated"], 0)
        self.assertEqual(store.items[(partition, "bot-answer")]["content"], "<p>bot answer</p>")
        self.assertEqual(store.items[(partition, "root")]["content"], "<p>original</p>")
        self.assertTrue(store.items[(partition, "root")]["should_reply"])

    async def test_cosmos_store_creates_updates_and_yields_to_concurrent_writers(self):
        from unittest.mock import Mock
        from azure.core import MatchConditions
        from azure.cosmos.exceptions import (
            CosmosAccessConditionFailedError,
            CosmosResourceExistsError,
            CosmosResourceNotFoundError,
        )
        from services.teams_collection_service import CosmosMessageStore

        container = AsyncMock()
        container.read.return_value = {"partitionKey": {"paths": ["/conversation_partition"]}}
        store = CosmosMessageStore(container)
        await store.validate()
        container.read.return_value = {"partitionKey": {"paths": ["/channel_key"]}}
        with self.assertRaisesRegex(ValueError, "/conversation_partition"):
            await store.validate()

        document = {"id": "message", "conversation_partition": "partition"}
        self.assertTrue(await store.create(document))
        container.create_item.assert_awaited_once_with(body=document)
        container.create_item.side_effect = CosmosResourceExistsError(status_code=409, message="exists")
        self.assertFalse(await store.create(document))

        container.read_item.return_value = {
            "id": "message", "conversation_partition": "partition",
            "content": "old", "should_reply": True, "_etag": "version1",
        }
        self.assertTrue(await store.update_content("message", "partition", "new"))
        replaced = container.replace_item.call_args.kwargs
        self.assertEqual(replaced["body"]["content"], "new")
        self.assertTrue(replaced["body"]["should_reply"])
        self.assertEqual(replaced["etag"], "version1")
        self.assertEqual(replaced["match_condition"], MatchConditions.IfNotModified)
        container.replace_item.reset_mock()
        self.assertFalse(await store.update_content("message", "partition", "new"))
        container.replace_item.assert_not_awaited()
        container.replace_item.side_effect = CosmosAccessConditionFailedError(
            status_code=412, message="conflict")
        self.assertFalse(await store.update_content("message", "partition", "newer"))
        container.read_item.side_effect = CosmosResourceNotFoundError(status_code=404, message="gone")
        self.assertFalse(await store.update_content("message", "partition", "newer"))

        async def items():
            yield {"id": "message", "content": "stored"}

        container.query_items = Mock(return_value=items())
        self.assertEqual(await store.read_thread("partition"),
                         {"message": {"id": "message", "content": "stored"}})
        self.assertEqual(container.query_items.call_args.kwargs["partition_key"], "partition")
        self.assertIn("c.content", container.query_items.call_args.kwargs["query"])

    async def test_start_time_skips_older_posts_and_stops_after_the_activity_cutoff(self):
        channel = {**CHANNEL, "startTime": "2026-09-01T08:00:00+08:00"}
        fetch = AsyncMock(side_effect=[
            {"value": [
                graph_message({
                    "id": "old-but-active", "createdDateTime": "2026-08-31T23:59:59Z",
                    "lastModifiedDateTime": "2026-09-10T00:00:00Z", "replies": [],
                }),
                graph_message({
                    "id": "boundary", "createdDateTime": "2026-09-01T00:00:00Z",
                    "replies": [graph_message({
                        "id": "reply", "replyToId": "boundary",
                        "createdDateTime": "2026-09-02T00:00:00Z"})],
                }),
             ],
             "@odata.nextLink": next_link(channel)},
            {"value": [
                graph_message({"id": "quiet", "createdDateTime": "2026-08-01T00:00:00Z",
                               "lastModifiedDateTime": "2026-08-01T00:00:00Z", "replies": []}),
                graph_message({"id": "never-read", "replies": []}),
             ]},
        ])
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store).backfill([channel])

        self.assertEqual(result["threadsRead"], 1)
        self.assertEqual(result["messagesCreated"], 2)
        self.assertEqual(fetch.await_count, 2)
        self.assertEqual({document_id for _partition, document_id in store.items},
                         {"boundary", "reply"})

    async def test_invalid_start_time_and_missing_tenant_key_fail_before_fetch(self):
        for value in ("", "invalid", "2026-09-01", "2026-09-01T00:00:00", 123, False):
            with self.subTest(startTime=value):
                fetch = AsyncMock()
                store = MemoryStore()
                with self.assertRaisesRegex(ValueError, "startTime"):
                    await TeamsCollectionService(fetch, store).backfill(
                        [{**CHANNEL, "startTime": value}])
                fetch.assert_not_awaited()
                self.assertFalse(store.items)
        for value in (None, "", "  ", 7):
            with self.subTest(tenantKey=value):
                fetch = AsyncMock()
                with self.assertRaisesRegex(ValueError, "tenantKey"):
                    await TeamsCollectionService(fetch, MemoryStore()).backfill(
                        [{**CHANNEL, "tenantKey": value}])
                fetch.assert_not_awaited()

    async def test_post_without_a_creation_time_is_rejected(self):
        channel = {**CHANNEL, "startTime": "2026-09-01T00:00:00Z"}
        for value in (None, "invalid", "2026-09-02T00:00:00"):
            with self.subTest(value=value):
                root = graph_message({"id": "root", "replies": []})
                root["createdDateTime"] = value
                fetch = AsyncMock(return_value={"value": [root]})
                store = MemoryStore()
                with self.assertRaisesRegex(ValueError, "createdDateTime"):
                    await TeamsCollectionService(fetch, store).backfill([channel])
                self.assertFalse(store.items)

    async def test_non_message_posts_and_replies_are_not_stored(self):
        fetch = AsyncMock(return_value={"value": [
            {"id": "system-root", "messageType": "systemEventMessage"},
            graph_message({
                "id": "root",
                "replies": [
                    {"id": "system-reply", "messageType": "systemEventMessage"},
                    graph_message({"id": "reply", "replyToId": "root"}),
                ],
            }),
        ]})
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store).backfill([CHANNEL])

        self.assertEqual(result["threadsRead"], 1)
        self.assertEqual(result["messagesCreated"], 2)
        self.assertEqual({document_id for _partition, document_id in store.items},
                         {"root", "reply"})

    async def test_partial_expansion_falls_back_to_full_reply_paging(self):
        root = graph_message({"id": "root"})
        reply1 = graph_message({"id": "reply1", "replyToId": "root",
                                "createdDateTime": "2026-09-01T01:00:00Z"})
        reply2 = graph_message({"id": "reply2", "replyToId": "root",
                                "createdDateTime": "2026-09-01T02:00:00Z"})
        fetch = AsyncMock(side_effect=[
            {"value": [{**root, "replies": [reply1], "replies@odata.count": 1,
                        "replies@odata.nextLink": next_link(CHANNEL, "root")}]},
            {"value": [reply1], "@odata.nextLink": next_link(CHANNEL, "root")},
            {"value": [reply2]},
        ])
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store).backfill([CHANNEL])

        self.assertEqual(result["messagesCreated"], 3)
        self.assertEqual(fetch.call_args_list[1].args, (CHANNEL, "root", None))
        self.assertEqual(fetch.call_args_list[2].args, (CHANNEL, "root", "next"))

    async def test_collects_multiple_channels_and_all_root_and_reply_pages(self):
        second = {**CHANNEL, "channelId": "19:second@thread.tacv2"}
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message({"id": "root"})], "@odata.nextLink": next_link(CHANNEL)},
            {"value": [graph_message({"id": "reply1", "replyToId": "root"})],
             "@odata.nextLink": next_link(CHANNEL, "root")},
            {"value": [graph_message({"id": "reply2", "replyToId": "root"})]},
            {"value": [graph_message({"id": "older"})]}, {"value": []},
            {"value": [graph_message({"id": "root"})]}, {"value": []},
        ])
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store).backfill([CHANNEL, second])

        self.assertEqual(result["channelsCompleted"], 2)
        self.assertEqual(result["threadsRead"], 3)
        self.assertEqual(result["messagesCreated"], 5)
        self.assertEqual(fetch.call_args_list[2].args, (CHANNEL, "root", "next"))
        self.assertEqual(fetch.call_args_list[3].args, (CHANNEL, None, "next"))
        self.assertEqual(
            store.items[(thread_partition(second["channelId"], "root"), "root")]
            ["conversation_partition"],
            thread_partition(second["channelId"], "root"),
        )

    async def test_reply_failure_never_writes_a_partial_thread(self):
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message({"id": "root"})]},
            {"value": [], "@odata.nextLink": next_link(CHANNEL, "root")},
            RuntimeError("unavailable"),
        ])
        store = MemoryStore()
        with self.assertRaisesRegex(RuntimeError, "unavailable"):
            await TeamsCollectionService(fetch, store).backfill([CHANNEL])
        self.assertFalse(store.items)

    async def test_reply_page_limit_and_wrong_parent_do_not_save_partial_thread(self):
        for page in ({"value": [], "@odata.nextLink": next_link(CHANNEL, "root")},
                     {"value": [graph_message({"id": "reply", "replyToId": "other"})]}):
            store = MemoryStore()
            fetch = AsyncMock(side_effect=[{"value": [graph_message({"id": "root"})]}, page])
            with self.assertRaises((RuntimeError, ValueError)):
                await TeamsCollectionService(fetch, store, max_pages=1).backfill([CHANNEL])
            self.assertFalse(store.items)

    async def test_repeated_continuation_fails_without_writing_partial_replies(self):
        page = {"value": [], "@odata.nextLink": next_link(CHANNEL, "root")}
        fetch = AsyncMock(side_effect=[{"value": [graph_message({"id": "root"})]}, page, page])
        store = MemoryStore()
        with self.assertRaisesRegex(RuntimeError, "repeated"):
            await TeamsCollectionService(fetch, store).backfill([CHANNEL])
        self.assertFalse(store.items)

    def test_continuation_is_parsed_without_following_untrusted_urls(self):
        self.assertEqual(continuation_token(next_link(CHANNEL, token="a+b/=c"), CHANNEL, None), "a+b/=c")
        self.assertEqual(continuation_token(next_link(CHANNEL) + "&$expand=replies", CHANNEL, None), "next")
        replies_link = next_link(CHANNEL, "root").replace(
            "logic-apis-westus2.azure-apim.net/apim/teams/connection", "graph.microsoft.com")
        self.assertEqual(continuation_token(replies_link, CHANNEL, "root"), "next")
        for link in (next_link(CHANNEL).replace("logic-apis-westus2.azure-apim.net", "example.com"),
                     next_link(CHANNEL).replace("/messages?", "/other?"), replies_link,
                     next_link(CHANNEL) + "&$expand=unexpected",
                     next_link(CHANNEL) + "&$expand=replies&$expand=replies"):
            with self.assertRaises(ValueError):
                continuation_token(link, CHANNEL, None)

    async def test_logic_app_passes_only_configured_channel_and_continuation(self):
        import httpx
        from types import SimpleNamespace
        from services.teams_collection_service import LogicAppPageClient

        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={"operation": "replies", "data": {"value": []}})
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        url = "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"
        channel = {**CHANNEL, "startTime": "2026-09-01T00:00:00Z"}
        pages = LogicAppPageClient(client, credential, url, "https://management.core.windows.net/", [channel])
        self.assertEqual(await pages.fetch_page(channel, "root", "continuation"), {"value": []})
        self.assertEqual(client.post.call_args.kwargs["json"], {
            "teamId": CHANNEL["teamId"], "channelId": CHANNEL["channelId"],
            "operation": "replies", "messageId": "root", "skipToken": "continuation"})
        self.assertFalse(client.post.call_args.kwargs["follow_redirects"])
        credential.get_token.assert_awaited_with(
            "https://management.core.windows.net/.default")
        self.assertEqual(
            client.post.call_args.kwargs["headers"], {"Authorization": "Bearer secret"})
        with self.assertRaises(ValueError):
            await pages.fetch_page({**CHANNEL, "channelId": "unapproved"}, None, None)
        with self.assertRaises(ValueError):
            LogicAppPageClient(client, credential, url + "&sig=secret", "https://management.core.windows.net/", [CHANNEL])
        client.post.return_value = httpx.Response(403, json={"error": "secret"})
        with self.assertRaisesRegex(RuntimeError, "HTTP 403") as failure:
            await pages.fetch_page(channel, None, None)
        self.assertNotIn("secret", str(failure.exception))

    async def test_logic_app_retries_transient_failures(self):
        import httpx
        from types import SimpleNamespace
        from services.teams_collection_service import LogicAppPageClient

        url = "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        success = httpx.Response(200, json={"operation": "messages", "data": {"value": []}})
        for transient in (httpx.ReadTimeout("timed out"), httpx.Response(503)):
            with self.subTest(transient=type(transient).__name__):
                client = AsyncMock()
                client.post.side_effect = [transient, success]
                pages = LogicAppPageClient(
                    client, credential, url, "https://management.core.windows.net/", [CHANNEL]
                )
                with patch("services.teams_collection_service.asyncio.sleep", new=AsyncMock()) as sleep:
                    self.assertEqual(await pages.fetch_page(CHANNEL, None, None), {"value": []})
                self.assertEqual(client.post.await_count, 2)
                sleep.assert_awaited_once_with(1)

    async def test_summary_runs_the_model_once_per_meaningful_thread_change(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        other = thread_partition("19:other@thread.tacv2", "root")
        messages = FakeMessageContainer([
            stored_message(partition, "root", "title: Question\n\n<p>How?</p>", ts=10),
            stored_message(partition, "reply", "<p>Do this.</p>", ts=11,
                           created_at="2026-09-01T01:00:00Z"),
            stored_message(other, "root", "<p>another channel</p>", ts=12),
        ])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor()
        service = TeamsQASummaryService(messages, summaries, processor)

        first = await service.summarize([channel])
        second = await service.summarize([channel])

        self.assertEqual(first["threadsScanned"], 1)
        self.assertEqual(first["threadsSummarized"], 1)
        self.assertEqual(second["threadsUnchanged"], 1)
        self.assertEqual(len(processor.calls), 1)
        _channel, post, replies, _digest = processor.calls[0]
        self.assertEqual(post["subject"], "Question")
        self.assertEqual(post["content"], "<p>How?</p>")
        self.assertEqual([reply["id"] for reply in replies], ["reply"])
        document = summaries.upserts[0]
        self.assertEqual(document["id"], "root")
        self.assertEqual(document["channel_id"], CHANNEL["channelId"])
        self.assertEqual(document["document_type"], "teams_thread_qa_summary")
        self.assertEqual(document["message_count"], 2)
        self.assertEqual(document["last_write_ts"], 11)
        self.assertEqual(document["qa"]["answer"], "Do this.")

        messages.documents.append(stored_message(
            partition, "late", "<p>and this.</p>", ts=20,
            created_at="2026-09-01T02:00:00Z"))
        third = await service.summarize([channel])
        self.assertEqual(third["threadsSummarized"], 1)
        self.assertEqual(len(processor.calls), 2)
        self.assertEqual(summaries.upserts[-1]["message_count"], 3)

    async def test_summary_timestamps_survive_a_string_range_filter(self):
        """Cosmos compares timestamps as strings, so the spelling has to be stable.

        ``conversation-messages`` holds whatever the bot serialized, and a message
        whose microseconds land on zero is stored without a fractional part. ``Z``
        sorts after ``.``, so mixing both spellings in one container drops the short
        form out of a ``>=`` filter. Summaries normalize instead of inheriting.
        """
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        whole_second = thread_partition(CHANNEL["channelId"], "whole")
        fractional = thread_partition(CHANNEL["channelId"], "fraction")
        messages = FakeMessageContainer([
            stored_message(whole_second, "whole", "<p>How?</p>", ts=10,
                           created_at="2026-09-15T00:00:00Z"),
            stored_message(fractional, "fraction", "<p>How?</p>", ts=11,
                           created_at="2026-09-15T00:00:00.500000Z"),
        ])
        service = TeamsQASummaryService(messages, FakeSummaryContainer(), FakeProcessor())

        result = await service.summarize([channel])
        documents = service._summaries.upserts

        self.assertEqual(result["threadsSummarized"], 2)
        started = sorted(document["thread_started_at"] for document in documents)
        self.assertEqual(started, ["2026-09-15T00:00:00.000000Z",
                                   "2026-09-15T00:00:00.500000Z"])
        for document in documents:
            self.assertEqual(document["last_message_at"], document["thread_started_at"])
            # The processor hands back an offset-style stamp; the container restates it.
            self.assertTrue(document["processed_at"].endswith("Z"), document["processed_at"])
            self.assertEqual(len(document["processed_at"]), len(started[0]))
        # The boundary a caller would naturally write now selects both threads.
        window = [document for document in documents
                  if "2026-09-15T00:00:00.000000Z" <= document["thread_started_at"]
                  <= "2026-09-15T00:00:01.000000Z"]
        self.assertEqual(len(window), 2)

    def test_thread_messages_are_ordered_by_instant_not_spelling(self):
        from services.teams_qa_summary_service import _ordered

        ordered = _ordered([
            {"id": "second", "created_at": "2026-09-15T00:00:00.500000Z"},
            {"id": "first", "created_at": "2026-09-15T00:00:00Z"},
            {"id": "third", "created_at": "2026-09-15T00:00:01Z"},
            {"id": "unparseable", "created_at": "not a timestamp"},
        ])

        self.assertEqual([message["id"] for message in ordered],
                         ["unparseable", "first", "second", "third"])

    async def test_summary_refreshes_the_gate_without_rerunning_the_model(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([
            stored_message(partition, "root", "<p>How?</p>", ts=10),
        ])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor()
        service = TeamsQASummaryService(messages, summaries, processor)
        await service.summarize([channel])

        # A bot backfilling should_reply moves _ts without changing any content.
        messages.documents[0]["_ts"] = 99
        result = await service.summarize([channel])

        self.assertEqual(result["threadsRefreshed"], 1)
        self.assertEqual(result["threadsSummarized"], 0)
        self.assertEqual(len(processor.calls), 1)
        self.assertEqual(summaries.upserts[-1]["last_write_ts"], 99)
        self.assertEqual(await service.summarize([channel]), {
            "channelsCompleted": 1, "threadsScanned": 1, "threadsSummarized": 0,
            "threadsUnchanged": 1, "threadsRefreshed": 0, "threadsExcluded": 0,
            "threadsSkipped": 0})

    async def test_summary_stores_nothing_for_a_thread_without_reusable_qa(self):
        """A rejected thread leaves no document, so every stored one is an answer."""
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([
            stored_message(partition, "root", "<p>How?</p>", ts=10),
            stored_message(partition, "bot", "<p>Bot guess.</p>", ts=11, role="system",
                           created_at="2026-09-01T01:00:00Z"),
        ])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor({
            "status": "excluded", "exclusion_reason": "No human reply.", "qa": None,
        })
        service = TeamsQASummaryService(messages, summaries, processor)

        result = await service.summarize([channel])

        self.assertEqual(result["threadsExcluded"], 1)
        self.assertEqual(result["threadsSummarized"], 0)
        self.assertFalse(summaries.upserts)
        self.assertFalse(summaries.documents)
        self.assertFalse(summaries.deletes)
        # Nothing was stored, so the gate cannot spare the model on the next run.
        self.assertEqual((await service.summarize([channel]))["threadsExcluded"], 1)
        self.assertEqual(len(processor.calls), 2)

    async def test_summary_removes_an_answer_the_thread_no_longer_supports(self):
        """An accepted thread that later fails the rules must not keep its answer."""
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([
            stored_message(partition, "root", "<p>How?</p>", ts=10),
        ])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor()
        service = TeamsQASummaryService(messages, summaries, processor)
        await service.summarize([channel])
        self.assertEqual(len(summaries.documents), 1)

        # The thread grows a correction that takes it out of scope.
        messages.documents.append(stored_message(
            partition, "retraction", "<p>Wrong channel.</p>", ts=20,
            created_at="2026-09-01T02:00:00Z"))
        processor.decision = {"status": "excluded", "exclusion_reason": "Out of scope.",
                              "qa": None}
        result = await service.summarize([channel])

        self.assertEqual(result["threadsExcluded"], 1)
        self.assertEqual(summaries.deletes, [("root", CHANNEL["channelId"])])
        self.assertFalse(summaries.documents)

    async def test_summary_omits_the_fields_that_only_routed_the_decision(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([stored_message(partition, "root", "<p>How?</p>", ts=10)])
        summaries = FakeSummaryContainer()

        await TeamsQASummaryService(messages, summaries, FakeProcessor()).summarize([channel])

        document = summaries.upserts[0]
        self.assertNotIn("status", document)
        self.assertNotIn("exclusion_reason", document)
        self.assertEqual(document["qa"]["answer"], "Do this.")

    async def test_summary_reprocesses_when_the_processor_version_changes(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([stored_message(partition, "root", "<p>How?</p>", ts=10)])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor()
        await TeamsQASummaryService(messages, summaries, processor).summarize([channel])

        processor.version = "v2"
        result = await TeamsQASummaryService(messages, summaries, processor).summarize([channel])

        self.assertEqual(result["threadsSummarized"], 1)
        self.assertEqual(len(processor.calls), 2)
        self.assertEqual(summaries.upserts[-1]["processor_version"], "v2")

    async def test_summary_skips_a_thread_whose_root_post_is_missing(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        channel = {**CHANNEL, "processingScope": {"name": "general", "description": "Scope."}}
        partition = thread_partition(CHANNEL["channelId"], "root")
        messages = FakeMessageContainer([
            stored_message(partition, "bot-answer", "<p>bot only</p>", ts=10, role="system"),
        ])
        summaries = FakeSummaryContainer()
        processor = FakeProcessor()

        result = await TeamsQASummaryService(messages, summaries, processor).summarize([channel])

        self.assertEqual(result["threadsSkipped"], 1)
        self.assertEqual(result["threadsSummarized"], 0)
        self.assertFalse(processor.calls)
        self.assertFalse(summaries.upserts)

    async def test_summary_service_requires_a_processor(self):
        from services.teams_qa_summary_service import TeamsQASummaryService

        with self.assertRaisesRegex(RuntimeError, "processor"):
            TeamsQASummaryService(FakeMessageContainer([]), FakeSummaryContainer(), None)

    async def test_thread_processor_validates_and_versions_model_output(self):
        from types import SimpleNamespace
        from services.teams_thread_processor import TeamsThreadProcessor

        agent = AsyncMock()
        agent.run.return_value = SimpleNamespace(text=json.dumps({
            "status": "included",
            "exclusion_reason": None,
            "qa": {"title": "Title", "question": "Question", "answer": "Answer"},
            "resources": [{
                "url": "https://example.com/resource",
                "access_status": "accessed",
                "summary": "Used the documented behavior.",
            }],
        }))
        channel = {
            **CHANNEL,
            "processingScope": {"name": "general", "description": "Azure developer experience."},
        }
        result = await TeamsThreadProcessor(agent, "v3").process(
            channel, {"id": "root"}, [{"id": "reply"}], "content-hash"
        )

        self.assertEqual(result["processor_version"], "v3")
        self.assertEqual(result["source_content_hash"], "content-hash")
        self.assertEqual(result["qa"]["answer"], "Answer")
        payload = json.loads(agent.run.call_args.args[0])
        self.assertEqual(payload["channel"], {
            "name": "general", "scope": "Azure developer experience.",
        })
        with self.assertRaisesRegex(ValueError, "subject"):
            await TeamsThreadProcessor(agent, "v3").process(
                channel, {"id": "root", "subject": "Different"}, [], "content-hash"
            )


if __name__ == "__main__":
    unittest.main()
