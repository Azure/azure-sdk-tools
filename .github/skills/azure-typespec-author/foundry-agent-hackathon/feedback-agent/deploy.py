"""Deploy the Feedback Agent as a hosted container agent to Foundry.

Builds the container image with ``az acr build`` (server-side, no local Docker),
pushes it to an Azure Container Registry, and creates a new hosted agent version
in the target Microsoft Foundry project via the azure-ai-projects SDK.

The Feedback Agent collects anonymized user telemetry from azure-typespec-author
sessions. This mirrors the sibling ``agent/deploy.py`` but deploys the feedback
agent image/name.

Prerequisites:
  * ``az login`` with access to the subscription/project.
  * An ACR the Foundry project's managed identity can pull from (AcrPull).

Usage:
  python deploy.py \
    --project-endpoint https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default \
    --acr <acrName> \
    --model gpt-5.6-sol
"""

import argparse
import subprocess
import sys
import time
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    AgentProtocol,
    ContainerConfiguration,
    HostedAgentDefinition,
    ProtocolVersionRecord,
)
from azure.identity import AzureCliCredential

_AGENT_DIR = Path(__file__).resolve().parent
_AGENT_NAME = "typespec-authoring-skill-feedback-agent"


def _run(cmd: list[str]) -> None:
    print(f"  $ {' '.join(cmd)}")
    subprocess.run(cmd, check=True)


def _git_short_sha() -> str:
    try:
        r = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True,
        )
        return r.stdout.strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return "latest"


def _wait_active(project: AIProjectClient, name: str, version: str, timeout: int = 600) -> bool:
    print(f"Waiting for {name} v{version} to become active (timeout {timeout}s)...")
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            info = project.agents.get(name)
            latest = info.versions.latest if info.versions else None
            if latest and str(latest.version) == version:
                status = ""
                for attr in ("provisioning_state", "provisioningState", "status"):
                    val = getattr(latest, attr, None)
                    if val:
                        status = str(val).lower().rsplit(".", 1)[-1]
                        break
                if status in ("active", "succeeded"):
                    print(f"  Version status is '{status}'.")
                    return True
                if status in ("failed", "error", "deleted"):
                    print(f"  Version entered terminal state: '{status}'.")
                    return False
                print(f"  Status: {status or 'unknown'} — retrying...")
        except Exception as exc:
            print(f"  WARNING: status check failed: {exc}")
        time.sleep(15)
    print("  Timed out.")
    return False


def main() -> None:
    parser = argparse.ArgumentParser(description="Deploy the hosted Self-Evolving Agent.")
    parser.add_argument("--project-endpoint", required=True)
    parser.add_argument("--acr", required=True, help="ACR name (without .azurecr.io)")
    parser.add_argument("--model", default="gpt-5.4-1", help="Model deployment name")
    parser.add_argument("--tag", default=None, help="Image tag (default: git short SHA)")
    parser.add_argument("--cpu", default="1")
    parser.add_argument("--memory", default="2Gi")
    parser.add_argument("--reasoning-effort", default="medium")
    parser.add_argument(
        "--skip-build",
        action="store_true",
        help="Skip 'az acr build' and deploy an image tag that is already in the registry.",
    )
    parser.add_argument(
        "--no-tracing",
        action="store_true",
        help="Do not inject the project's Application Insights connection string "
        "(disables OpenTelemetry trace/metric export).",
    )
    args = parser.parse_args()

    tag = args.tag or _git_short_sha()
    image = f"{args.acr}.azurecr.io/{_AGENT_NAME}:{tag}"

    if args.skip_build:
        print(f"Skipping build; using existing image: {image}")
    else:
        print(f"Building image: {image}")
        _run([
            "az", "acr", "build",
            "--registry", args.acr,
            "--image", f"{_AGENT_NAME}:{tag}",
            "--file", str(_AGENT_DIR / "Dockerfile"),
            str(_AGENT_DIR),
        ])

    print(f"Deploying to {args.project_endpoint}")
    project = AIProjectClient(
        endpoint=args.project_endpoint,
        credential=AzureCliCredential(),
        allow_preview=True,
    )
    try:
        try:
            existing = project.agents.get(_AGENT_NAME)
            next_version = str(int(existing.versions.latest.version) + 1)
        except Exception:
            next_version = "1"

        env_vars = {
            "AI_FOUNDRY_PROJECT_ENDPOINT": args.project_endpoint,
            "AI_FOUNDRY_AGENT_MODEL": args.model,
            "AI_FOUNDRY_AGENT_REASONING_EFFORT": args.reasoning_effort,
            "APP_VERSION": next_version,
        }

        # Enable tracing: the Foundry platform auto-injects the reserved
        # APPLICATIONINSIGHTS_CONNECTION_STRING env var into the container when an
        # Application Insights connection exists on the project. We only verify the
        # connection here (the env var itself is reserved and cannot be set).
        if not args.no_tracing:
            try:
                project.telemetry.get_application_insights_connection_string()
                print(
                    "  Tracing enabled — project has an Application Insights "
                    "connection (platform injects the connection string)."
                )
            except Exception as exc:
                print(
                    "  WARNING: no Application Insights connection on the project "
                    f"({exc}). Traces will not be exported. Connect Application "
                    "Insights to the project to enable tracing."
                )

        agent = project.agents.create_version(
            agent_name=_AGENT_NAME,
            definition=HostedAgentDefinition(
                cpu=args.cpu,
                memory=args.memory,
                container_configuration=ContainerConfiguration(image=image),
                protocol_versions=[
                    ProtocolVersionRecord(protocol=AgentProtocol.RESPONSES, version="1.0.0")
                ],
                environment_variables=env_vars,
            ),
            metadata={"enableVnextExperience": "true"},
        )
        print(f"Created — agent: {agent.name}, version: {agent.version}")
        if _wait_active(project, agent.name, str(agent.version)):
            print(f"Done — {agent.name} v{agent.version} is active.")
        else:
            sys.exit(f"WARNING: {agent.name} v{agent.version} did not reach active status.")
    finally:
        project.close()


if __name__ == "__main__":
    main()
