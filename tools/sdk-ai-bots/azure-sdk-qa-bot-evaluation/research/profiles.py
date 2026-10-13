import json
import re


PROFILES = {
    "baseline": "",
    "c1_facts": "",
    "c2_typed_evidence": "",
    "c3_witness_policy": (
        "Before recommending an action, distinguish observed facts, the exact governed "
        "repository/branch/path/version, applicable policy, and reviewer authorization. "
        "Identify the decisive evidence that licenses the recommendation. A configuration "
        "or schedule is not execution evidence or approval. If missing evidence would "
        "change the action, ask for that specific evidence rather than assume it. "
        "Answer supported questions directly; do not ask for irrelevant context. "
        "Keep the final answer concise and distinguish facts from conditional advice."
    ),
    "c4_primary_sources": (
        "Use the supplied primary code/configuration to establish mechanics for the exact "
        "object in the question. Distinguish separate sync stages and exact branch names. "
        "Retrieved guidance can describe policy, but source mechanics do not grant approval. "
        "Do not convert an event timestamp into an inclusion cutoff without evidence "
        "that the source revision was captured at that event."
    ),
    "c5_counterexample_policy": (
        "Before finalizing an operational recommendation, check whether it could fail "
        "while all cited evidence remains true. In particular, distinguish configuration "
        "from an actual execution, and a timestamp from the event that captures a revision. "
        "If a plausible counterexample remains, state the condition that controls the "
        "outcome and the decisive evidence needed; do not give an unsupported default "
        "action or deadline. Apply this check to advice as well as factual claims. "
        "Do not disclose internal reasoning; give a short supported answer."
    ),
}


def matches(query, tenant="api_spec_review_bot"):
    return (
        tenant == "api_spec_review_bot"
        and bool(re.search(
            r"\b(main|rpsaasmaster|rpsaasdev|armcorerpdev|internalarmcontracts)\b",
            query, re.IGNORECASE,
        ))
        and bool(re.search(
            r"\b(sync\w*|mirror\w*)\b|merge.?backs?|merge process", query, re.IGNORECASE,
        ))
    )


def source_card(snapshot):
    contract = snapshot["contract"]
    targets = ", ".join(contract["target_branches"])
    return (
        "Verified read-only source/configuration snapshot, captured "
        f"{snapshot['captured_utc']}. This is not a run record, reviewer authorization, "
        "or a rule for arbitrary manual commands or other branch spellings.\n"
        f"Public SDK-tools source commit: {snapshot['source_revision']}.\n"
        "Pipeline 1752 definition revision 21: daily 17:00 Pacific Time, Windows zone "
        "'Pacific Standard Time', observes Pacific DST, scheduleOnlyWithChanges=false.\n"
        "Public main -> private main is whole-branch sync with no service-spec path exclusion.\n"
        f"The separate private main -> {targets} merge uses Theirs **, Ours specification, "
        "Merge specification/common-types and AcceptTheirsForFinalMerge=true. It preserves "
        "ordinary service spec content and private-only service files; infrastructure and "
        "specification/common-types take source content. Exact configured branch names matter.\n"
        "No reverse private-branch -> public-main publication is defined by this configuration.\n"
        "Git-Repo-Sync fetches Source $SourceBranch inside execution. Neither queue time, "
        "scheduled time NOR RUN START TIME establishes the fetched source revision. A PR "
        "merged after a run starts but before source fetch can still be captured. A PR "
        "merged after source fetch is not part of that fetched snapshot. Do not advise "
        "'after run start means tomorrow'; inclusion depends on the actual source fetch "
        "and resulting target commit/content. A schedule does not prove success, inclusion, "
        "or completion time. No fixed-duration SLA follows from the schedule.\n"
        "Sources:\n" + "\n".join(source["url"] for source in contract["source_files"])
        + "\nhttps://dev.azure.com/azure-sdk/internal/_build?definitionId=1752"
    )


def source_content(profile, snapshot):
    if profile == "baseline":
        return ""
    if profile == "c2_typed_evidence":
        contract = snapshot["contract"]
        return json.dumps({
            "kind": "verified_read_only_evidence_not_authorization",
            "captured_utc": snapshot["captured_utc"],
            "source_revision": snapshot["source_revision"],
            "pipeline_definition": {
                "id": 1752, "revision": 21, "schedule": contract["schedule"],
            },
            "stage_1": {
                "from": "public/main", "to": "private/main",
                "service_spec_exclusion": False,
            },
            "stage_2": {
                "from": "private/main", "exact_targets": contract["target_branches"],
                "ordinary_service_specs": "preserved",
                "common_types_and_infrastructure": "take source content",
            },
            "reverse_publication": "not defined",
            "revision_capture_event": "source fetch inside execution",
            "not_revision_capture_evidence": [
                "scheduled time", "queue time", "run start time",
            ],
            "inclusion_witness": [
                "actual fetched source revision", "resulting target commit/content",
            ],
            "successful_completion_witness": "actual run result",
            "manual_operation_applicability": "not established",
            "reviewer_approval": "not established",
            "sources": contract["source_files"],
        }, indent=2)
    if profile == "c4_primary_sources":
        return (
            "Read-only primary source snapshot, not execution evidence or approval.\n"
            + json.dumps(snapshot["contract"], indent=2)
            + "\n\n" + "\n\n".join(
                f"Source: {source['url']}\n{snapshot['texts'][source['path']]}"
                for source in snapshot["contract"]["source_files"]
            )
        )
    return source_card(snapshot)
