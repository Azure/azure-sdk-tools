# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import json
import sys

import matplotlib.pyplot as plt
import openai
import pandas as pd
import prompty
import prompty.azure
import umap
from sentence_transformers import SentenceTransformer
from sklearn.cluster import KMeans
from sklearn.metrics import silhouette_score
from sklearn.metrics.pairwise import cosine_similarity
from src._github_manager import GithubManager
from src._utils import get_prompt_path


def analyze_issues():
    """
    Analyze known APIView issues from GitHub, cluster them, detect redundancies,
    and generate summaries using OpenAI.
    """
    # TODO: Restore this after testing
    # # -------------------------------
    # # Step 1: Fetch filtered issues
    # # -------------------------------
    # gm = GithubManager.get_instance()
    # issues = gm.search_issues(
    #     owner="Azure",
    #     repo="azure-sdk-tools",
    #     query="-label:Javascript -label:Python -label:Go -label:Swift -label:Java "
    #     "-label:.NET -label:Rust -label:TypeSpec -label:C++ -label:Swagger "
    #     "-label:C99 -label:Epic label:APIView is:open",
    #     max_results=300,
    # )

    # # TODO: Remove this dump after testing
    # with open("apiview_issues_dump.json", "w", encoding="utf-8") as f:
    #     json.dump(issues, f, ensure_ascii=False, indent=2)

    # TODO: Remove this after testing
    with open("apiview_issues_dump.json", "r", encoding="utf-8") as f:
        issues = json.load(f)

    data = []
    for issue in issues:
        text = issue["title"] + " " + (issue.get("body") or "")
        data.append({"id": issue["number"], "text": text})
    df = pd.DataFrame(data)

    # -------------------------------
    # Step 2: Embeddings
    # -------------------------------
    model = SentenceTransformer("all-MiniLM-L6-v2")
    embeddings = model.encode(df["text"].tolist(), show_progress_bar=True)

    # -------------------------------
    # Step 3: Clustering (KMeans, silhouette score sweep)
    # -------------------------------
    best_n = 2
    best_score = -1
    for n_clusters in range(2, 15):
        kmeans = KMeans(n_clusters=n_clusters, random_state=42)
        labels = kmeans.fit_predict(embeddings)
        score = silhouette_score(embeddings, labels)
        if score > best_score:
            best_score = score
            best_n = n_clusters

    print(f"\nOptimal number of clusters: {best_n} with silhouette score: {best_score:.4f}")
    kmeans = KMeans(n_clusters=best_n, random_state=42)
    labels = kmeans.fit_predict(embeddings)
    df["cluster"] = labels

    # -------------------------------
    # Step 4: Visualization
    # -------------------------------
    reducer = umap.UMAP(random_state=42)
    embedding_2d = reducer.fit_transform(embeddings)

    plt.figure(figsize=(10, 7))
    plt.scatter(embedding_2d[:, 0], embedding_2d[:, 1], c=df["cluster"], cmap="tab10")
    plt.title("Filtered GitHub Issues Clustering")
    plt.show()

    # -------------------------------
    # Step 5: LLM Summaries (using prompty.execute with summarize_issue_cluster)
    # -------------------------------

    cluster_summaries = {}
    prompt_path = get_prompt_path(folder="other", filename="summarize_issue_cluster")
    for cluster_id in sorted(df["cluster"].unique()):
        cluster_id_py = int(cluster_id)  # Ensure key is a Python int, not numpy.int32
        issues = df[df["cluster"] == cluster_id]["text"].tolist()
        inputs = {"cluster_id": str(cluster_id_py), "issues": "\n".join(f"- {t}" for t in issues)}
        summary = prompty.execute(prompt_path, inputs=inputs)
        label = None
        summary_text = ""
        try:
            summary_obj = json.loads(summary)
            if isinstance(summary_obj, dict):
                label = summary_obj.get("label")
                summary_text = summary_obj.get("summary", "")
        except Exception:
            pass
        cluster_summaries[cluster_id_py] = {"label": label, "summary": summary_text}

    df["cluster_summary"] = df["cluster"].map(
        lambda cid: cluster_summaries[int(cid)]["summary"] if int(cid) in cluster_summaries else ""
    )

    # Create a dictionary of the output of each cluster, with the label, the summary, and the issue links
    clusters_output = {}
    for cluster_id in sorted(df["cluster"].unique()):
        cluster_id_py = int(cluster_id)
        issues_in_cluster = df[df["cluster"] == cluster_id]
        # Try to get label and summary from the LLM output if possible
        summary = cluster_summaries.get(cluster_id_py, "")
        # If the summary is a JSON string with label/summary, parse it
        label = None
        try:
            summary_obj = json.loads(summary) if isinstance(summary, str) else summary
            if isinstance(summary_obj, dict):
                label = summary_obj.get("label")
                summary_text = summary_obj.get("summary", "")
            else:
                summary_text = summary
        except Exception:
            summary_text = summary
        # Build issue links (assuming GitHub Azure/azure-sdk-tools repo)
        issue_links = [
            f"https://github.com/Azure/azure-sdk-tools/issues/{row['id']}" for _, row in issues_in_cluster.iterrows()
        ]
        clusters_output[cluster_id_py] = {
            "label": label,
            "summary": summary_text,
            "issues": issue_links,
        }
    # Dump to a file
    with open("clusters_output.json", "w", encoding="utf-8") as f:
        json.dump(clusters_output, f, ensure_ascii=False, indent=2)
