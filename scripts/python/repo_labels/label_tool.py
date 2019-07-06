import os
import sys

from github import Github, GithubException

SERVICE_LABEL_COLOR = "e99695"

#
# Help
#


def print_help():
    print("""
Usage: label_tool.py [command] [options]

Commands:
    audit [repo file] - prints out labels from each repo
    push [repo file] [label file] - pushes labels to all repos

Notes:
    Expects environment variable GH_TOKEN to be filled with your 
    access token to GitHub.  This can be generated on GitHub under
    Account->Settings->Developer settings->Personal access tokens.
    """)
    sys.exit()

#
# Push
#


def get_repo(repo_name):
    con = Github(os.environ["GH_TOKEN"])
    repo = con.get_repo(repo_name)
    repo.name  # Force checking if repo exists, otherwise "get_repo" does nothing
    return repo


def create_label(repo, label):
    print(f"Adding label {label}")
    try:
        repo.create_label(label, SERVICE_LABEL_COLOR)
        print(f"+ Created label {label}")
    except GithubException as err:
        err_code = err.data['errors'][0].get('code', '')
        if err.status == 422 and err_code == "already_exists":
            print(f"* Label {label} already exists")
            return
        raise


def push_labels(repo_name, label_list):
    print(f"Getting repo {repo_name}")
    repo = get_repo(repo_name)

    print("Adding labels to repo")
    for label in label_list:
        create_label(repo, label)


def push(repolist_filepath, labellist_filepath):
    print(f"Reading label list from file: {labellist_filepath}")
    with open(labellist_filepath, "r") as lfile:
        label_list = lfile.read().splitlines()

    print(f"Reading repo list from file: {repolist_filepath}")
    with open(repolist_filepath, "r") as rfile:
        for repo_name in rfile.read().splitlines():
            if not repo_name.startswith("//") and not repo_name.startswith("#"):
                push_labels(repo_name, label_list)

#
# Audit
#


def print_labels(repo_name):
    print(f"Printing labels in repo {repo_name}")
    repo = get_repo(repo_name)
    for label in repo.get_labels():
        print(f" {label.name}")


def audit(repolist_filepath):
    print(f"Reading repo list from file: {repolist_filepath}")
    with open(repolist_filepath, "r") as rfile:
        for line in rfile.read().splitlines():
            if not line.startswith("//"):
                print_labels(line)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print_help()

    if sys.argv[1] == "push" and len(sys.argv) == 4:
        push(sys.argv[2], sys.argv[3])
    elif sys.argv[1] == "audit" and len(sys.argv) == 3:
        audit(sys.argv[2])
    else:
        print_help()
