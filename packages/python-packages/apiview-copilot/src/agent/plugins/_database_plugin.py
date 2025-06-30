import uuid
from typing import Optional
from semantic_kernel.functions import kernel_function

from src._models import Memory
from src._database_manager import get_database_manager


class DatabasePlugin:

    @kernel_function(
        description="Search for Guidelines in the ArchAgent Knowledge Base by programming language (e.g. python, csharp, etc.)."
    )
    async def get_guideline(self, guideline_id: str):
        """
        Retrieve a guideline from the ArchAgent Knowledge Base by its ID.
        Args:
            guideline_id (str): The ID of the guideline to retrieve.
        """
        db = get_database_manager()
        # replace .html# with =html= in guideline_id
        guideline_id = guideline_id.replace(".html#", "=html=")
        guideline = db.guidelines.get(guideline_id)
        # TODO: Expand the guideline into a context object with links resolved
        return guideline

    @kernel_function(description="Add a memory related to a guideline in the ArchAgent Knowledge Base.")
    async def add_guideline_memory(
        self,
        title: str,
        content: str,
        guideline_id: str,
        is_exception: bool,
        service_name: Optional[str],
        language: Optional[str],
    ):
        """
        Add a memory related to a guideline in the ArchAgent Knowledge Base.
        Args:
            guideline_id (str): The ID of the guideline to which the memory is related.
            content (str): The memory content to add.
            title (str): The title of the memory.
            is_exception (bool): Whether the memory is an exception to established guidelines.
            service_name (str): The service related to the memory, if any.
            language (str): The programming language of the memory, if any.
        """
        if not language:
            return {"status": "error", "message": "Language must be specified."}

        db = get_database_manager()
        # replace .html# with =html= in guideline_id
        guideline_id = guideline_id.replace(".html#", "=html=")
        guideline = db.guidelines.get(guideline_id)
        memory_id = str(uuid.uuid4())
        memory = Memory(
            id=memory_id,
            related_guidelines=[guideline_id],
            title=title,
            content=content,
            source="agent",
            is_exception=is_exception,
            service=service_name,
            language=language,
        )
        related_memories = guideline.get("related_memories", [])
        related_memories.append(memory_id)
        guideline["related_memories"] = related_memories
        db.guidelines.upsert(guideline_id, guideline)
        db.memories.upsert(memory_id, memory)
        return {"status": "Memory added successfully"}
