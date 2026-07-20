"""Deploy the Self-Evolving Agent as a hosted container agent to Foundry.

Builds the container image with ``az acr build`` (server-side, no local Docker),
pushes it to an Azure Container Registry, and creates a new hosted agent version
in the target Microsoft Foundry project via the azure-ai-projects SDK.

This mirrors azure-sdk-qa-bot-agent/scripts/deploy_hosted_agent.py but takes all
configuration from CLI arguments instead of Azure App Configuration.

Prerequisites:
  * ``az login`` with access to the subscription/project.
  * An ACR the Foundry project's managed identity can pull from (AcrPull).

Usage:
  python deploy.py \
    --project-endpoint https://renhel-demo1-resource.services.ai.azure.com/api/projects/renhel-demo1 \
    --acr <acrName> \
    --model gpt-5.4-1
"""

import argparse
import base64
import subprocess
import os
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
_AGENT_NAME = "typespec-authoring-skill-self-evolving-agent"


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
        "--github-repo",
        default="Azure/azure-sdk-tools",
        help="owner/name of the repo the agent reads and opens PRs against (remote mode).",
    )
    parser.add_argument(
        "--github-token",
        default=None,
        help="GitHub PAT injected into the container so the agent can read the "
        "repo, open PRs, and read CI runs. Defaults to env GITHUB_TOKEN / GH_TOKEN. "
        "Prefer a GitHub App (--github-app-id/...) for automation.",
    )
    parser.add_argument(
        "--github-app-id",
        default=os.environ.get("GITHUB_APP_ID"),
        help="GitHub App ID (App auth). Defaults to env GITHUB_APP_ID.",
    )
    parser.add_argument(
        "--github-app-installation-id",
        default=os.environ.get("GITHUB_APP_INSTALLATION_ID"),
        help="GitHub App installation ID. Defaults to env GITHUB_APP_INSTALLATION_ID.",
    )
    parser.add_argument(
        "--github-app-private-key-file",
        default=None,
        help="Path to the GitHub App private key .pem. Injected base64-encoded. "
        "Defaults to env GITHUB_APP_PRIVATE_KEY / GITHUB_APP_PRIVATE_KEY_BASE64.",
    )
    parser.add_argument(
        "--ado-pat",
        default=None,
        help="Azure DevOps PAT injected as ADO_PAT so the agent can trigger the "
        "code-quality pipeline (8178) in step 3. Defaults to env ADO_PAT / AZURE_DEVOPS_EXT_PAT.",
    )
    parser.add_argument(
        "--ado-token",
        default=None,
        help="Pre-minted AAD bearer token for ADO, injected as ADO_TOKEN (alternative to --ado-pat).",
    )
    parser.add_argument(
        "--ado-token-keyvault-url",
        default=os.environ.get("ADO_TOKEN_KEYVAULT_URL"),
        help="Key Vault URL (e.g. https://<vault>.vault.azure.net/) holding the "
        "'ado-token' secret. Recommended for online/hosted runs: the container's "
        "managed identity reads the token from Key Vault (needs Key Vault get on "
        "the secret) instead of minting it. Defaults to env ADO_TOKEN_KEYVAULT_URL.",
    )
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
            "GITHUB_REPO": args.github_repo,
        }

        # Inject GitHub credentials so the agent can complete its workflow
        # remotely (read the repo, open a PR, trigger + read the skill-eval CI).
        # Two options: a GitHub App (recommended) or a PAT. Without either, the
        # GitHub tools return a clear error instead of crashing.
        github_token = args.github_token or os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
        app_id = args.github_app_id
        app_installation_id = args.github_app_installation_id
        app_key_b64 = os.environ.get("GITHUB_APP_PRIVATE_KEY_BASE64")
        if args.github_app_private_key_file:
            pem = Path(args.github_app_private_key_file).read_text(encoding="utf-8")
            app_key_b64 = base64.b64encode(pem.encode("utf-8")).decode("ascii")
        elif not app_key_b64 and os.environ.get("GITHUB_APP_PRIVATE_KEY"):
            app_key_b64 = base64.b64encode(
                os.environ["GITHUB_APP_PRIVATE_KEY"].encode("utf-8")
            ).decode("ascii")

        if app_id and app_installation_id and app_key_b64:
            env_vars["GITHUB_APP_ID"] = str(app_id)
            env_vars["GITHUB_APP_INSTALLATION_ID"] = str(app_installation_id)
            env_vars["GITHUB_APP_PRIVATE_KEY_BASE64"] = app_key_b64
            print("  GitHub App configured — remote workflow enabled (App installation token).")
        elif github_token:
            env_vars["GITHUB_TOKEN"] = github_token
            print("  GitHub PAT injected — remote workflow enabled.")
        else:
            print(
                "  WARNING: no GitHub credentials (--github-app-* or --github-token / "
                "env GITHUB_TOKEN). The agent cannot read the repo or open PRs remotely "
                "until one is provided."
            )

        # ADO credentials for step 3 (trigger the code-quality pipeline 8178).
        ado_pat = args.ado_pat or os.environ.get("ADO_PAT") or os.environ.get("AZURE_DEVOPS_EXT_PAT")
        ado_token = args.ado_token or os.environ.get("ADO_TOKEN")
        ado_token_kv_url = args.ado_token_keyvault_url
        if ado_pat:
            env_vars["ADO_PAT"] = ado_pat
            print("  ADO PAT injected — step 3 can trigger pipeline 8178.")
        elif ado_token:
            env_vars["ADO_TOKEN"] = ado_token
            print("  ADO bearer token injected — step 3 can trigger pipeline 8178.")
        elif ado_token_kv_url:
            env_vars["ADO_TOKEN_KEYVAULT_URL"] = ado_token_kv_url
            print(
                f"  ADO token Key Vault configured ({ado_token_kv_url}) — the container "
                "MI reads 'ado-token' from Key Vault. Grant the MI Key Vault 'get' on "
                "the secret and keep it fresh (e.g. the qa-bot AdoTokenRefresh function)."
            )
        else:
            print(
                "  NOTE: no ADO credentials (--ado-pat / --ado-token / "
                "--ado-token-keyvault-url). Step 3 will fall back to the container "
                "managed identity, which must be an ADO org member."
            )

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
