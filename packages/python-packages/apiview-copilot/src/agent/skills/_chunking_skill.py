from semantic_kernel.functions import sk_function
from src._sectioned_document import SectionedDocument


@sk_function(description="Splits code into chunks using a SectionedDocument.")
def chunk_code(code: str, chunk_size: int = 250) -> SectionedDocument:
    """
    Splits the code into a SectionedDocument using the provided chunk size.
    """
    lines = code.splitlines()
    return SectionedDocument(lines=lines, max_chunk_size=chunk_size)
