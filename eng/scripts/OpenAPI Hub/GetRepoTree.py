from git import Repo
from pymongo import MongoClient
from typing import List
import datetime
import os
import re


def get_inner_blobs_structure(blob: Repo.tree) -> dict:
    inner_structure: dict = {}
    for inner_blob in blob:
        if inner_blob.type == "tree":
            inner_structure[inner_blob.name] = get_inner_blobs_structure(inner_blob)
    return inner_structure


def get_repo_tree(repo_url: str, clone_path: str, branch: str) -> [str, dict]:
    print("Cloning to [{}]".format(clone_path))
    if not os.path.isdir(clone_path):
        print("Cloning repo into {} ...".format(clone_path))
        repo: Repo = Repo.clone_from(repo_url, clone_path, branch=branch)
    else:
        print("Repo already cloned, checking out branch: {}".format(branch))
        repo: Repo = Repo(clone_path)
        repo.git.checkout(branch, force=True)

    tree: Repo.tree = repo.tree()
    print("Repo cloned successfully")

    repo_structure: dict = {}
    for blob in tree:
        if blob.name == "specification" and blob.type == "tree":
            repo_structure[blob.name] = get_inner_blobs_structure(blob)

    if "specification" not in repo_structure:
        raise Exception("No 'specification' folder found in the repo.")

    return repo.head.commit.hexsha, repo_structure["specification"]


def update_repo_structure_cache(
    owner: str, repo_name: str, branch: str, github_token: str, repo_clone_path: str
) -> dict:
    new_document = {}
    new_document["updated_on"] = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    new_document["key"] = "{}/{}/{}".format(owner, repo_name, branch)

    repo_url: str = "https://{}@github.com/{}/{}".format(github_token, owner, repo_name)
    clone_path: str = os.path.join(repo_clone_path, "{}{}".format(owner, repo_name))
    new_document["commitSha"], new_document["repoStructure"] = get_repo_tree(
        repo_url, clone_path, branch
    )

    return new_document


def mongo_operations(
    new_document: dict, DB_NAME: str, COLLECTION_NAME: str, CONNECTION_STRING: str
) -> None:
    print("Creating MongoDB client...")
    client = MongoClient(CONNECTION_STRING)
    db = client[DB_NAME]
    collection = db[COLLECTION_NAME]

    print("Using database '{}' and collection '{}'".format(DB_NAME, COLLECTION_NAME))

    if collection.count_documents({"key": new_document["key"]}) == 0:
        print("Document does not exist, inserting '{}'...".format(new_document["key"]))
        collection.insert_one(new_document)
    else:
        print("Document for '{}' exists, replacing...".format(new_document["key"]))
        collection.replace_one({"key": new_document["key"]}, new_document)

    print("\033[92mDocument saved\033[0m")


def main() -> None:
    GITHUB_TOKEN: str = os.environ.get("GITHUB_TOKEN")
    REPOS_URL_LIST: str = os.environ.get("REPOS_URL_LIST")
    DB_NAME: str = os.environ.get("DB_NAME")
    COLLECTION_NAME: str = os.environ.get("COLLECTION_NAME")
    CONNECTION_STRING: str = os.environ.get("MONGO_CONNECTION_STRING")
    REPO_CLONE_PATH: str = os.getenv("REPO__CLONE_PATH")

    if (
        GITHUB_TOKEN is None
        or REPOS_URL_LIST is None
        or DB_NAME is None
        or COLLECTION_NAME is None
        or CONNECTION_STRING is None
        or REPO_CLONE_PATH is None
    ):
        raise Exception("Environment variables not set")

    repos: List[str] = REPOS_URL_LIST.split(",")

    for url in repos:
        print("\nProcessing repo: {}".format(url))
        match = re.search(
            r"https://github\.com/(?P<owner>[^/]*)/(?P<repo_name>[^/]*)/tree/(?P<branch>[^/]*)",
            url,
        )
        print(
            "Owner: {}, Repo: {}, Branch: {}".format(
                match.group("owner"), match.group("repo_name"), match.group("branch")
            )
        )
        document = update_repo_structure_cache(
            match.group("owner"),
            match.group("repo_name"),
            match.group("branch"),
            GITHUB_TOKEN,
            REPO_CLONE_PATH,
        )
        mongo_operations(document, DB_NAME, COLLECTION_NAME, CONNECTION_STRING)


main()
