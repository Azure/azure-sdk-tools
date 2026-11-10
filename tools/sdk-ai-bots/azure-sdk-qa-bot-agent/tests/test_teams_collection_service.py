import asyncio
import copy
import json
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from unittest.mock import AsyncMock, Mock, patch
from urllib.parse import quote

import httpx
from azure.cosmos.exceptions import CosmosResourceExistsError
from pydantic import ValidationError

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.teams_collection_service import (
    TeamsHistoryRequest,
    build_message_document,
    collect_channel_history,
    export_history_to_json,
    extract_skip_token,
    fetch_teams_page,
    normalize_message_content,
    validate_history_request,
)
from scripts import collect_teams_history as script

PROJECT = Path(__file__).resolve().parents[1]
MODULE = "services.teams_collection_service"
LOGIC_APP_URL = (
    "https://host.logic.azure.com/workflows/workflow/triggers/manual/paths/invoke"
    "?api-version=2016-10-01"
)


def request(**overrides):
    return TeamsHistoryRequest(**{
        "team_id": "7ccc31f0-b371-450b-a73c-48f5a31a9b96",
        "channel_id": "19:test@thread.tacv2",
        "tenant_id": "typespec_channel_qa_bot",
        **overrides,
    })


def next_link(message_id=None, token="next"):
    channel = request()
    path = f"/teams/{channel.team_id}/channels/{quote(channel.channel_id, safe='')}/messages"
    path = "/beta" + path if message_id is None else "/v1.0" + path + f"/{message_id}/replies"
    return (
        f"https://logic-apis-westus2.azure-apim.net/apim/teams/connection{path}"
        f"?$skiptoken={quote(token, safe='')}"
    )


def graph_message(message_id="root", **overrides):
    return {
        "id": message_id,
        "messageType": "message",
        "createdDateTime": "2026-09-01T00:00:00Z",
        "from": {"user": {"id": "user-id", "displayName": "Test User"}},
        "body": {"contentType": "html", "content": "<p>body</p>"},
        "webUrl": "https://teams.microsoft.com/l/message/thread/id",
        **overrides,
    }


def message_container():
    documents = {}
    container = AsyncMock()
    container.read.return_value = {"partitionKey": {"paths": ["/conversation_partition"]}}

    async def create(*, body):
        key = (body["conversation_partition"], body["id"])
        if key in documents:
            raise CosmosResourceExistsError(status_code=409, message="exists")
        documents[key] = copy.deepcopy(body)
        return body

    container.create_item.side_effect = create
    return container, documents


async def save_history_to_cosmos(history_request, fetch, container):
    history_request = history_request.model_copy(update={"save_to_cosmos": True})
    with TemporaryDirectory() as directory:
        return await export_history_to_json(
            history_request, fetch, Path(directory) / "history.json", container=container,
        )


class TeamsCollectionTests(unittest.IsolatedAsyncioTestCase):
    def test_workflow_templates_parameters_and_shared_deployment(self):
        pipeline = (PROJECT / "pipelines/logicapp-cd.yml").read_text(encoding="utf-8")
        guide = (PROJECT.parent / "doc/teams-channel-backfill.md").read_text(
            encoding="utf-8"
        )
        self.assertIn("- teams-collection", pipeline)
        self.assertEqual(pipeline.count("- task: AzureCLI@2"), 1)
        self.assertIn("format('pipelines/logicapp/template.{0}.json', parameters.workflow)", pipeline)
        self.assertIn(
            "format('pipelines/logicapp/parameters.{0}.{1}.{2}.json', parameters.workflow, variables.listenerTeam, parameters.environment)",
            pipeline,
        )
        self.assertIn("Unsupported Logic App configuration:", pipeline)
        self.assertIn("az deployment group create", pipeline)
        self.assertNotIn("listCallbackUrl", pipeline)
        self.assertNotIn("az appconfig kv set", pipeline)
        self.assertIn("listCallbackUrl", guide)
        self.assertIn("TEAMS_COLLECTION_LOGIC_APP_URL", guide)
        self.assertIn("SAS query parameters", guide)
        for workflow, team, environment in (
            ("chat", "azure_sdk", "dev"),
            ("chat", "azure_sdk", "test"),
            ("chat", "azure_sdk", "prod"),
            ("chat", "azure_mcp_server", "prod"),
            ("teams-collection", "azure_sdk", "dev"),
            ("teams-collection", "azure_sdk", "test"),
            ("teams-collection", "azure_sdk", "prod"),
        ):
            with self.subTest(workflow=workflow, team=team, environment=environment):
                json.loads((PROJECT / f"pipelines/logicapp/template.{workflow}.json").read_text())
                json.loads(
                    (PROJECT / f"pipelines/logicapp/parameters.{workflow}.{team}.{environment}.json")
                    .read_text()
                )

        template = json.loads(
            (PROJECT / "pipelines/logicapp/template.teams-collection.json").read_text()
        )
        self.assertEqual([resource["type"] for resource in template["resources"]],
                         ["Microsoft.Logic/workflows"])
        self.assertNotIn("allowedChannels", json.dumps(template))
        triggers = template["resources"][0]["properties"]["accessControl"]["triggers"]
        self.assertEqual(triggers["sasAuthenticationPolicy"]["state"], "Disabled")
        claims = template["variables"]["backfillPolicy"]["backfill"]["claims"]
        developer_claims = template["variables"]["developerPolicy"]["developers"]["claims"]
        self.assertIn({"name": "roles", "value": "TeamsHistory.Invoke"}, developer_claims)
        self.assertIn({"name": "aud", "value": "[parameters('developerAppId')]"}, developer_claims)
        self.assertIn(
            "[reference(parameters('serverIdentityResourceId'), '2018-11-30').principalId]",
            [claim["value"] for claim in claims],
        )
        for environment in ("dev", "test", "prod"):
            collection = json.loads(
                (PROJECT / f"pipelines/logicapp/parameters.teams-collection.azure_sdk.{environment}.json")
                .read_text()
            )["parameters"]
            chat = json.loads(
                (PROJECT / f"pipelines/logicapp/parameters.chat.azure_sdk.{environment}.json")
                .read_text()
            )["parameters"]
            self.assertEqual(collection["serverIdentityResourceId"], chat["serverIdentityResourceId"])
            self.assertNotIn("allowedChannels", collection)

    def test_normalization_keeps_title_forwarded_text_and_image_html(self):
        html = '<p>See <attachment id="a1"></attachment></p><img src="https://example.com/image">'
        for forwarded in (
            {"body": {"content": "<p>forwarded</p>"}},
            {"message": {"body": {"content": "<p>forwarded</p>"}}},
            {"originalMessage": {"body": {"content": "<p>forwarded</p>"}}},
            {"content": "<p>forwarded</p>"},
        ):
            message = graph_message(
                subject="  Need help  ", body={"content": html},
                attachments=[{"id": "a1", "contentType": "forwardedMessageReference",
                              "content": json.dumps(forwarded)}],
            )
            self.assertEqual(
                normalize_message_content(message),
                'title: Need help\n\n<p>See <p>forwarded</p></p><img src="https://example.com/image">',
            )
        self.assertEqual(normalize_message_content(graph_message()), "<p>body</p>")
        self.assertEqual(normalize_message_content(graph_message(
            body={"content": '<attachment id="a1"></attachment>'},
            attachments=[{"id": "a1", "contentType": "forwardedMessageReference",
                          "content": "not json"}],
        )), "not json")

    async def test_import_keeps_message_shape_attachments_and_original_images(self):
        root = graph_message(
            subject="Need help",
            body={"content": '<p>body</p><img src="https://example.com/image">'},
            attachments=[{"id": "file", "contentType": "reference", "name": "file.docx"}],
            replies=[graph_message(
                "reply", replyToId="root",
                **{"from": {"application": {"id": "bot-id", "displayName": "QA Bot"}}},
            )],
        )
        fetch = AsyncMock(return_value={"value": [root]})
        container, documents = message_container()
        summary = await save_history_to_cosmos(request(), fetch, container)
        self.assertEqual(summary, {"channelsCompleted": 1, "threadsRead": 1, "messagesRead": 2,
                                   "messagesCreated": 2, "messagesSkipped": 0, "messagesExported": 2})
        partition = f"teams_channel:{request().channel_id};messageid=root"
        document = documents[(partition, "root")]
        self.assertEqual(document["conversation_id"], f"{request().channel_id};messageid=root")
        self.assertEqual(document["tenant_id"], request().tenant_id)
        self.assertEqual(document["created_at"], "2026-09-01T00:00:00Z")
        self.assertEqual(document["content"], 'title: Need help\n\n<p>body</p><img src="https://example.com/image">')
        self.assertEqual(document["extra_info"]["attachments"][0]["name"], "file.docx")
        self.assertNotIn("images", document["extra_info"])
        self.assertNotIn("should_reply", document)
        self.assertNotIn("trace_id", document)
        self.assertEqual(documents[(partition, "reply")]["sender_role"], "system")
        container.upsert_item.assert_not_awaited()
        container.replace_item.assert_not_awaited()

    async def test_repeated_import_skips_existing_messages_even_when_teams_content_changes(self):
        root = graph_message(replies=[])
        fetch = AsyncMock(side_effect=[
            {"value": [root]},
            {"value": [root]},
            {"value": [{**root, "body": {"content": "edited in Teams"}}]},
        ])
        container, documents = message_container()
        self.assertEqual((await save_history_to_cosmos(request(), fetch, container))["messagesCreated"], 1)
        before = copy.deepcopy(documents)
        for _ in range(2):
            summary = await save_history_to_cosmos(request(), fetch, container)
            self.assertEqual(summary["messagesCreated"], 0)
            self.assertEqual(summary["messagesSkipped"], 1)
            self.assertEqual(documents, before)
        container.read_item.assert_not_awaited()
        container.query_items.assert_not_called()
        container.replace_item.assert_not_awaited()
        container.upsert_item.assert_not_awaited()

    async def test_live_write_during_reply_fetch_is_not_overwritten(self):
        container, documents = message_container()
        live = build_message_document(request(), graph_message(body={"content": "newer live edit"}), "root")
        live.update(should_reply=True, trace_id="live-trace", _etag="new-version")
        live["extra_info"]["images"] = [{"id": "existing-image", "contentUrl": "https://example.com/image"}]

        async def fetch(message_id, token):
            if message_id is None:
                return {"value": [graph_message(body={"content": "stale snapshot"})]}
            documents[(live["conversation_partition"], live["id"])] = copy.deepcopy(live)
            return {"value": [graph_message("reply", replyToId="root")]}

        summary = await save_history_to_cosmos(request(), fetch, container)
        self.assertEqual(summary["messagesCreated"], 1)
        self.assertEqual(summary["messagesSkipped"], 1)
        self.assertEqual(documents[(live["conversation_partition"], "root")], live)
        container.replace_item.assert_not_awaited()
        container.upsert_item.assert_not_awaited()

    async def test_save_checks_partition_and_propagates_non_conflict_failures(self):
        container, _ = message_container()
        container.read.return_value = {"partitionKey": {"paths": ["/wrong"]}}
        with TemporaryDirectory() as directory, \
                patch(MODULE + ".get_conversation_message_container", new=AsyncMock(return_value=container)):
            with self.assertRaisesRegex(ValueError, "conversation_partition"):
                await collect_channel_history(
                    request(save_to_cosmos=True), Path(directory) / "history.json", LOGIC_APP_URL,
                )
        container.create_item.side_effect = RuntimeError("unavailable")
        fetch = AsyncMock(return_value={"value": [graph_message(replies=[])]})
        with self.assertRaisesRegex(RuntimeError, "unavailable"):
            await save_history_to_cosmos(request(), fetch, container)

    async def test_root_and_reply_paging_deduplicate_messages(self):
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message()], "@odata.nextLink": next_link()},
            {"value": [graph_message("reply", replyToId="root")], "@odata.nextLink": next_link("root")},
            {"value": [graph_message("reply", replyToId="root"), graph_message("reply2", replyToId="root")]},
            {"value": [graph_message(), graph_message("other", replies=[])]},
        ])
        container, documents = message_container()
        summary = await save_history_to_cosmos(request(), fetch, container)
        self.assertEqual(summary["threadsRead"], 2)
        self.assertEqual(summary["messagesCreated"], 4)
        self.assertEqual(len(documents), 4)
        self.assertEqual([call.args for call in fetch.call_args_list],
                         [(None, None), ("root", None), ("root", "next"), (None, "next")])

    async def test_partial_expansion_fetches_all_reply_pages(self):
        fetch = AsyncMock(side_effect=[
            {"value": [graph_message(replies=[graph_message("partial", replyToId="root")],
                                     **{"replies@odata.nextLink": next_link("root")})]},
            {"value": [graph_message("reply", replyToId="root")], "@odata.nextLink": next_link("root")},
            {"value": [graph_message("reply2", replyToId="root")]},
        ])
        container, documents = message_container()
        summary = await save_history_to_cosmos(request(max_pages=1), fetch, container)
        self.assertEqual(summary["messagesCreated"], 3)
        self.assertEqual(fetch.await_count, 3)
        self.assertEqual({key[1] for key in documents}, {"root", "reply", "reply2"})

    async def test_reply_failure_limits_and_wrong_parent_do_not_write_partial_thread(self):
        for reply_result, overrides in (
            (RuntimeError("unavailable"), {}),
            ({"value": [], "@odata.nextLink": next_link("root")}, {"max_pages": 1}),
            ({"value": [graph_message("reply", replyToId="wrong-root")]}, {}),
            ({"value": [{"messageType": "message"}]}, {}),
            ({"value": None}, {}),
        ):
            with self.subTest(reply_result=reply_result):
                container, documents = message_container()
                fetch = AsyncMock(side_effect=[{"value": [graph_message()]}, reply_result])
                with patch(f"{MODULE}.MAX_HISTORY_PAGES", 1), self.assertRaises((RuntimeError, ValueError)):
                    await save_history_to_cosmos(request(**overrides), fetch, container)
                self.assertEqual(documents, {})

    async def test_repeated_reply_token_fails_before_writing(self):
        page = {"value": [], "@odata.nextLink": next_link("root")}
        fetch = AsyncMock(side_effect=[{"value": [graph_message()]}, page, page])
        container, documents = message_container()
        with self.assertRaisesRegex(RuntimeError, "repeated"):
            await save_history_to_cosmos(request(), fetch, container)
        self.assertEqual(documents, {})

    async def test_root_page_limit_reports_incomplete_import(self):
        fetch = AsyncMock(return_value={
            "value": [graph_message(replies=[])], "@odata.nextLink": next_link(),
        })
        container, _ = message_container()
        with self.assertRaisesRegex(RuntimeError, "Page limit"):
            await save_history_to_cosmos(request(max_pages=1), fetch, container)

    async def test_start_time_filters_root_creation_and_stops_after_activity_cutoff(self):
        fetch = AsyncMock(side_effect=[
            {"value": [
                graph_message("old-active", createdDateTime="2026-08-01T00:00:00Z",
                              lastModifiedDateTime="2026-09-02T00:00:00Z", replies=[]),
                graph_message("boundary", replies=[]),
            ], "@odata.nextLink": next_link()},
            {"value": [
                graph_message("quiet", createdDateTime="2026-08-01T00:00:00Z", replies=[]),
                graph_message("not-read", replies=[]),
            ]},
        ])
        container, documents = message_container()
        summary = await save_history_to_cosmos(
            request(start_time="2026-09-01T08:00:00+08:00"), fetch, container,
        )
        self.assertEqual(summary["messagesCreated"], 1)
        self.assertEqual({key[1] for key in documents}, {"boundary"})
        self.assertEqual(fetch.await_count, 2)

    async def test_system_events_are_ignored_and_expanded_replies_may_omit_parent(self):
        fetch = AsyncMock(return_value={"value": [
            {"id": "event", "messageType": "systemEventMessage"},
            graph_message(replies=[
                {"id": "reply-event", "messageType": "systemEventMessage"},
                graph_message("reply"),
            ]),
        ]})
        container, documents = message_container()
        summary = await save_history_to_cosmos(request(), fetch, container)
        self.assertEqual(summary["messagesCreated"], 2)
        self.assertEqual({key[1] for key in documents}, {"root", "reply"})

    def test_continuation_validation(self):
        self.assertEqual(extract_skip_token(next_link(token="a+b/=c"), request(), None), "a+b/=c")
        self.assertEqual(extract_skip_token(next_link() + "&$expand=replies", request(), None), "next")
        graph_link = next_link("root").replace(
            "logic-apis-westus2.azure-apim.net/apim/teams/connection", "graph.microsoft.com"
        )
        self.assertEqual(extract_skip_token(graph_link, request(), "root"), "next")
        for link in (
            next_link().replace("logic-apis-westus2.azure-apim.net", "example.com"),
            next_link().replace("/messages?", "/other?"),
            graph_link,
            next_link() + "&$expand=unexpected",
            next_link() + "&$expand=replies&$expand=replies",
        ):
            with self.subTest(link=link), self.assertRaises(ValueError):
                extract_skip_token(link, request(), None)

    async def test_logic_app_sends_bound_channel_and_managed_identity_token(self):
        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={"operation": "replies", "data": {"value": []}})
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="test-token")
        self.assertEqual(
            await fetch_teams_page(client, credential, LOGIC_APP_URL, request(), "root", "continuation"),
            {"value": []},
        )
        self.assertEqual(client.post.call_args.kwargs["json"], {
            "teamId": request().team_id, "channelId": request().channel_id,
            "operation": "replies", "messageId": "root", "skipToken": "continuation",
        })
        self.assertEqual(client.post.call_args.kwargs["headers"], {"Authorization": "Bearer test-token"})
        self.assertFalse(client.post.call_args.kwargs["follow_redirects"])
        credential.get_token.assert_awaited_once_with("https://management.core.windows.net/.default")
        for url in (LOGIC_APP_URL + "&sig=secret", LOGIC_APP_URL.replace("host.logic.azure.com", "example.com")):
            with self.assertRaises(ValueError):
                await fetch_teams_page(client, credential, url, request())

    async def test_logic_app_retries_transient_failures_but_not_forbidden(self):
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="test-token")
        success = httpx.Response(200, json={"operation": "messages", "data": {"value": []}})
        for transient in (httpx.ReadTimeout("timeout"), httpx.Response(503), httpx.Response(429)):
            client = AsyncMock()
            client.post.side_effect = [transient, success]
            with patch(MODULE + ".asyncio.sleep", new=AsyncMock()) as sleep:
                result = await fetch_teams_page(client, credential, LOGIC_APP_URL, request())
            self.assertEqual(result, {"value": []})
            sleep.assert_awaited_once_with(1)
        client = AsyncMock()
        client.post.return_value = httpx.Response(403, json={"error": "private-response"})
        with self.assertRaisesRegex(RuntimeError, "HTTP 403") as failure:
            await fetch_teams_page(client, credential, LOGIC_APP_URL, request())
        self.assertNotIn("private-response", str(failure.exception))
        self.assertEqual(client.post.await_count, 1)

    async def test_developer_authentication_requests_the_configured_scope(self):
        client = AsyncMock()
        client.post.return_value = httpx.Response(200, json={
            "operation": "messages", "data": {"value": []},
        })
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="test-token")
        scope = "api://azure-sdk-qa-bot-dev/.default"
        await fetch_teams_page(client, credential, LOGIC_APP_URL, request(), token_scope=scope)
        credential.get_token.assert_awaited_once_with(scope)

    async def test_invalid_logic_app_response_fails_explicitly(self):
        credential = AsyncMock()
        for body in ({}, {"operation": "replies", "data": {}},
                     {"operation": "messages", "data": []}):
            client = AsyncMock()
            client.post.return_value = httpx.Response(200, json=body)
            with self.assertRaisesRegex(ValueError, "invalid collection response"):
                await fetch_teams_page(client, credential, LOGIC_APP_URL, request())

    async def test_collection_wires_configuration_client_and_cosmos(self):
        client = AsyncMock()
        client.__aenter__.return_value = client
        client.post.return_value = httpx.Response(200, json={
            "operation": "messages", "data": {"value": [graph_message(replies=[])]},
        })
        credential = AsyncMock()
        credential.get_token.return_value = SimpleNamespace(token="test-token")
        container, documents = message_container()
        with TemporaryDirectory() as directory, \
                patch(MODULE + ".get_conversation_message_container", new=AsyncMock(return_value=container)), \
                patch(MODULE + ".get_credential", return_value=credential), \
                patch(MODULE + ".httpx.AsyncClient", return_value=client):
            summary = await collect_channel_history(
                request(save_to_cosmos=True), Path(directory) / "history.json", LOGIC_APP_URL,
            )
        self.assertEqual(summary["messagesCreated"], 1)
        self.assertEqual(len(documents), 1)
        container.read.assert_awaited_once()

    async def test_export_preserves_raw_threads_and_applies_the_root_time_filter(self):
        root = graph_message(
            "root", subject="History", body={"content": "<p>\u5386\u53f2 <img src='image'></p>"},
            attachments=[{"id": "attachment", "contentType": "reference", "name": "file.txt"}],
        )
        reply = graph_message("reply", replyToId="root")
        fetch = AsyncMock(side_effect=[
            {"value": [
                root,
                graph_message("other", replies=[]),
                graph_message("old", createdDateTime="2026-08-01T00:00:00Z",
                              lastModifiedDateTime="2026-09-02T00:00:00Z", replies=[]),
            ]},
            {"value": [reply], "@odata.nextLink": next_link("root")},
            {"value": [reply, graph_message("reply2", replyToId="root")]},
        ])
        with TemporaryDirectory() as directory:
            path = Path(directory) / "history.json"
            summary = await export_history_to_json(
                request(start_time="2026-09-01T00:00:00Z", max_pages=1), fetch, path,
            )
            result = json.loads(path.read_text(encoding="utf-8"))
        self.assertFalse(result["request"]["save_to_cosmos"])
        self.assertEqual(result["threads"][0]["post"], root)
        self.assertEqual([item["id"] for item in result["threads"][0]["replies"]], ["reply", "reply2"])
        self.assertEqual(result["threads"][1], {"post": graph_message("other"), "replies": []})
        self.assertEqual(summary, {
            "channelsCompleted": 1, "threadsRead": 2, "messagesRead": 4, "messagesExported": 4,
        })

    async def test_empty_export_is_valid_json(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "history.json"
            summary = await export_history_to_json(
                request(), AsyncMock(return_value={"value": []}), path,
            )
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["threads"], [])
        self.assertEqual(summary, {
            "channelsCompleted": 1, "threadsRead": 0, "messagesRead": 0, "messagesExported": 0,
        })

    async def test_default_collection_exports_without_cosmos_access(self):
        with TemporaryDirectory() as directory, \
                patch(MODULE + ".get_credential", return_value=Mock()), \
                patch(MODULE + ".fetch_teams_page", new=AsyncMock(
                    return_value={"value": [graph_message(replies=[])]},
                )), \
                patch(MODULE + ".get_conversation_message_container", new=AsyncMock()) as cosmos:
            path = Path(directory) / "history.json"
            summary = await collect_channel_history(request(tenant_id=None), path, LOGIC_APP_URL)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8"))["threads"], [
                {"post": graph_message(), "replies": []},
            ])
        cosmos.assert_not_awaited()
        self.assertEqual(summary["messagesExported"], 1)
        self.assertNotIn("messagesCreated", summary)

    async def test_failed_or_cancelled_export_removes_partial_file(self):
        for result in (
            {"value": [graph_message(replies=[])], "@odata.nextLink": next_link()},
            RuntimeError("unavailable"),
            asyncio.CancelledError(),
        ):
            with self.subTest(result=result), TemporaryDirectory() as directory:
                path = Path(directory) / "history.json"
                fetch = AsyncMock(side_effect=[result])
                with self.assertRaises((RuntimeError, asyncio.CancelledError)):
                    await export_history_to_json(request(max_pages=1), fetch, path)
                self.assertFalse(path.exists())

    async def test_export_does_not_overwrite_existing_file(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "history.json"
            path.write_text("existing history", encoding="utf-8")
            fetch = AsyncMock()
            with self.assertRaises(FileExistsError):
                await export_history_to_json(request(), fetch, path)
            self.assertEqual(path.read_text(encoding="utf-8"), "existing history")
            fetch.assert_not_awaited()

    async def test_export_ignores_container_without_explicit_save_flag(self):
        container, documents = message_container()
        with TemporaryDirectory() as directory:
            await export_history_to_json(
                request(), AsyncMock(return_value={"value": [graph_message(replies=[])]}),
                Path(directory) / "history.json", container=container,
            )
        self.assertEqual(documents, {})
        container.create_item.assert_not_awaited()

    async def test_invalid_request_fails_before_fetch(self):
        for overrides in ({"team_id": "invalid"}, {"channel_id": " "},
                          {"tenant_id": " "}, {"start_time": "invalid"}):
            fetch = AsyncMock()
            container, _ = message_container()
            with self.assertRaises(ValueError):
                await save_history_to_cosmos(request(**overrides), fetch, container)
            fetch.assert_not_awaited()
        with self.assertRaises(ValidationError):
            request(max_pages=1001)

    def test_tenant_is_required_only_for_cosmos_writes(self):
        validate_history_request(request(tenant_id=None))
        with self.assertRaisesRegex(ValueError, "tenant_id"):
            validate_history_request(request(tenant_id=None, save_to_cosmos=True))

    def test_script_save_flag_is_opt_in(self):
        arguments = [
            "--team-id", request().team_id, "--channel-id", request().channel_id,
            "--output", "history.json",
        ]
        self.assertFalse(script.parse_args(arguments).save_to_cosmos)
        with patch("sys.stderr"), self.assertRaises(SystemExit) as failure:
            script.parse_args([*arguments, "--save-to-cosmos"])
        self.assertEqual(failure.exception.code, 2)
        args = script.parse_args([*arguments, "--save-to-cosmos", "--tenant-id", request().tenant_id])
        self.assertTrue(args.save_to_cosmos)

    async def test_script_exports_json_and_only_saves_when_requested(self):
        for save in (False, True):
            with self.subTest(save=save), TemporaryDirectory() as directory:
                path = Path(directory) / "history.json"
                arguments = [
                    "collect_teams_history.py", "--team-id", request().team_id,
                    "--channel-id", request().channel_id, "--output", str(path),
                    "--logic-app-url", LOGIC_APP_URL,
                ]
                if save:
                    arguments += ["--save-to-cosmos", "--tenant-id", request().tenant_id]
                container, documents = message_container()
                with patch("sys.argv", arguments), patch("builtins.print") as report, \
                        patch.object(script.app_config, "init", new=AsyncMock()) as init, \
                        patch.object(script, "close_cosmos_client", new=AsyncMock()), \
                        patch.object(script, "close_credential", new=AsyncMock()), \
                        patch(MODULE + ".get_credential", return_value=Mock()), \
                        patch(MODULE + ".fetch_teams_page", new=AsyncMock(return_value={
                            "value": [graph_message(replies=[graph_message("reply")])],
                        })) as fetch, \
                        patch(MODULE + ".get_conversation_message_container",
                              new=AsyncMock(return_value=container)) as cosmos:
                    await script.main()
                exported = json.loads(path.read_text(encoding="utf-8"))
                summary = json.loads(report.call_args.args[0])["summary"]
                self.assertEqual(exported["request"]["save_to_cosmos"], save)
                self.assertEqual(len(exported["threads"]), 1)
                self.assertEqual(summary["messagesExported"], 2)
                fetch.assert_awaited_once()
                if save:
                    cosmos.assert_awaited_once()
                    init.assert_awaited_once()
                    self.assertEqual(summary["messagesCreated"], 2)
                    self.assertEqual(len(documents), 2)
                else:
                    cosmos.assert_not_awaited()
                    init.assert_not_awaited()
                    self.assertEqual(documents, {})
                    self.assertNotIn("messagesCreated", summary)

    async def test_script_skips_app_config_for_export_with_explicit_callback(self):
        args = script.parse_args([
            "--team-id", request().team_id, "--channel-id", request().channel_id,
            "--output", "history.json", "--logic-app-url", LOGIC_APP_URL,
            "--logic-app-scope", "api://azure-sdk-qa-bot-dev/.default",
        ])
        with TemporaryDirectory() as directory, \
                patch.object(script.app_config, "init", new=AsyncMock()) as init, \
                patch.object(script, "collect_channel_history", new=AsyncMock(return_value={})) as collect, \
                patch.object(script, "close_cosmos_client", new=AsyncMock()) as close_cosmos, \
                patch.object(script, "close_credential", new=AsyncMock()) as close_credential:
            args.output = Path(directory) / "history.json"
            await script.run(args)
        init.assert_not_awaited()
        self.assertFalse(collect.call_args.args[0].save_to_cosmos)
        self.assertEqual(collect.call_args.args[2], LOGIC_APP_URL)
        self.assertEqual(collect.call_args.kwargs["token_scope"], "api://azure-sdk-qa-bot-dev/.default")
        close_cosmos.assert_awaited_once()
        close_credential.assert_awaited_once()

    async def test_script_loads_config_for_cosmos_and_closes_clients_on_failure(self):
        args = script.parse_args([
            "--team-id", request().team_id, "--channel-id", request().channel_id,
            "--tenant-id", request().tenant_id, "--output", "history.json",
            "--save-to-cosmos",
        ])
        with TemporaryDirectory() as directory, \
                patch.object(script.app_config, "init", new=AsyncMock()) as init, \
                patch.object(script.app_config, "get", return_value=LOGIC_APP_URL), \
                patch.object(script, "collect_channel_history", new=AsyncMock(
                    side_effect=RuntimeError("collection failed"),
                )) as collect, \
                patch.object(script, "close_cosmos_client", new=AsyncMock()) as close_cosmos, \
                patch.object(script, "close_credential", new=AsyncMock()) as close_credential:
            with self.assertRaisesRegex(RuntimeError, "collection failed"):
                args.output = Path(directory) / "history.json"
                await script.run(args)
        init.assert_awaited_once()
        self.assertTrue(collect.call_args.args[0].save_to_cosmos)
        close_cosmos.assert_awaited_once()
        close_credential.assert_awaited_once()

    async def test_live_save_keeps_attachments_without_deriving_image_metadata(self):
        from models.conversation import ConversationMessage
        from services.conversation_service import ConversationService

        document = build_message_document(request(), graph_message(
            body={"content": '<img src="https://example.com/image">'},
            attachments=[{"id": "file", "contentType": "reference"}],
        ), "root")
        message = ConversationMessage.model_validate(document)
        container = AsyncMock()
        container.upsert_item.side_effect = lambda body: body
        with patch("services.conversation_service.get_conversation_message_container",
                   new=AsyncMock(return_value=container)):
            await ConversationService().save_conversation(message)
        saved = container.upsert_item.call_args.args[0]
        self.assertEqual(saved["content"], document["content"])
        self.assertEqual(saved["extra_info"], document["extra_info"])
        self.assertNotIn("images", saved["extra_info"])


if __name__ == "__main__":
    unittest.main()
