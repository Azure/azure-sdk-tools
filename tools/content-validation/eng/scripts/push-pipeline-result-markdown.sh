#!/bin/bash
set -e

# Setting Variables
GITHUB_PAT=$1
REPO_OWNER=$2
REPO_NAME=$3
GIT_USER_NAME=$4
GIT_USER_EMAIL=$5
LANGUAGE=$6

to_lowercase() {
    local input="$1"
    echo "$input" | tr '[:upper:]' '[:lower:]'
}

language=$(to_lowercase "$LANGUAGE")

REPO_URL="https://github.com/${REPO_OWNER}/${REPO_NAME}.git"
CLONE_DIR="./repo-clone"
BRANCH="latest-pipeline-result"
FILE_NAME="latest-pipeline-result-for-${language}.md"

git clone "https://${GITHUB_PAT}@github.com/${REPO_OWNER}/${REPO_NAME}.git" $CLONE_DIR
cd $CLONE_DIR
git checkout $BRANCH
git pull origin $BRANCH
cp -f ../$FILE_NAME .
git config --global user.email "${GIT_USER_EMAIL}"
git config --global user.name "${GIT_USER_NAME}"

git add $FILE_NAME
git commit -m "Updating the latest pipeline result"
git push origin $BRANCH