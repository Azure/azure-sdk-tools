---
name: support-channels
description: "Look up the right Teams support channel key when a question belongs to another channel's domain (even if answered) so users know where domain experts can help further. DO NOT USE FOR: questions that belong to the current channel."
---

# Support Channels

Load this skill when the user's question belongs to a different domain/channel than the current channel (whether you answered it or it remains unresolved), so the bot can suggest where domain experts can provide a deeper dive.

- Do not refer the user back to the current channel (see `current_channel` in `[tenant_context]`, or `[tenant_scope]` if omitted).
- For a Teams channel handoff, append `{{channel:<key>}}` at the end of your response using a key below, and do not write a "Not resolved? Please re-post..." footer yourself. Keep keys internal.

## Channels

{{support_channels}}
