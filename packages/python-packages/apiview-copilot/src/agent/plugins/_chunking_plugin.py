from semantic_kernel.functions import kernel_function
from src._sectioned_document import SectionedDocument
from semantic_kernel.functions import KernelPlugin


class ChunkingPlugin(KernelPlugin):

    def __init__(self):
        super().__init__(name="ChunkingPlugin", description="A plugin for chunking code into manageable sections.")

    @kernel_function(description="Splits code into chunks using a SectionedDocument.")
    def chunk_code(self, code: str, chunk_size: int = 250) -> SectionedDocument:
        """
        Splits the code into a SectionedDocument using the provided chunk size.
        """
        lines = code.splitlines()
        return SectionedDocument(lines=lines, max_chunk_size=chunk_size)
