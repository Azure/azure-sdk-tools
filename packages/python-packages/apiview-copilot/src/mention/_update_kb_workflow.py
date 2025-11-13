import json
import uuid
import prompty

from azure.cosmos.exceptions import CosmosResourceNotFoundError
from src._database_manager import DatabaseManager
from src._github_manager import GithubManager
from src._models import Example, Guideline, Memory
from src._search_manager import SearchManager
from src._utils import get_prompt_path

from ._base import MentionWorkflow


class UpdateKnowledgeBaseWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_memory.prompty"
    summarize_prompt_file = "summarize_actions.prompty"
    memory_to_github_issue_prompt_file = "parse_memory_to_github_issue.prompty"
    
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

        github_issue_plan = self._generate_issue_plan(memory, guidelines, examples)

        return {"success": success, "failures": failures}

    def _generate_issue_plan(self, memory: Memory, guidelines: list[Guideline], examples: list[Example]):
        """Produce a GitHub issue draft when the reasoning indicates guideline updates are needed."""
        if not self.reasoning:
            raise ValueError("Cannot generate GitHub issue plan without reasoning from parse_conversation_action assigned in self.reasoning")
        
        # if not memory.related_guidelines:
        #     raise RuntimeError("No related guidelines found; cannot generate GitHub issue plan.")

        prompt_path = get_prompt_path(folder="mention", filename=self.memory_to_github_issue_prompt_file)

        prompt_inputs = self._build_issue_prompt_inputs(memory, guidelines, examples)
        try:
            raw_issue = prompty.execute(prompt_path, inputs=prompt_inputs)
        except Exception as exc:
            print(f"Error generating GitHub issue plan: {exc}")
            raise

        return json.loads(raw_issue)

    def _build_issue_prompt_inputs(
        self,
        memory: Memory,
        guidelines: list[Guideline],
        examples: list[Example],
    ) -> dict:
        """Serialize knowledge-base entities into the format expected by the GitHub issue prompt."""

        def dump_model(model):
            return model.model_dump(exclude_none=True)

        return {
            "memory": dump_model(memory),
            "guidelines": [dump_model(g) for g in guidelines],
            "examples": [dump_model(ex) for ex in examples],
            "reasoning": self.reasoning,
        }

    # TODO: Add usage, then replace owner/repo with fork for testing. Then, switch to Azure/azure-rest-api-specs when merged. 
    def _create_guidelines_issue(self, plan: dict):
        """Create a new knowledge base issue on GitHub."""
        client = GithubManager.get_instance()
        issue = client.create_issue(
            owner="Azure",
            repo="azure-rest-api-specs",
            title=plan.get("title"),
            body=plan.get("body"),
        )
        return issue
