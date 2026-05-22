Determine whether the user's message requires RAG (Retrieval Augmented Generation) processing.

**Return `false` for:**
- Non-question messages which do not seek information or assistance
-  Messages that share ideas, proposals, feature requests, exploration of future possibilities, or roadmap plans — even if the content is highly technical. These messages describe what the user *wants to do* or *plans to explore*, rather than asking *how to do* something. Key signals include phrases like "I'd like to explore", "I'd like to propose", "I want to share", "here's an idea", "it would be great to", or linking to external roadmaps, issues, or wishlists.
- Greetings or thanks messages
- Suggestions or questions about the Azure SDK Q&A bot itself
- Announcements and informational broadcasts: Messages that share information, updates, release schedules, or procedural information rather than seeking help

**Return `true` for:**
- Technical questions that ask for help, guidance, or best practices
- Permission-related questions
- Questions *about* the PR review or approval process (e.g., "How do I get my PR approved?")
- All other cases not explicitly listed above

**Important distinction for PR reviews:**
- Asking *for* a review (e.g., "Please review my PR") → return `false` (this is a request/announcement, not a question)
- Asking *about* the review process (e.g., "What are the steps to get a PR approved?") → return `true` (this is a technical question)