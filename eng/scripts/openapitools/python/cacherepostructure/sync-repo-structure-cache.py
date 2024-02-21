from git import Repo
from pymongo import MongoClient
import argparse
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
    print(f"Cloning to [{clone_path}]")
    if not os.path.isdir(clone_path):
        print(f"Cloning repo into {clone_path} ...")
        repo: Repo = Repo.clone_from(repo_url, clone_path, branch=branch)
    else:
        print(f"Repo already cloned, checking out branch: {branch}")
        repo: Repo = Repo(clone_path)
        repo.git.checkout(branch, force=True)

    tree: Repo.tree = repo.tree()
    print("Repo cloned successfully")

    repo_structure: dict = {}
    for blob in tree:
        if blob.name == "specification" and blob.type == "tree":
            repo_structure[blob.name] = get_inner_blobs_structure(blob)

    if "specification" not in repo_structure:
        raise RuntimeError("No 'specification' folder found in the repo.")

    return repo.head.commit.hexsha, repo_structure["specification"]


def update_repo_structure_cache(
    owner: str, repo_name: str, branch: str, github_token: str, repo_clone_path: str
) -> dict:
    new_document = {}
    new_document["updated_on"] = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    new_document["key"] = f"{owner}/{repo_name}/{branch}"

    repo_url: str = f"https://{github_token}@github.com/{owner}/{repo_name}"
    clone_path: str = os.path.join(repo_clone_path, f"{owner}{repo_name}")
    new_document["commitSha"], new_document["repoStructure"] = get_repo_tree(
        repo_url, clone_path, branch
    )

    return new_document


def update_mongo_collection(
    new_document: dict, DB_NAME: str, COLLECTION_NAME: str, CONNECTION_STRING: str
) -> None:
    print("Creating MongoDB client...")
    client = MongoClient(CONNECTION_STRING)
    db = client[DB_NAME]
    collection = db[COLLECTION_NAME]

    print(f"Using database '{DB_NAME}' and collection '{COLLECTION_NAME}'")

    try:
        existing_document: bool = collection.count_documents({"key": new_document["key"]}) == 0
    except Exception as e:
        print(f"Error while checking for existing document: {e}")
        raise e

    print(f"Existing document: {existing_document}")
    # if existing_document:
    #     print(f"Document for '{new_document['key']}' exists, replacing...")
    #     try:
    #         collection.replace_one({"key": new_document['key']}, new_document)
    #     except Exception as e:
    #         print(f"Error while replacing document: {e}")
    #         raise e
    # else: 
    #     print(f"Document does not exist, inserting '{new_document['key']}'...")
    #     try:
    #         collection.insert_one(new_document)
    #     except Exception as e:
    #         print(f"Error while inserting document: {e}")
    #         raise e

    print("\033[92mDocument saved\033[0m")


def main(DB_NAME: str, COLLECTION_NAME: str, REPOS_URL_LIST:str, REPO_CLONE_PATH: str ) -> None:
    GITHUB_TOKEN: str = os.environ.get("GITHUB_TOKEN")
    CONNECTION_STRING: str = os.environ.get("MONGO_CONNECTION_STRING")

    if (
        GITHUB_TOKEN is None
        or REPOS_URL_LIST is None
        or DB_NAME is None
        or COLLECTION_NAME is None
        or CONNECTION_STRING is None
        or REPO_CLONE_PATH is None
    ):
        raise RuntimeError("Environment variables not set")

    repos: List[str] = [repo.strip() for repo in REPOS_URL_LIST.split(",")]

    for url in repos:
        print(f"\nProcessing repo: {url}")
        match = re.search(
            r"https://github\.com/(?P<owner>[^/]*)/(?P<repo_name>[^/]*)/tree/(?P<branch>[^/]*)",
            url,
        )
        print(f"Owner: {match.group('owner')}, Repo: {match.group('repo_name')}, Branch: {match.group('branch')}")
        document = update_repo_structure_cache(
            match.group("owner"),
            match.group("repo_name"),
            match.group("branch"),
            GITHUB_TOKEN,
            REPO_CLONE_PATH,
        )
        update_mongo_collection(document, DB_NAME, COLLECTION_NAME, CONNECTION_STRING)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Updates the repos structure cache.')
    parser.add_argument('--db_name', type=str, required=True, help='Name of the database contining the collection to be updated')
    parser.add_argument('--collection_name', type=str, required=True, help='Name of the collection to be updated')
    parser.add_argument('--repos_url_list', type=str, required=True, help='Comma separated list of repo urls to be updated')
    parser.add_argument('--repo_clone_path', type=str, required=True, help='Path where the repos will be cloned to')

    args = parser.parse_args()
    main(args.db_name, args.collection_name, args.repos_url_list, args.repo_clone_path)
