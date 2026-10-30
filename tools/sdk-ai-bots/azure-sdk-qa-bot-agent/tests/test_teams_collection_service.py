import asyncio
import copy
import json
import sys
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, Mock, patch
from urllib.parse import quote

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.conversation_service import (
    TeamsCollectionService,
    continuation_token,
    thread_partition,
)


TENANT_KEY = "typespec_channel_qa_bot"
CHANNEL = {"teamId": "7ccc31f0-b371-450b-a73c-48f5a31a9b96",
           "channelId": "19:test@thread.tacv2", "tenantKey": TENANT_KEY}
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
    return {"channels": [CHANNEL], "maxPages": 100, **overrides}


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
        return {document_id: {"id": document_id, "content": document["content"],
                              "extra_info": copy.deepcopy(document.get("extra_info"))}
                for (stored, document_id), document in self.items.items() if stored == partition}

    async def create(self, document):
        key = (document["conversation_partition"], document["id"])
        if document["id"] in self.conflicts or key in self.items:
            return False
        self.items[key] = copy.deepcopy(document)
        self.creates += 1
        return True

    async def update_message(self, document_id, partition, fields):
        key = (partition, document_id)
        if document_id in self.races or key not in self.items:
            return False
        document = self.items[key]
        document["content"] = fields["content"]
        document["extra_info"] = {**(document.get("extra_info") or {}),
                                   "attachments": copy.deepcopy(fields["attachments"]),
                                   "images": copy.deepcopy(fields["images"])}
        self.updates += 1
        return True


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
        from scripts.deploy_hosted_agent import main

        with patch.object(sys, "argv", ["deploy_hosted_agent.py", "teams-collection", "run"]), \
                patch("sys.stderr", new_callable=StringIO) as stderr:
            with self.assertRaises(SystemExit) as failure:
                main()
        self.assertEqual(failure.exception.code, 2)
        self.assertIn("invalid choice: 'run'", stderr.getvalue())

    def test_backfill_template_contains_only_the_oauth_logic_app(self):
        project = Path(__file__).resolve().parents[1]
        template = json.loads((project / "pipelines/teams-collection/template.json").read_text())
        self.assertEqual([resource["type"] for resource in template["resources"]],
                         ["Microsoft.Logic/workflows"])
        self.assertNotIn("collectorPrincipalId", template["parameters"])
        self.assertNotIn("cosmosAccountName", template["parameters"])

        workflow = template["resources"][0]
        triggers = workflow["properties"]["accessControl"]["triggers"]
        self.assertEqual(triggers["sasAuthenticationPolicy"]["state"], "Disabled")
        policies = triggers["openAuthenticationPolicies"]["policies"]
        backfill_claims = template["variables"]["backfillPolicy"]["backfill"]["claims"]
        self.assertIn("[parameters('backfillPrincipalId')]",
                  [claim["value"] for claim in backfill_claims])
        definition = workflow["properties"]["definition"]
        authorize = definition["actions"]["Authorize_channel"]
        self.assertIn("contains(parameters('allowedChannels')", authorize["expression"])
        cases = authorize["actions"]["Select_operation"]["cases"]
        self.assertEqual(set(cases), {"messages", "replies"})

    def test_backfill_parameter_files_match_configured_channels(self):
        project = Path(__file__).resolve().parents[1]
        config = json.loads((project / "config/teams_collection_config.json").read_text())
        allowed = [f"{channel['teamId']}|{channel['channelId']}"
                   for channel in config["channels"]]
        dev_dump_channels = [
            "3e17dcb0-4257-4a30-b843-77f47f1d4121|"
            "19:acbd512e57bd475198ea6bf4564599e3@thread.skype",
            "3e17dcb0-4257-4a30-b843-77f47f1d4121|"
            "19:7b87fb348f224b37b6206fa9d89a105b@thread.skype",
        ]
        expected = {
            "dev": ("azuresdkqabot-dev-teams-collection", "azure-sdk-qa-bot-dev"),
            "test": ("azuresdkqabot-test-teams-collection", "azure-sdk-qa-bot"),
            "prod": ("azuresdkqabot-teams-collection", "azure-sdk-qa-bot"),
        }
        for environment, (workflow_name, resource_group) in expected.items():
            with self.subTest(environment=environment):
                path = project / f"pipelines/teams-collection/parameters.azure_sdk.{environment}.json"
                parameters = json.loads(path.read_text())["parameters"]
                self.assertEqual(parameters["logicAppName"]["value"], workflow_name)
                self.assertIn(f"/resourceGroups/{resource_group}/",
                              parameters["teamsConnectionResourceId"]["value"])
                self.assertTrue(parameters["tenantId"]["value"])
                self.assertEqual(parameters["allowedChannels"]["value"],
                                 allowed + (dev_dump_channels if environment == "dev" else []))
                self.assertNotIn("collectorPrincipalId", parameters)
                self.assertNotIn("cosmosAccountName", parameters)

    def test_backfill_deployment_validates_identity_and_sanitizes_callback_url(self):
        from scripts.deploy_hosted_agent import _principal_id, _sas_free_callback_url

        self.assertEqual(
            _principal_id("F8EC2208-D3C9-4271-B787-07C26651E1C6", "--backfill-principal-id"),
            "f8ec2208-d3c9-4271-b787-07c26651e1c6",
        )
        for value in (None, "", "not-a-guid"):
            with self.subTest(value=value), self.assertRaisesRegex(
                ValueError, "--backfill-principal-id"
            ):
                _principal_id(value, "--backfill-principal-id")
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

    def test_backfill_pipeline_does_not_deploy_a_hosted_agent(self):
        project = Path(__file__).resolve().parents[1]
        agent_pipeline = (project / "pipelines/agent-cd.yml").read_text()
        logicapp_pipeline = (project / "pipelines/logicapp-cd.yml").read_text()
        deploy_script = (project / "scripts/deploy_hosted_agent.py").read_text()
        self.assertNotIn("teams_collection_agent", agent_pipeline)
        self.assertIn("- teams-collection", logicapp_pipeline)
        self.assertIn("python scripts/deploy_hosted_agent.py teams-collection deploy",
                      logicapp_pipeline)
        self.assertIn("TEAMS_COLLECTION_LOGIC_APP_URL", deploy_script)
        self.assertNotIn("collectorPrincipalId", logicapp_pipeline)

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
        from services.conversation_service import backfill_configured_channels

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
        with patch("services.conversation_service.get_conversation_message_container", new=AsyncMock()), \
                patch("services.conversation_service.CosmosMessageStore", return_value=store), \
                patch("services.conversation_service.get_credential", return_value=credential), \
                patch("services.conversation_service.httpx.AsyncClient", return_value=client):
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
        from services.conversation_service import select_channels

        second = {**CHANNEL, "channelId": "19:second@thread.tacv2"}
        channels = [CHANNEL, second]
        self.assertEqual(select_channels(channels, None), channels)
        self.assertEqual(select_channels(channels, second["channelId"]), [second])
        with self.assertRaisesRegex(ValueError, "allowlist"):
            select_channels(channels, "19:unknown@thread.tacv2")

    async def _settled_backfill(self, service, job_id):
        from models.conversation import TeamsBackfillStatus

        for _ in range(500):
            job = service.get(job_id)
            if job is None or job.status is not TeamsBackfillStatus.running:
                return job
            await asyncio.sleep(0.01)
        raise AssertionError("Backfill job never left the running state.")

    async def test_server_accepts_one_backfill_at_a_time_and_reports_its_outcome(self):
        import httpx
        import server
        from services.conversation_service import TeamsBackfillService

        release = asyncio.Event()
        entered = asyncio.Event()
        calls = []

        async def runner(config, settings, channel_id, start_time):
            calls.append((config, settings, channel_id, start_time))
            entered.set()
            await release.wait()
            return {"messagesCreated": 3}

        config_path = Path(__file__).resolve().parents[1] / "config/teams_collection_config.json"
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

                self.assertEqual((await client.get("/teams/backfill/missing-job")).status_code, 404)
                for payload in ({"channel_id": "19:unknown@thread.tacv2"},
                                {"start_time": "2026-01-01"}):
                    with self.subTest(payload=payload):
                        rejected = await client.post("/teams/backfill", json=payload)
                        self.assertEqual(rejected.status_code, 422, rejected.text)

        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][2:], (channel, "2026-01-01T00:00:00Z"))
        self.assertEqual(calls[0][0]["channels"][0]["channelId"], channel)

    async def test_backfill_job_reports_failure_without_echoing_raw_error_content(self):
        from models.conversation import TeamsBackfillRequest
        from services.conversation_service import TeamsBackfillService

        config_path = Path(__file__).resolve().parents[1] / "config/teams_collection_config.json"
        service = TeamsBackfillService(
            config_path, AsyncMock(side_effect=RuntimeError("DO_NOT_LOG"))
        )
        with self.assertLogs("services.conversation_service", level="ERROR"):
            accepted = await service.start(TeamsBackfillRequest())
            job = await self._settled_backfill(service, accepted.job_id)
            self.assertEqual(job.status.value, "failed")
            self.assertNotIn("DO_NOT_LOG", job.error)
            self.assertIsNotNone(job.completed_at)
            retried = await service.start(TeamsBackfillRequest())
            self.assertEqual(retried.status.value, "running")
            self.assertEqual(
                (await self._settled_backfill(service, retried.job_id)).status.value, "failed"
            )

    def test_normalization_matches_the_realtime_workflow(self):
        from services.conversation_service import normalized_content

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
        self.assertEqual(
            normalized_content(graph_message({
                "id": "reply", "body": {"content": "<p>plain</p>"}})),
            "<p>plain</p>",
        )
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
                           "message_link": "https://teams.microsoft.com/l/message/thread/id",
                           "attachments": [], "images": []},
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
        from services.conversation_service import CosmosMessageStore

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
            "extra_info": {"channel_id": "channel", "message_link": "link"},
        }
        attachment = {"id": "file", "contentType": "reference",
                      "contentUrl": "https://contoso.sharepoint.com/file.docx",
                      "content": None, "name": "file.docx", "thumbnailUrl": None,
                      "teamsAppId": None}
        fields = {"content": "new", "attachments": [attachment], "images": []}
        self.assertTrue(await store.update_message("message", "partition", fields))
        replaced = container.replace_item.call_args.kwargs
        self.assertEqual(replaced["body"]["content"], "new")
        self.assertEqual(replaced["body"]["extra_info"], {
            "channel_id": "channel", "message_link": "link",
            "attachments": [attachment], "images": []})
        self.assertTrue(replaced["body"]["should_reply"])
        self.assertEqual(replaced["etag"], "version1")
        self.assertEqual(replaced["match_condition"], MatchConditions.IfNotModified)
        container.replace_item.reset_mock()
        container.read_item.return_value = {**container.read_item.return_value, "content": "new",
                                            "extra_info": {"attachments": [attachment]}}
        self.assertFalse(await store.update_message("message", "partition", fields))
        container.replace_item.assert_not_awaited()
        container.replace_item.side_effect = CosmosAccessConditionFailedError(
            status_code=412, message="conflict")
        self.assertFalse(await store.update_message(
            "message", "partition", {**fields, "content": "newer"}))
        container.read_item.side_effect = CosmosResourceNotFoundError(status_code=404, message="gone")
        self.assertFalse(await store.update_message(
            "message", "partition", {**fields, "content": "newer"}))

        async def items():
            yield {"id": "message", "content": "stored"}

        container.query_items = Mock(return_value=items())
        self.assertEqual(await store.read_thread("partition"),
                         {"message": {"id": "message", "content": "stored"}})
        self.assertEqual(container.query_items.call_args.kwargs["partition_key"], "partition")
        self.assertIn("c.content", container.query_items.call_args.kwargs["query"])
        self.assertIn("c.extra_info", container.query_items.call_args.kwargs["query"])

    def test_images_are_lifted_from_the_body_and_emoji_are_skipped(self):
        from services.conversation_service import message_images

        hosted = ("https://graph.microsoft.com/v1.0/teams/team/channels/channel/messages/"
                  "root/hostedContents/aWQ9eF8w/$value")
        content = (
            f'<p>Error:</p><img src="{hosted}" alt="image" width="250">'
            '<img itemtype="http://schema.skype.com/Emoji" src="https://statics.teams.cdn.office.net/smile.png">'
            f'<p>again</p><img src="{hosted}"/>'
            '<img src="https://contoso.com/diagram.png" itemid="diagram">'
            '<img alt="no source">'
        )
        self.assertEqual(
            [image.model_dump(mode="json") for image in message_images(content)],
            [{"id": "aWQ9eF8w", "contentUrl": hosted},
             {"id": "diagram", "contentUrl": "https://contoso.com/diagram.png"}],
        )
        self.assertEqual(message_images("<p>plain</p>"), [])
        self.assertEqual(message_images(None), [])

    async def test_backfill_keeps_connector_attachments_and_images_and_fills_old_messages(self):
        hosted = ("https://graph.microsoft.com/v1.0/teams/team/channels/channel/messages/"
                  "root/hostedContents/aWQ9eF8w/$value")
        attachment = {"id": "file", "contentType": "reference",
                      "contentUrl": "https://contoso.sharepoint.com/file.docx",
                      "content": None, "name": "file.docx", "thumbnailUrl": None}
        root = graph_message({
            "id": "root",
            "body": {"content": f'<p>See</p><img src="{hosted}"><attachment id="file"></attachment>'},
            "attachments": [{**attachment, "unexpected": "dropped"}],
            "replies": [],
        })
        connector_attachment = {**attachment, "teamsAppId": None}
        partition = thread_partition(CHANNEL["channelId"], "root")

        store = MemoryStore()
        created = await TeamsCollectionService(
            AsyncMock(return_value={"value": [root]}), store).backfill([CHANNEL])
        self.assertEqual(created["messagesCreated"], 1)
        extra_info = store.items[(partition, "root")]["extra_info"]
        self.assertEqual(extra_info["attachments"], [connector_attachment])
        self.assertEqual(extra_info["images"], [{"id": "aWQ9eF8w", "contentUrl": hosted}])

        again = await TeamsCollectionService(
            AsyncMock(return_value={"value": [root]}), store).backfill([CHANNEL])
        self.assertEqual(again["messagesUnchanged"], 1)
        self.assertEqual(store.updates, 0)

        # A live message saved before attachments were kept gains them in place.
        live = MemoryStore()
        live.seed({**stored_message(partition, "root", store.items[(partition, "root")]["content"],
                                    link="live-link"), "should_reply": True})
        filled = await TeamsCollectionService(
            AsyncMock(return_value={"value": [root]}), live).backfill([CHANNEL])
        self.assertEqual(filled["messagesUpdated"], 1)
        document = live.items[(partition, "root")]
        self.assertTrue(document["should_reply"])
        self.assertEqual(document["extra_info"], {
            "channel_id": CHANNEL["channelId"], "message_link": "live-link",
            "attachments": [connector_attachment],
            "images": [{"id": "aWQ9eF8w", "contentUrl": hosted}]})

    async def test_saved_teams_message_keeps_attachments_and_derives_images(self):
        from models.conversation import ConversationMessage
        from services.conversation_service import ConversationService

        hosted = ("https://graph.microsoft.com/v1.0/teams/team/channels/channel/messages/"
                  "root/hostedContents/aWQ9eF8w/$value")
        container = AsyncMock()
        container.upsert_item.side_effect = lambda body: body
        message = ConversationMessage.model_validate({
            "id": "root", "sender_role": "user", "sender_id": "user-id",
            "sender_name": "Test User", "content": f'<p>See</p><img src="{hosted}">',
            "created_at": "2026-09-01T00:00:00Z",
            "conversation_id": f"{CHANNEL['channelId']};messageid=root",
            "conversation_type": "teams_channel",
            "extra_info": {"channel_id": CHANNEL["channelId"], "message_link": "link",
                           "attachments": [{"id": "file", "contentType": "reference",
                                            "name": "file.docx"}]},
        })
        with patch("services.conversation_service.get_conversation_message_container",
                   new=AsyncMock(return_value=container)):
            await ConversationService().save_conversation(message)

        saved = container.upsert_item.call_args.args[0]
        self.assertEqual(saved["extra_info"]["attachments"], [{
            "id": "file", "contentType": "reference", "contentUrl": None, "content": None,
            "name": "file.docx", "thumbnailUrl": None, "teamsAppId": None}])
        self.assertEqual(saved["extra_info"]["images"],
                         [{"id": "aWQ9eF8w", "contentUrl": hosted}])

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
        from services.conversation_service import LogicAppPageClient

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
        from services.conversation_service import LogicAppPageClient

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
                with patch("services.conversation_service.asyncio.sleep", new=AsyncMock()) as sleep:
                    self.assertEqual(await pages.fetch_page(CHANNEL, None, None), {"value": []})
                self.assertEqual(client.post.await_count, 2)
                sleep.assert_awaited_once_with(1)

if __name__ == "__main__":
    unittest.main()
