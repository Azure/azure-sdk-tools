from semantic_kernel.agents import AzureAIAgentThread


async def run_agent_chat(agent, user_input, thread_id=None, messages=None):
    messages = messages or []
    # Only append user_input if not already the last message
    if not messages or messages[-1] != user_input:
        messages.append(user_input)
    # Only use thread_id if it is a valid Azure thread id (starts with 'thread')
    if thread_id and isinstance(thread_id, str) and thread_id.startswith("thread"):
        thread = AzureAIAgentThread(client=agent.client, thread_id=thread_id)
    else:
        thread = AzureAIAgentThread(client=agent.client)
    response = await agent.get_response(messages=messages, thread=thread)
    thread_id_out = getattr(thread, "id", None) or thread_id
    return str(response), thread_id_out, messages
