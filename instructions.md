## Codex CLI Instructions

There is a tool located in this repo named `test-proxy`. It exists under `tools/test-proxy/Azure.Sdk.Tools.TestProxy` with tests under `tools/test-proxy/Azure.Sdk.Tools.TestProxy.Tests`. The _only_ code changes that are made should be below that directory.

This tool is a reverse-proxy that enables cross-language record-playback of HTTP communication.

First, I want you to evaluate the server codebase to determine if https://github.com/Azure/azure-sdk-tools/issues/3581 this issue is feasible. Within the issue is a link to an MDN article.

Is it _feasible_ to support both websocket and http over the same port? What would that _look_ like? How should we refactor to handle the new traffic?

If not, then we will need to support a DIFFERENT port. And it's merely a feature add.

Do the evaluation first and ask clarifying questions, then add the code changes as a checklist to the bottom of this markdown. After that, I want you to execute on those newly added checklist items.

## WebSocket Support Checklist
- [ ] Add WebSocket proxy middleware + handler to allow HTTP and WebSocket traffic on the same port, with pass-through in record mode and explicit error in playback mode.
- [ ] Extend request-target parsing to accept ws/wss absolute-form and add a helper to map http/https request URIs to ws/wss upstream URIs.
- [ ] Add tests covering ws/wss absolute-form parsing and http/https -> ws/wss mapping.
