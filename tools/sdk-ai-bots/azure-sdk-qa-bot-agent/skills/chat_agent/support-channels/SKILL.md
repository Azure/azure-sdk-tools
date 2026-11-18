---
name: support-channels
description: "Look up the right Teams support channel key ONLY when a question remains unresolved and needs another channel's human experts. DO NOT USE FOR: questions you can answer directly (even after loading another domain skill) or while investigating."
---

# Support Channels

Load this skill **only** when a question remains unresolved after investigation and needs human review, approval, escalation, or domain experts in another channel.

- Do not refer the user back to the current channel (see `[tenant_context]` / `[tenant_scope]`).
- For ARM/management-plane reviews, direct to [ARM Office Hours](https://armwiki.azurewebsites.net/introduction/office_hours.html) in your reply instead of routing to `api_stewardship_board`.
- For a Teams channel handoff, append `{{channel:<key>}}` at the end of your response using a key below, and do not write a "Not resolved? Please re-post..." footer yourself. Keep keys internal.

## Channels

{{support_channels}}
