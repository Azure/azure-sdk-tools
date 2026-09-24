import asyncio
import copy
import json
import sys
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch
from urllib.parse import quote

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.teams_collection_service import TeamsCollectionService, continuation_token


TENANT = "72f988bf-86f1-41af-91ab-2d7cd011db47"
CHANNEL = {"teamId": "7ccc31f0-b371-450b-a73c-48f5a31a9b96", "channelId": "19:test@thread.tacv2"}
CONNECTION = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/test/providers/Microsoft.Web/connections/teams"


def next_link(channel, message_id=None, token="next"):
    path = f"/teams/{channel['teamId']}/channels/{quote(channel['channelId'], safe='')}/messages"
    if message_id:
        path = "/v1.0" + path + f"/{message_id}/replies"
    else:
        path = "/beta" + path
    return f"https://logic-apis-westus2.azure-apim.net/apim/teams/connection{path}?$skiptoken={quote(token, safe='')}"


def graph_message(values):
    return {"messageType": "message", **values}


class MemoryStore:
    def __init__(self):
        self.items = {}
        self.checkpoints = {}
        self.writes = 0

    async def read(self, document_id, partition):
        if document_id == "channel-checkpoint":
            return copy.deepcopy(self.checkpoints.get(partition))
        return copy.deepcopy(self.items.get((document_id, partition)))

    async def read_channel_index(self, partition):
        return {document_id: copy.deepcopy(document)
                for (document_id, channel), document in self.items.items() if channel == partition}

    async def read_channel_documents(self, partition):
        return [copy.deepcopy(document)
                for (_document_id, channel), document in self.items.items() if channel == partition]

    async def write(self, document, previous):
        if document.get("type") == "channel-checkpoint":
            self.checkpoints[document["channel_key"]] = copy.deepcopy(document)
            return
        self.items[(document["id"], document["channel_key"])] = copy.deepcopy(document)
        self.writes += 1


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

    async def test_collection_reaches_service_and_store_through_logic_app(self):
        import httpx
        from types import SimpleNamespace
        from unittest.mock import Mock
        from utils.teams_collection import collect_configured_channels

        store = MemoryStore()
        store.validate = AsyncMock()
        store.record_run = AsyncMock()
        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={"operation": "messages", "data": {
            "value": [graph_message({
                "id": "root",
                "replies": [graph_message({"id": "reply", "replyToId": "root"})],
            })],
        }})
        client.__aenter__.return_value = client
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        url = "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"
        settings = Mock(return_value=url)
        with patch("utils.azure_cosmosdb.get_teams_channel_posts_container", new=AsyncMock()), \
                patch("utils.teams_collection.CosmosThreadStore", return_value=store), \
                patch("utils.azure_credential.get_credential", return_value=credential), \
            patch("utils.teams_collection.httpx.AsyncClient", return_value=client):
            result = await collect_configured_channels(
                {"channels": [CHANNEL], "tenantId": TENANT, "maxPages": 100}, settings,
            )
        self.assertEqual(result["postsWritten"], 1)
        self.assertEqual(result["repliesRead"], 1)
        self.assertEqual(next(iter(store.items.values()))["replies"][0]["id"], "reply")
        self.assertEqual(len(store.checkpoints), 1)
        self.assertEqual(store.record_run.await_count, 2)
        self.assertEqual(store.record_run.call_args.args[0]["status"], "succeeded")
        settings.assert_called_once_with("TEAMS_COLLECTION_LOGIC_APP_URL", "")
        self.assertEqual(client.post.call_args.args[0], url)
        self.assertEqual(client.post.call_args.kwargs["json"], {**CHANNEL, "operation": "messages"})
        credential.get_token.assert_awaited_once_with("https://management.core.windows.net/.default")

    async def test_cli_dispatches_remote_routine_and_closes_credential(self):
        from types import SimpleNamespace
        from scripts.deploy_teams_collection import execute_routine

        config = {"channels": [CHANNEL], "tenantId": TENANT, "maxPages": 100}
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

    async def test_failed_collection_records_failure_without_raw_error_content(self):
        from utils.teams_collection import collect_configured_channels

        config = {"channels": [CHANNEL], "tenantId": TENANT, "maxPages": 100}
        store = AsyncMock()
        recorded = []

        async def capture_run(document):
            recorded.append(copy.deepcopy(document))

        store.record_run.side_effect = capture_run
        with patch("utils.azure_cosmosdb.get_teams_channel_posts_container", new=AsyncMock()), \
                patch("utils.teams_collection.CosmosThreadStore", return_value=store), \
                patch("utils.azure_credential.get_credential"), \
                patch("utils.teams_collection.TeamsCollectionService") as service:
            service.return_value.collect = AsyncMock(side_effect=RuntimeError("DO_NOT_LOG"))
            with self.assertRaises(RuntimeError):
                await collect_configured_channels(config, lambda key, default: (
                    "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"))
        self.assertEqual([run["status"] for run in recorded], ["running", "failed"])
        self.assertEqual(recorded[-1]["error_type"], "RuntimeError")
        self.assertIn("ended_at", recorded[-1])
        self.assertNotIn("DO_NOT_LOG", json.dumps(recorded))

    async def test_hosted_http_returns_before_collection_and_exposes_final_response(self):
        import httpx
        from agents.teams_collection_agent.init import TeamsCollectionAgent, create_server

        release = asyncio.Event()
        completed = asyncio.Event()

        async def collect():
            await release.wait()
            completed.set()
            return {"postsWritten": 2}

        server = create_server(TeamsCollectionAgent(collect))
        async with server.router.lifespan_context(server):
            async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server), base_url="http://test") as client:
                try:
                    response = await asyncio.wait_for(client.post("/responses", json={"input": "collect"}), 5)
                    self.assertEqual(response.status_code, 200, response.text)
                    self.assertIn(response.json()["status"], ("queued", "in_progress"))
                    self.assertTrue(response.json()["id"].startswith("caresp_"))
                    self.assertFalse(completed.is_set())
                finally:
                    release.set()
                await asyncio.wait_for(completed.wait(), 5)
        self.assertTrue(completed.is_set())
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server), base_url="http://test") as client:
            final = await client.get("/responses/" + response.json()["id"])
            self.assertEqual(final.status_code, 200)
            self.assertEqual(final.json()["status"], "completed")
            self.assertEqual(json.loads(final.json()["output"][0]["content"][0]["text"]), {"postsWritten": 2})

    async def test_hosted_agent_dispatches_collect_and_reprocess_operations(self):
        from agents.teams_collection_agent.init import REPROCESS_REQUEST, TeamsCollectionAgent
        from agent_framework_foundry_hosting import ResponsesHostServer

        collect = AsyncMock(return_value={"postsWritten": 2, "repliesRead": 3})
        reprocess = AsyncMock(return_value={"postsReprocessed": 4})
        agent = TeamsCollectionAgent(collect, reprocess)
        response = await agent.run("Read another channel instead")
        self.assertEqual(json.loads(response.text), {"postsWritten": 2, "repliesRead": 3})
        updates = [update async for update in agent.run("Collect", stream=True)]
        self.assertEqual(json.loads(updates[0].text)["postsWritten"], 2)
        self.assertEqual(collect.await_count, 2)
        response = await agent.run(REPROCESS_REQUEST)
        self.assertEqual(json.loads(response.text), {"postsReprocessed": 4})
        reprocess.assert_awaited_once()
        self.assertIsNotNone(ResponsesHostServer(agent))

    async def test_routine_creation_is_paused_and_dispatch_uses_public_endpoint(self):
        import httpx
        from types import SimpleNamespace
        from scripts.deploy_teams_collection import routine_request, routine_definition

        config = json.loads(
            (Path(__file__).resolve().parents[1] / "config/teams_collection_config.json").read_text()
        )
        definition = routine_definition(config)
        self.assertFalse(definition["enabled"])
        self.assertEqual(definition["authorization"], {"identity": "agent"})
        self.assertEqual(definition["triggers"]["schedule"]["cron_expression"], "0 0 * * 0")
        client = AsyncMock()
        client.get.return_value = httpx.Response(404)
        client.put.return_value = httpx.Response(201, json={"name": "teams-channel-collection", "enabled": False})
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        endpoint = "https://account.services.ai.azure.com/api/projects/project"
        await routine_request(config, "routine-create", endpoint, credential, client)
        self.assertEqual(client.put.call_args.args[0], endpoint + "/routines/teams-channel-collection")
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

    def test_template_enforces_identity_channel_allowlist_and_read_only_actions(self):
        project = Path(__file__).resolve().parents[1]
        template = json.loads((project / "pipelines/teams-collection/template.json").read_text())
        container = next(resource for resource in template["resources"]
                         if resource["type"].endswith("/containers"))
        workflow = next(resource for resource in template["resources"]
                        if resource["type"] == "Microsoft.Logic/workflows")
        self.assertEqual(container["properties"]["resource"]["partitionKey"]["paths"], ["/channel_key"])
        access = workflow["properties"]["accessControl"]["triggers"]
        self.assertEqual(access["sasAuthenticationPolicy"]["state"], "Disabled")
        self.assertEqual({claim["name"] for claim in access["openAuthenticationPolicies"]["policies"]["collector"]["claims"]},
                         {"iss", "aud", "oid"})
        claims = {
            claim["name"]: claim["value"]
            for claim in access["openAuthenticationPolicies"]["policies"]["collector"]["claims"]
        }
        self.assertEqual(claims["aud"], "https://management.core.windows.net")
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

    def test_template_grants_collector_metadata_and_only_archive_data_access(self):
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
        self.assertEqual(len(assignments), 2)
        metadata, archive = assignments
        self.assertEqual(metadata["properties"]["scope"], "[variables('cosmosAccountResourceId')]")
        self.assertIn("variables('metadataRoleName')", metadata["properties"]["roleDefinitionId"])
        self.assertEqual(archive["properties"]["scope"],
                         "[concat(variables('cosmosAccountResourceId'), '/dbs/azure-sdk-qa-bot/colls/teams-channel-posts')]")
        self.assertIn("00000000-0000-0000-0000-000000000002", archive["properties"]["roleDefinitionId"])
        for assignment in assignments:
            self.assertEqual(assignment["properties"]["principalId"], "[parameters('collectorPrincipalId')]")
            self.assertIn("guid(", assignment["name"])
            self.assertTrue(assignment["dependsOn"])

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

    def test_collection_cd_deploys_agent_and_dedicated_infrastructure(self):
        project = Path(__file__).resolve().parents[1]
        pipeline = (project / "pipelines/teams-collection-cd.yml").read_text()
        generic_pipeline = (project / "pipelines/agent-cd.yml").read_text()

        self.assertIn("python scripts/deploy_hosted_agent.py", pipeline)
        self.assertIn("teams_collection_agent", pipeline)
        self.assertIn("python scripts/deploy_teams_collection.py", pipeline)
        self.assertIn("--appconfig-endpoint \"$(AZURE_APPCONFIG_ENDPOINT)\"", pipeline)
        self.assertNotIn("pipelines/logicapp/template.json", pipeline)
        self.assertNotIn("- teams_collection_agent", generic_pipeline)

    async def test_cosmos_reads_only_hash_index_in_one_channel_partition(self):
        from unittest.mock import Mock
        from utils.teams_collection import CosmosThreadStore

        async def items():
            yield {"id": "thread", "content_hash": "hash", "_etag": "version1"}

        container = AsyncMock()
        container.query_items = Mock(return_value=items())
        index = await CosmosThreadStore(container).read_channel_index("channel")
        self.assertEqual(index, {"thread": {"id": "thread", "content_hash": "hash", "_etag": "version1"}})
        container.query_items.assert_called_once_with(
            query=("SELECT c.id, c.content_hash, c.processing, c._etag "
                   "FROM c WHERE IS_DEFINED(c.post_id)"),
            partition_key="channel",
        )
        container.read_item.assert_not_awaited()

    async def test_expanded_replies_only_write_new_or_changed_threads(self):
        root = graph_message({
            "id": "root", "createdDateTime": "2026-09-01T00:00:00Z",
            "lastModifiedDateTime": "2026-09-01T00:00:00Z",
        })
        quiet = graph_message({
            "id": "quiet", "createdDateTime": "2026-09-01T00:00:00Z", "replies": [],
        })
        reply = graph_message({
            "id": "reply", "replyToId": "root", "createdDateTime": "2026-09-01T01:00:00Z",
            "body": {"content": "original"},
        })
        edited = {**reply, "lastModifiedDateTime": "2026-09-10T00:00:00Z", "body": {"content": "edited"}}
        new_post = graph_message({
            "id": "new", "createdDateTime": "2026-09-10T00:00:00Z", "replies": [],
        })
        fetch = AsyncMock(side_effect=[
            {"value": [{**root, "replies": [reply]}, quiet]},
            {"value": [{**root, "replies": [edited]}, quiet, new_post]},
            {"value": [{**root, "replies": [edited]}, quiet, new_post]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        first = await service.collect([CHANNEL])
        quiet_before = next(copy.deepcopy(document) for document in store.items.values() if document["post_id"] == "quiet")
        changed = await service.collect([CHANNEL])
        unchanged = await service.collect([CHANNEL])
        self.assertEqual(first["postsWritten"], 2)
        self.assertEqual(changed["postsWritten"], 2)
        self.assertEqual(changed["postsUnchanged"], 1)
        self.assertEqual(unchanged["postsWritten"], 0)
        self.assertEqual(unchanged["postsUnchanged"], 3)
        self.assertEqual(store.writes, 4)
        self.assertEqual(fetch.await_count, 3)
        self.assertTrue(all(call.args == (CHANNEL, None, None) for call in fetch.call_args_list))
        self.assertEqual(quiet_before, next(document for document in store.items.values() if document["post_id"] == "quiet"))
        self.assertEqual(len(store.checkpoints), 1)

    async def test_non_message_posts_and_replies_are_not_archived(self):
        reply = graph_message({"id": "reply", "replyToId": "root"})
        fetch = AsyncMock(return_value={"value": [
            {"id": "system-root", "messageType": "systemEventMessage"},
            graph_message({
                "id": "root",
                "replies": [
                    {"id": "system-reply", "messageType": "systemEventMessage"},
                    reply,
                ],
            }),
        ]})
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store, TENANT).collect([CHANNEL])

        self.assertEqual(result["postsRead"], 1)
        self.assertEqual(result["repliesRead"], 1)
        self.assertEqual(next(iter(store.items.values()))["replies"], [reply])

    async def test_expanded_reply_without_reply_to_id_uses_root_context(self):
        reply = graph_message({"id": "reply"})
        fetch = AsyncMock(return_value={"value": [
            graph_message({"id": "root", "replies": [reply]}),
        ]})
        store = MemoryStore()

        result = await TeamsCollectionService(fetch, store, TENANT).collect([CHANNEL])

        self.assertEqual(result["repliesRead"], 1)
        self.assertEqual(next(iter(store.items.values()))["replies"], [reply])

    async def test_partial_expansion_falls_back_to_full_reply_paging(self):
        root = graph_message({"id": "root"})
        reply1 = graph_message({"id": "reply1", "replyToId": "root"})
        reply2 = graph_message({"id": "reply2", "replyToId": "root"})
        fetch = AsyncMock(side_effect=[
            {"value": [{**root, "replies": [reply1], "replies@odata.count": 1,
                        "replies@odata.nextLink": next_link(CHANNEL, "root")}]},
            {"value": [reply1], "@odata.nextLink": next_link(CHANNEL, "root")},
            {"value": [reply2]},
            {"value": [{**root, "replies": [reply2, reply1], "replies@odata.count": 2}]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        await service.collect([CHANNEL])
        unchanged = await service.collect([CHANNEL])
        document = next(iter(store.items.values()))
        self.assertEqual(document["post"], root)
        self.assertEqual(document["replies"], [reply1, reply2])
        self.assertEqual(unchanged["postsUnchanged"], 1)
        self.assertEqual(store.writes, 1)
        self.assertEqual(fetch.call_args_list[1].args, (CHANNEL, "root", None))
        self.assertEqual(fetch.call_args_list[2].args, (CHANNEL, "root", "next"))

    async def test_failed_scan_preserves_checkpoint_and_retries_completed_threads(self):
        from datetime import datetime, timezone

        root = graph_message({"id": "root", "replies": []})
        changed = {**root, "body": {"content": "updated"}}
        fetch = AsyncMock(side_effect=[
            {"value": [root]},
            {"value": [changed], "@odata.nextLink": next_link(CHANNEL)}, RuntimeError("page failed"),
            {"value": [changed]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        with patch("services.teams_collection_service.datetime", wraps=datetime) as clock:
            clock.now.side_effect = [datetime(2026, 9, 10, hour, tzinfo=timezone.utc) for hour in range(7)]
            await service.collect([CHANNEL])
            checkpoint = copy.deepcopy(store.checkpoints)
            with self.assertRaisesRegex(RuntimeError, "page failed"):
                await service.collect([CHANNEL])
            self.assertEqual(store.checkpoints, checkpoint)
            retried = await service.collect([CHANNEL])
        self.assertEqual(retried["postsWritten"], 0)
        self.assertEqual(retried["postsUnchanged"], 1)
        self.assertEqual(store.writes, 2)
        self.assertNotEqual(store.checkpoints, checkpoint)

    async def test_backfills_processing_before_replacing_and_skips_current_result(self):
        channel = {
            **CHANNEL,
            "processingScope": {"name": "general", "description": "Azure developer experience."},
        }
        root = graph_message({
            "id": "root", "subject": "Question", "body": {"content": "How?"},
            "replies": [graph_message({
                "id": "reply", "replyToId": "root", "body": {"content": "Do this."},
            })],
        })

        class Processor:
            def __init__(self):
                self.calls = []

            def is_current(self, processing, digest):
                return bool(processing and processing["source_content_hash"] == digest)

            async def process(self, configured_channel, post, replies, digest):
                self.calls.append((configured_channel, post, replies, digest))
                return {
                    "processor": "teams-channel-qa-summary",
                    "processor_version": "v1",
                    "source_content_hash": digest,
                    "status": "included",
                    "qa": {"title": "Question", "question": "How?", "answer": "Do this."},
                }

        processor = Processor()
        fetch = AsyncMock(side_effect=[
            {"value": [root]}, {"value": [root]}, {"value": [root]},
        ])
        store = MemoryStore()
        await TeamsCollectionService(fetch, store, TENANT).collect([channel])
        self.assertNotIn("processing", next(iter(store.items.values())))
        service = TeamsCollectionService(fetch, store, TENANT, processor=processor)
        first = await service.collect([channel])
        second = await service.collect([channel])

        self.assertEqual(first["postsWritten"], 1)
        self.assertEqual(second["postsUnchanged"], 1)
        self.assertEqual(len(processor.calls), 1)
        self.assertEqual(store.writes, 2)
        document = next(iter(store.items.values()))
        self.assertEqual(document["processing"]["qa"]["answer"], "Do this.")
        self.assertNotIn("replies", processor.calls[0][1])
        self.assertEqual(processor.calls[0][2][0]["id"], "reply")

    async def test_processing_failure_does_not_write_thread_or_checkpoint(self):
        class Processor:
            def is_current(self, processing, digest):
                return False

            async def process(self, channel, post, replies, digest):
                raise RuntimeError("processing failed")

        fetch = AsyncMock(return_value={"value": [
            graph_message({"id": "root", "replies": []}),
        ]})
        store = MemoryStore()
        with self.assertRaisesRegex(RuntimeError, "processing failed"):
            await TeamsCollectionService(
                fetch, store, TENANT, processor=Processor()
            ).collect([CHANNEL])
        self.assertFalse(store.items)
        self.assertFalse(store.checkpoints)

    async def test_reprocessing_uses_stored_raw_thread_without_fetching_teams(self):
        root = graph_message({"id": "root", "body": {"content": "Question"}, "replies": []})
        store = MemoryStore()
        await TeamsCollectionService(
            AsyncMock(return_value={"value": [root]}), store, TENANT
        ).collect([CHANNEL])

        class Processor:
            def is_current(self, processing, digest):
                return False

            async def process(self, channel, post, replies, digest):
                return {
                    "processor": "teams-channel-qa-summary",
                    "processor_version": "v2",
                    "source_content_hash": digest,
                    "status": "excluded",
                    "exclusion_reason": "No human answer.",
                    "qa": None,
                }

        fetch = AsyncMock()
        result = await TeamsCollectionService(
            fetch, store, TENANT, processor=Processor()
        ).reprocess([CHANNEL])

        self.assertEqual(result, {
            "channelsCompleted": 1, "postsRead": 1, "postsReprocessed": 1,
        })
        fetch.assert_not_awaited()
        self.assertEqual(next(iter(store.items.values()))["processing"]["processor_version"], "v2")

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

    async def test_first_scan_is_full_even_with_a_lookback_window(self):
        from datetime import datetime, timezone

        first = graph_message({
            "id": "first", "createdDateTime": "2020-01-01T00:00:00Z", "replies": [],
        })
        second = graph_message({
            "id": "second", "createdDateTime": "2019-01-01T00:00:00Z", "replies": [],
        })
        fetch = AsyncMock(side_effect=[
            {"value": [first], "@odata.nextLink": next_link(CHANNEL)},
            {"value": [second]},
        ])
        store = MemoryStore()
        with patch("services.teams_collection_service.datetime", wraps=datetime) as clock:
            clock.now.return_value = datetime(2026, 9, 18, tzinfo=timezone.utc)
            result = await TeamsCollectionService(
                fetch, store, TENANT, lookback_days=7
            ).collect([CHANNEL])

        self.assertEqual(result["postsRead"], 2)
        self.assertEqual(fetch.await_count, 2)
        checkpoint = next(iter(store.checkpoints.values()))
        self.assertEqual(checkpoint["scan_mode"], "full")
        self.assertIsNone(checkpoint["lookback_cutoff"])

    async def test_incremental_scan_stops_after_the_reply_chain_activity_cutoff(self):
        import hashlib
        from datetime import datetime, timezone

        key = hashlib.sha256(json.dumps(
            [TENANT, CHANNEL["teamId"], CHANNEL["channelId"]]
        ).encode()).hexdigest()
        store = MemoryStore()
        store.checkpoints[key] = {
            "id": "channel-checkpoint",
            "channel_key": key,
            "last_successful_scan_started_at": "2026-09-11T00:00:00+00:00",
        }
        recent_reply = graph_message({
            "id": "recent-reply", "replyToId": "recent-thread",
            "createdDateTime": "2026-09-15T00:00:00Z",
        })
        recent_thread = graph_message({
            "id": "recent-thread", "createdDateTime": "2020-01-01T00:00:00Z",
            "lastModifiedDateTime": "2020-01-01T00:00:00Z",
            "replies": [recent_reply],
        })
        cutoff_thread = graph_message({
            "id": "cutoff", "createdDateTime": "2026-09-10T23:59:59Z",
            "replies": [],
        })
        older_thread = graph_message({
            "id": "older", "createdDateTime": "2019-01-01T00:00:00Z",
            "replies": [],
        })
        fetch = AsyncMock(return_value={
            "value": [recent_thread, cutoff_thread, older_thread],
            "@odata.nextLink": next_link(CHANNEL),
        })
        with patch("services.teams_collection_service.datetime", wraps=datetime) as clock:
            clock.now.return_value = datetime(2026, 9, 18, tzinfo=timezone.utc)
            result = await TeamsCollectionService(
                fetch, store, TENANT, lookback_days=7
            ).collect([CHANNEL])

        self.assertEqual(result, {
            "channelsCompleted": 1, "postsRead": 1, "postsWritten": 1,
            "postsUnchanged": 0, "repliesRead": 1,
        })
        fetch.assert_awaited_once_with(CHANNEL, None, None)
        self.assertEqual({document["post_id"] for document in store.items.values()},
                         {"recent-thread"})
        checkpoint = store.checkpoints[key]
        self.assertEqual(checkpoint["scan_mode"], "incremental")
        self.assertEqual(checkpoint["lookback_cutoff"], "2026-09-11T00:00:00+00:00")

    def test_rejects_invalid_lookback_window(self):
        for value in (True, 0, 31, 1.5, "7"):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "lookback_days"):
                TeamsCollectionService(AsyncMock(), MemoryStore(), TENANT, lookback_days=value)

    async def test_invalid_expanded_replies_do_not_write_or_checkpoint(self):
        for replies in (None, {}, [graph_message({"id": "reply", "replyToId": "another-root"})]):
            with self.subTest(replies=replies):
                fetch = AsyncMock(return_value={
                    "value": [graph_message({"id": "root", "replies": replies})],
                })
                store = MemoryStore()
                with self.assertRaises(ValueError):
                    await TeamsCollectionService(fetch, store, TENANT).collect([CHANNEL])
                self.assertFalse(store.items)
                self.assertFalse(store.checkpoints)

    async def test_cosmos_uses_conditional_replace_and_does_not_retry_conflicts(self):
        from azure.core import MatchConditions
        from azure.cosmos.exceptions import CosmosHttpResponseError
        from utils.teams_collection import CosmosThreadStore

        container = AsyncMock()
        store = CosmosThreadStore(container)
        document = {"id": "thread", "channel_key": "channel"}
        await store.write(document, None)
        container.create_item.assert_awaited_once_with(body=document)
        await store.write(document, {"_etag": "version1"})
        container.replace_item.assert_awaited_once_with(
            item="thread", body=document, etag="version1", match_condition=MatchConditions.IfNotModified)
        container.replace_item.side_effect = CosmosHttpResponseError(status_code=412, message="conflict")
        with self.assertRaisesRegex(RuntimeError, "Concurrent"):
            await store.write(document, {"_etag": "version1"})
        container.upsert_item.assert_not_called()

    async def test_logic_app_passes_only_configured_channel_and_continuation(self):
        import httpx
        from types import SimpleNamespace
        from utils.teams_collection import LogicAppPageClient

        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={"operation": "replies", "data": {"value": []}})
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="secret")
        url = "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke?api-version=2016-10-01"
        channel = {**CHANNEL, "startTime": "2026-09-01T00:00:00Z"}
        pages = LogicAppPageClient(client, credential, url, "https://management.core.windows.net/", [channel])
        self.assertEqual(await pages.fetch_page(channel, "root", "continuation"), {"value": []})
        self.assertEqual(client.post.call_args.kwargs["json"], {
            **CHANNEL, "operation": "replies", "messageId": "root", "skipToken": "continuation"})
        self.assertFalse(client.post.call_args.kwargs["follow_redirects"])
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
        from utils.teams_collection import LogicAppPageClient

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
                with patch("utils.teams_collection.asyncio.sleep", new=AsyncMock()) as sleep:
                    self.assertEqual(await pages.fetch_page(CHANNEL, None, None), {"value": []})
                self.assertEqual(client.post.await_count, 2)
                sleep.assert_awaited_once_with(1)

    async def test_start_time_filters_root_creation_inclusively_and_keeps_paging(self):
        channel = {**CHANNEL, "startTime": "2026-09-01T08:00:00+08:00"}
        second = {**CHANNEL, "channelId": "19:second@thread.tacv2", "startTime": "2026-09-02T00:00:00Z"}
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message({
                "id": "old", "createdDateTime": "2026-08-31T23:59:59Z",
                "lastModifiedDateTime": "2026-09-10T00:00:00Z",
            })],
             "@odata.nextLink": next_link(channel)},
            {"value": [
                graph_message({"id": "boundary", "createdDateTime": "2026-09-01T00:00:00Z"}),
                graph_message({"id": "later", "createdDateTime": "2026-09-03T09:00:00+08:00"}),
            ]},
            {"value": [graph_message({
                "id": "reply1", "replyToId": "boundary",
                "createdDateTime": "2026-09-01T01:00:00Z",
            })],
             "@odata.nextLink": next_link(channel, "boundary")},
            {"value": [graph_message({
                "id": "reply2", "replyToId": "boundary",
                "createdDateTime": "2026-09-10T00:00:00Z",
            })]},
            {"value": []},
            {"value": [
                graph_message({"id": "before-second", "createdDateTime": "2026-09-01T00:00:00Z"}),
                graph_message({"id": "second-boundary", "createdDateTime": "2026-09-02T00:00:00Z"}),
            ]},
            {"value": []},
        ])
        store = MemoryStore()
        result = await TeamsCollectionService(fetch, store, TENANT).collect([channel, second])
        self.assertEqual(result, {"channelsCompleted": 2, "postsRead": 3, "postsWritten": 3,
                                  "postsUnchanged": 0, "repliesRead": 2})
        self.assertEqual({document["post_id"] for document in store.items.values()},
                         {"boundary", "later", "second-boundary"})
        self.assertEqual(fetch.call_args_list[1].args, (channel, None, "next"))
        self.assertEqual(fetch.call_args_list[3].args, (channel, "boundary", "next"))
        thread = next(document for document in store.items.values() if document["post_id"] == "boundary")
        self.assertEqual([reply["id"] for reply in thread["replies"]], ["reply1", "reply2"])

    async def test_invalid_start_time_fails_before_fetch(self):
        for value in ("", "invalid", "2026-09-01", "2026-09-01T00:00:00", 123, False):
            with self.subTest(value=value):
                fetch = AsyncMock()
                store = MemoryStore()
                with self.assertRaisesRegex(ValueError, "startTime"):
                    await TeamsCollectionService(fetch, store, TENANT).collect([{**CHANNEL, "startTime": value}])
                fetch.assert_not_awaited()
                self.assertFalse(store.items)

    async def test_start_time_rejects_missing_or_invalid_post_creation_time(self):
        channel = {**CHANNEL, "startTime": "2026-09-01T00:00:00Z"}
        for value in (None, "invalid", "2026-09-02T00:00:00"):
            with self.subTest(value=value):
                root = graph_message({"id": "root"})
                if value is not None:
                    root["createdDateTime"] = value
                fetch = AsyncMock(return_value={"value": [root]})
                store = MemoryStore()
                with self.assertRaisesRegex(ValueError, "Post createdDateTime"):
                    await TeamsCollectionService(fetch, store, TENANT).collect([channel])
                fetch.assert_awaited_once_with(channel, None, None)
                self.assertFalse(store.items)

    async def test_start_time_is_optional_and_does_not_change_thread_identity(self):
        root = graph_message({"id": "root", "createdDateTime": "2026-08-01T00:00:00Z"})
        fetch = AsyncMock(side_effect=[{"value": [root]}, {"value": []}] * 3)
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        await service.collect([CHANNEL])
        for value in (None, "2026-08-01T00:00:00Z"):
            result = await service.collect([{**CHANNEL, "startTime": value}])
            self.assertEqual(result["postsUnchanged"], 1)
        self.assertEqual(len(store.items), 1)
        self.assertEqual(store.writes, 1)

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
        result = await TeamsCollectionService(fetch, store, TENANT).collect([CHANNEL, second])
        self.assertEqual(result, {"channelsCompleted": 2, "postsRead": 3, "postsWritten": 3,
                                  "postsUnchanged": 0, "repliesRead": 2})
        self.assertEqual(len(store.items), 3)
        self.assertEqual(fetch.call_args_list[2].args, (CHANNEL, "root", "next"))
        self.assertEqual(fetch.call_args_list[3].args, (CHANNEL, None, "next"))
        self.assertEqual(len(next(iter(store.items.values()))["replies"]), 2)

    async def test_reply_changes_update_existing_post_even_when_root_is_unchanged(self):
        root = graph_message({"id": "old", "lastModifiedDateTime": "2020-01-01T00:00:00Z"})
        fetch = AsyncMock(side_effect=[
            {"value": [root]}, {"value": []},
            {"value": [root]}, {"value": []},
            {"value": [root]}, {"value": [graph_message({"id": "new", "replyToId": "old"})]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        await service.collect([CHANNEL])
        unchanged = await service.collect([CHANNEL])
        self.assertEqual(unchanged["postsUnchanged"], 1)
        await service.collect([CHANNEL])
        self.assertEqual(len(store.items), 1)
        self.assertEqual(store.writes, 2)
        self.assertEqual(next(iter(store.items.values()))["replies"][0]["id"], "new")

    async def test_old_reply_edit_is_detected_without_a_root_timestamp_change(self):
        root = graph_message({
            "id": "root", "lastModifiedDateTime": "2026-09-01T00:00:00Z",
            "etag": "root-version",
        })
        reply = graph_message({
            "id": "reply", "replyToId": "root", "createdDateTime": "2026-09-01T01:00:00Z",
            "lastModifiedDateTime": "2026-09-01T01:00:00Z", "body": {"content": "original"},
        })
        edited = {**reply, "lastModifiedDateTime": "2026-09-10T00:00:00Z", "body": {"content": "edited"}}
        fetch = AsyncMock(side_effect=[
            {"value": [root]}, {"value": [reply]},
            {"value": [root]}, {"value": [edited]},
            {"value": [root]}, {"value": [edited]},
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        await service.collect([CHANNEL])
        changed = await service.collect([CHANNEL])
        unchanged = await service.collect([CHANNEL])
        self.assertEqual(changed["postsWritten"], 1)
        self.assertEqual(unchanged["postsUnchanged"], 1)
        self.assertEqual(store.writes, 2)
        self.assertEqual(next(iter(store.items.values()))["replies"][0]["body"]["content"], "edited")

    async def test_reply_failure_never_overwrites_existing_thread(self):
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message({"id": "root"})]},
            {"value": [graph_message({"id": "original", "replyToId": "root"})]},
            {"value": [graph_message({"id": "root"})]},
            {"value": [], "@odata.nextLink": next_link(CHANNEL, "root")}, RuntimeError("unavailable"),
        ])
        store = MemoryStore()
        service = TeamsCollectionService(fetch, store, TENANT)
        await service.collect([CHANNEL])
        with self.assertRaisesRegex(RuntimeError, "unavailable"):
            await service.collect([CHANNEL])
        self.assertEqual(store.writes, 1)
        self.assertEqual(next(iter(store.items.values()))["replies"][0]["id"], "original")

    async def test_reply_page_limit_and_wrong_parent_do_not_save_partial_thread(self):
        for page in ({"value": [], "@odata.nextLink": next_link(CHANNEL, "root")},
                     {"value": [graph_message({"id": "reply", "replyToId": "other"})]}):
            store = MemoryStore()
            fetch = AsyncMock(side_effect=[{"value": [graph_message({"id": "root"})]}, page])
            with self.assertRaises((RuntimeError, ValueError)):
                await TeamsCollectionService(fetch, store, TENANT, max_pages=1).collect([CHANNEL])
            self.assertFalse(store.items)

    async def test_repeated_continuation_fails_without_writing_partial_replies(self):
        page = {"value": [], "@odata.nextLink": next_link(CHANNEL, "root")}
        fetch = AsyncMock(side_effect=[{"value": [graph_message({"id": "root"})]}, page, page])
        store = MemoryStore()
        with self.assertRaisesRegex(RuntimeError, "repeated"):
            await TeamsCollectionService(fetch, store, TENANT).collect([CHANNEL])
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


if __name__ == "__main__":
    unittest.main()