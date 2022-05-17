#!/bin/sh

SPEC_REPO=/spec-repo
WORK_DIR=/work-dir
SDK_REPO=/sdk-repo

if [ -d "${SPEC_REPO}" ]; then
  while true
  do
    if [ -f "/tmp/notExit" ]; then
      USER_GROUP_ID=`stat -c "%u:%g" ${SPEC_REPO}`
      if [ -d "${WORK_DIR}" ]; then
        chown -R ${USER_GROUP_ID} ${WORK_DIR}
      fi
      if [ -d "${SDK_REPO}" ]; then
        chown -R ${USER_GROUP_ID} ${SDK_REPO}
      fi
    fi
    sleep 5s
  done
else
   echo "Error: '${SPEC_REPO}' NOT found."
   exit 1
fi

