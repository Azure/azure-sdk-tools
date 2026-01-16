Structure your response as a JSON object following this exact format:

```json
{
  "has_result": boolean,     // true if you can provide a meaningful answer
  "answer": string,          // your complete response with reasoning and solution, no need to contain the references
  "references": [            // supporting references from the KNOWLEDGE CONTEXT
    {
      "title": string,       // knowledge title
      "source": string,      // knowledge source
      "link": string,        // knowledge link
      "content": string      // knowledge content supporting your answer
    }
  ],
  "reasoning_progress": string // output your reasoning progress of generating the answer
}
```
