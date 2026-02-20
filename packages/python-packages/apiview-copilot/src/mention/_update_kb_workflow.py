# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import uuid

from azure.cosmos.exceptions import CosmosResourceNotFoundError
from src._database_manager import DatabaseManager
from src._models import Example, Guideline, Memory
from src._search_manager import SearchManager

from ._base import MentionWorkflow


class UpdateKnowledgeBaseWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_memory.prompty"
    summarize_prompt_file = "summarize_actions.prompty"

    def execute_plan(self, plan: dict):
        db_manager = DatabaseManager.get_instance()
        guideline_ids = plan.get("guideline_ids", [])
        raw_memory = plan.get("memory", {})
        raw_memory["source"] = "mention_agent"
        raw_memory["service"] = None
        raw_examples = raw_memory.pop("related_examples", [])

        memory = Memory(**raw_memory)
        old_memory_id = memory.id
        memory.id = str(uuid.uuid4())
        memory_id = memory.id
        examples = [Example(**ex) for ex in raw_examples]
        for ex in examples:
            ex.service = None
        for example in examples:
            example.id = example.id.replace(old_memory_id, memory_id)
            example.memory_ids = [memory_id]
            memory.related_examples.append(example.id)

        guidelines = []
        for guideline_id in guideline_ids:
            try:
                prefix = "https://azure.github.io/azure-sdk/"
                if guideline_id.startswith(prefix):
                    guideline_id = guideline_id[len(prefix) :]
                guideline = Guideline(**db_manager.guidelines.get(guideline_id))
                guideline.related_memories.append(memory_id)
                memory.related_guidelines.append(guideline_id)
                guidelines.append(guideline)
            except CosmosResourceNotFoundError:
                continue
            except Exception as e:
                print(f"Error retrieving guideline {guideline_id}: {e}")
                continue

        success = []
        failures = {}
        for guideline in guidelines:
            try:
                success.append(db_manager.guidelines.upsert(guideline.id, data=guideline, run_indexer=False))
            except Exception as e:
                print(f"Error updating guideline {guideline.id}: {e}")
                failures[guideline.id] = str(e)
        for example in examples:
            try:
                success.append(db_manager.examples.upsert(example.id, data=example, run_indexer=False))
            except Exception as e:
                print(f"Error updating example {example.id}: {e}")
                failures[example.id] = str(e)
        try:
            success.append(db_manager.memories.upsert(memory.id, data=memory, run_indexer=False))
        except Exception as e:
            print(f"Error updating memory {memory.id}: {e}")
            failures[memory.id] = str(e)
        SearchManager.run_indexers()
        return {"success": success, "failures": failures}
