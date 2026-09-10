#!/bin/bash
# Backfill release pipeline data for datasets missing release info.
# This script runs all the az devops queries needed and saves raw data.
# After running, use: node scripts/backfill-releases-apply.js
#
# Run from repo root: bash scripts/backfill-releases.sh

set -e
ORG="https://dev.azure.com/azure-sdk"
PRJ="project=internal"
DIR="backfill-release-data"
mkdir -p "$DIR"

az_def() {
  local name="$1" file="$DIR/def-$(echo "$name" | tr ' /' '__').json"
  [ -f "$file" ] && { echo "  (cached) def: $name" >&2; cat "$file"; return; }
  echo "  Lookup def: $name" >&2
  az devops invoke --area build --resource definitions \
    --organization "$ORG" --route-parameters project=internal \
    --query-parameters "name=$name" --output json | jq -c > "$file"
  cat "$file"
}

az_builds() {
  local defId="$1" after="$2" file="$DIR/builds-${defId}-${after:0:10}.json"
  [ -f "$file" ] && { echo "  (cached) builds: $defId" >&2; cat "$file"; return; }
  echo "  Lookup builds: defId=$defId after=$after" >&2
  az devops invoke --area build --resource builds \
    --organization "$ORG" --route-parameters project=internal \
    --query-parameters "definitions=$defId" "reasonFilter=manual" "\$top=20" \
    "queryOrder=finishTimeDescending" "minFinishTime=$after" \
    --output json | jq -c > "$file"
  cat "$file"
}

az_timeline() {
  local buildId="$1" file="$DIR/timeline-${buildId}.json"
  [ -f "$file" ] && { echo "  (cached) timeline: $buildId" >&2; cat "$file"; return; }
  echo "  Lookup timeline: buildId=$buildId" >&2
  az devops invoke --area build --resource timeline \
    --organization "$ORG" --route-parameters project=internal buildId="$buildId" \
    --output json | jq -c > "$file"
  cat "$file"
}

# Service:Language:PipelineNames(|separated):MinDate(earliest merge)
SERVICES=(
  "batch:Java:java - batch:2026-03-18"
  "batch:Go:go - armbatch:2026-03-04"
  "batch:Python:python - batch:2026-02-28"
  "computeschedule:.NET:net - computeschedule - mgmt|net - computeschedule:2026-04-08"
  "confluent:Java:java - confluent:2026-02-24"
  "confluent:Python:python - confluent:2026-03-22"
  "confluent:Go:go - armconfluent:2026-02-24"
  "confluent:JavaScript:js - confluent - mgmt|js - confluent:2026-03-05"
  "confluent:.NET:net - confluent - mgmt|net - confluent:2026-03-12"
  "containerregistry:Java:java - containerregistry:2026-03-23"
  "containerregistry:Go:go - armcontainerregistry:2026-03-20"
  "containerregistry:Python:python - containerregistry:2026-03-20"
  "containerregistry:JavaScript:js - containerregistry - mgmt|js - containerregistry:2026-03-23"
  "containerservice:.NET:net - containerservice - mgmt|net - containerservice:2026-03-26"
  "containerservice:Java:java - containerservice:2026-03-25"
  "containerservice:Go:go - armcontainerservice:2026-03-30"
  "containerservice:Python:python - containerservice:2026-03-16"
  "containerservice:JavaScript:js - containerservice - mgmt|js - containerservice:2026-03-19"
  "keyvault-certificates:JavaScript:js - keyvault-certificates:2026-04-02"
  "servicegroups:Java:java - servicegroups:2026-03-31"
  "servicegroups:Go:go - armservicegroups:2026-03-31"
  "servicegroups:JavaScript:js - servicegroups - mgmt|js - servicegroups:2026-04-01"
)

echo "=== Step 1: Find pipeline definitions ==="
declare -A DEF_IDS
for entry in "${SERVICES[@]}"; do
  IFS=: read -r svc lang names minDate <<< "$entry"
  IFS='|' read -ra name_list <<< "$names"
  for name in "${name_list[@]}"; do
    result=$(az_def "$name")
    count=$(echo "$result" | jq '.value | length')
    if [ "$count" -gt 0 ]; then
      defId=$(echo "$result" | jq '.value[0].id')
      echo "  ✅ $lang/$svc: defId=$defId ($name)"
      DEF_IDS["$svc:$lang"]="$defId:$minDate"
      break
    fi
  done
  [ -z "${DEF_IDS["$svc:$lang"]}" ] && echo "  ❌ $lang/$svc: no pipeline found"
done

echo ""
echo "=== Step 2: Find release builds ==="
declare -A BUILD_IDS
for key in "${!DEF_IDS[@]}"; do
  IFS=: read -r defId minDate <<< "${DEF_IDS[$key]}"
  result=$(az_builds "$defId" "$minDate")
  # Get first succeeded build
  buildId=$(echo "$result" | jq '[.value[] | select(.result=="succeeded")] | .[0].id // empty')
  if [ -n "$buildId" ] && [ "$buildId" != "null" ]; then
    echo "  ✅ $key: build $buildId"
    BUILD_IDS["$key"]="$buildId"
  else
    # Check for any build at all
    anyBuild=$(echo "$result" | jq '.value[0].id // empty')
    if [ -n "$anyBuild" ] && [ "$anyBuild" != "null" ]; then
      anyResult=$(echo "$result" | jq -r '.value[0].result')
      echo "  ⚠️  $key: build $anyBuild ($anyResult)"
      BUILD_IDS["$key"]="$anyBuild"
    else
      echo "  ❌ $key: no release builds found"
    fi
  fi
done

echo ""
echo "=== Step 3: Fetch build timelines ==="
for key in "${!BUILD_IDS[@]}"; do
  buildId="${BUILD_IDS[$key]}"
  result=$(az_timeline "$buildId")
  releaseStage=$(echo "$result" | jq '[.records[] | select(.type=="Stage" and (.name | test("releas";"i")))] | .[0]')
  stageName=$(echo "$releaseStage" | jq -r '.name // "none"')
  stageResult=$(echo "$releaseStage" | jq -r '.result // "none"')
  stageFinish=$(echo "$releaseStage" | jq -r '.finishTime // "none"')
  echo "  $key: build=$buildId stage=$stageName result=$stageResult finish=${stageFinish:0:19}"
done

echo ""
echo "=== Done! Raw data saved in $DIR/ ==="
echo "Run: node scripts/backfill-releases-apply.js"
