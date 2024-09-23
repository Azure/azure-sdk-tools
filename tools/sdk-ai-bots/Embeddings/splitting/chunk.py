from enum import Enum

class ChunkType(Enum):
    HEADING = "heading"
    LARGER = "larger"
    SMALLER = "smaller"

class Chunk:
    id: str
    text: str
    token_size: int
    headings: dict[str, str]
    heading: str
    title: str # fallback to document title if heading is None
    link: str # fallback to document link if heading is None
    type: ChunkType
    parent_id: str

class RagTextType(Enum):
    HEADING = "heading"
    LARGER = "larger"

class RagChunk:
    id: str
    text: str
    document_title: str
    document_link: str
    heading_title: str # fallback to document title if heading is None
    heading_link: str # fallback to document link if heading is None
    rag_text: str
    rag_text_type: RagTextType