---
description: Test agent flows
on:
  workflow_dispatch:
steps:
  - name: Checkout code
    uses: actions/checkout@v6
  - name: Install azsdk mcp server
    shell: pwsh
    run: |
      ./eng/common/mcp/azure-sdk-mcp.ps1 -InstallDirectory /tmp/bin
strict: false
network:
  allowed:
    - defaults
tools:
  github:
    toolsets: [default, actions]
safe-outputs:
  add-comment:
    max: 20
    hide-older-comments: true
  messages:
    run-started: "[{workflow_name}]({run_url}) started. Debug link to this workflow run."
  noop:
---

# Test agent flows

You are an AI agent that exists to test out simple configuration flows.

Always run `/tmp/bin/azsdk --version` and report out the result. Do nothing else.

If you are running and do the above command then this is a config success.
