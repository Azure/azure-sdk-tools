import argparse
import asyncio
import hashlib
import json
import logging
import os
from pathlib import Path
import re
import runpy
import sys
from typing import overload
from urllib.request import urlopen

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from research.profiles import PROFILES, matches, source_content

AGENT = Path(__file__).resolve().parents[2] / "azure-sdk-qa-bot-agent"
logger = logging.getLogger(__name__)


def load_snapshot(value):
    if not value:
        raise ValueError("QA_RESEARCH_SNAPSHOT is required for a source-bearing research profile")
    snapshot = json.loads(value)
    revision = snapshot["source_revision"]
    if not re.fullmatch(r"[0-9a-f]{40}", revision):
        raise ValueError("Research source revision must be a full commit SHA")
    if snapshot["contract"]["source_revision"] != revision:
        raise ValueError("Research source revision mismatch")
    expected_paths = {
        "eng/pipelines/mirror-repos.yml",
        "eng/pipelines/templates/steps/sync-repo-merge-branch.yml",
        "eng/pipelines/templates/steps/sync-repo-branch.yml",
        "eng/scripts/Merge-Branch.ps1",
        "eng/scripts/Git-Repo-Sync.ps1",
    }
    sources = snapshot["contract"]["source_files"]
    if {source["path"] for source in sources} != expected_paths or len(sources) != len(expected_paths):
        raise ValueError("Unexpected research source paths")
    texts = {}
    for source in sources:
        url = f"https://raw.githubusercontent.com/Azure/azure-sdk-tools/{revision}/{source['path']}"
        with urlopen(url, timeout=30) as response:
            body = response.read()
        if hashlib.sha256(body).hexdigest() != source["sha256"]:
            raise ValueError(f"Research source checksum mismatch: {source['path']}")
        texts[source["path"]] = body.decode("utf-8")
    snapshot["texts"] = texts
    return snapshot


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", choices=["", *PROFILES], default="")
    args = parser.parse_args()
    sys.path.insert(0, str(AGENT))
    os.chdir(AGENT)
    if not args.profile:
        runpy.run_path(str(AGENT / "server.py"), run_name="__main__")
        return
    packet = ""
    if args.profile != "baseline":
        packet = source_content(args.profile, load_snapshot(os.environ.get("QA_RESEARCH_SNAPSHOT")))
    import uvicorn
    import server
    import config.app_config as app_config
    from config.tenant_config import TenantID
    from models.chat import AdditionalInfo, AdditionalInfoType
    from services.chat_service import ChatService

    class ResearchService(ChatService):
        def _build_tenant_system_message(self, tenant_id):
            original = super()._build_tenant_system_message(tenant_id)
            if tenant_id == TenantID.API_SPEC_REVIEW_BOT and PROFILES[args.profile]:
                return original + "\n[research_reasoning_protocol]\n" + PROFILES[args.profile]
            return original

        async def chat(self, req):
            if packet and matches(req.message.content, req.tenant_id.value):
                req = req.model_copy(update={"additional_infos": [
                    *(req.additional_infos or []),
                    AdditionalInfo(type=AdditionalInfoType.Text, content=packet),
                ]})
            return await super().chat(req)

    class PinnedSettings:
        @overload
        def __call__(self, key: str, default: str) -> str: ...

        @overload
        def __call__(self, key: str, default: None = None) -> str | None: ...

        def __call__(self, key: str, default: str | None = None) -> str | None:
            if key == "AI_FOUNDRY_AGENT_VERSION":
                return "267"
            return app_config.get(key, default)

    async def serve():
        await app_config.init()
        server._chat_service = ResearchService(settings=PinnedSettings())
        logger.warning(
            "Opt-in QA research profile=%s agent_version=267 evidence_sha256=%s",
            args.profile, hashlib.sha256(packet.encode()).hexdigest(),
        )
        await uvicorn.Server(uvicorn.Config(server.app, host="127.0.0.1", port=8089)).serve()

    asyncio.run(serve())


if __name__ == "__main__":
    main()
