#!/bin/sh

SPEC_REPO=/spec-repo
WORK_DIR=/work-dir
SDK_REPO=/sdk-repo

while [ ! -d "${SPEC_REPO}" ]; do
    sleep 10
done

if [ -d "${SPEC_REPO}" ]; then
  while true
  do
    USER_GROUP_ID=`stat -c "%u:%g" ${SPEC_REPO}`
    if [ -d "${WORK_DIR}" ]; then
      chown -R ${USER_GROUP_ID} ${WORK_DIR} > /dev/null 2>&1
    fi
    if [ -d "${SDK_REPO}" ]; then
      chown -R ${USER_GROUP_ID} ${SDK_REPO} > /dev/null 2>&1
    fi
    sleep 5s
  done
else
   echo "Error: '${SPEC_REPO}' NOT found."
   exit 1
fi

