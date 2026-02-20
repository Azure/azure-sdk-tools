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
- PR review request or approval process questions
- All other cases not explicitly listed above