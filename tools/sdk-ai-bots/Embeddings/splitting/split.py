from .document import Document
from .chunk import Chunk, ChunkType

from langchain.text_splitter import MarkdownHeaderTextSplitter, RecursiveCharacterTextSplitter
import tiktoken
import re

def _get_headers_to_split_on(heading_level: int) -> list[tuple]:
    headers_to_split_on: list[tuple] = []
    for i in range(1, heading_level + 1):
        headers_to_split_on.append(("#" * i, "#" * i))
    return headers_to_split_on

def _get_heading_title_and_anchor(heading: str) -> tuple[str, str]:
    title = heading.lstrip("#").strip()

    # ### [Data plane](#tab/data-plane)
    tab_pattern = "\[(.*)\]\(#tab/(.*)\)"
    match = re.search(tab_pattern, title)
    if match:
        title = match.group(1)
        anchor = "?tabs={}".format(match.group(2))
        return title, anchor

    # ### <a name="MISSING_RESOURCE_ID" />MISSING_RESOURCE_ID
    atag_pattern = '<a\s+name="(.*)"\s+/>(.*)'
    match = re.search(atag_pattern, title)
    if match:
        title = match.group(2)
        anchor = "#" + match.group(1)
        return title, anchor
    
    # ### <a name="r3021" ></a>R3021 PathResourceTypeNameCamelCase
    atag_pattern = '<a\s+name="(.*)"\s*></a>(.*)'
    match = re.search(atag_pattern, title)
    if match:
        title = match.group(2)
        anchor = "#" + match.group(1)
        return title, anchor

    anchor = "#" + "-".join(re.sub("[`~!@#$%^&*()+=\[\]\{\}\\|:;'\",./<>?]", "", title).lower().split(" "))
    return title, anchor

def split_document(document: Document, heading_level: int, larger_chunk_size: int, smaller_chunk_size: int, overlap_size: int) -> list[Chunk]:
    chunks: list[Chunk] = []
    heading_chunks = split_document_by_markdown_heading(document, heading_level)
    chunks.extend(heading_chunks)
    for heading_chunk in heading_chunks:
        larger_chunks = split_chunk(heading_chunk, larger_chunk_size, overlap_size)
        chunks.extend(larger_chunks)
        for larger_chunk in larger_chunks:
            smaller_chunks = split_chunk(larger_chunk, smaller_chunk_size, overlap_size)
            chunks.extend(smaller_chunks)
    return chunks

def split_document_by_markdown_heading(document: Document, heading_level: int) -> list[Chunk]:
    splitter = MarkdownHeaderTextSplitter(headers_to_split_on=_get_headers_to_split_on(heading_level))
    tokenizer = tiktoken.get_encoding("cl100k_base")
    chunks = splitter.split_text(document.text)
    heading_chunks: list[Chunk] = []
    for i, chunk in enumerate(chunks):
        heading_chunk = Chunk()
        heading_chunk.id = "{}_{}".format(document.id, i)
        heading_chunk.text = chunk.page_content
        tokens = tokenizer.encode(chunk.page_content, disallowed_special=())
        heading_chunk.token_size = len(tokens)
        if len(chunk.metadata) > 0:
            heading_key = sorted(chunk.metadata.keys())[-1]
            heading_chunk.headings = chunk.metadata
            heading_chunk.heading = "{} {}".format(heading_key, chunk.metadata[heading_key])
            title, anchor = _get_heading_title_and_anchor(heading_chunk.heading)
            heading_chunk.title = title
            heading_chunk.link = "{}{}".format(document.link, anchor)
        else:
            heading_chunk.headings = None
            heading_chunk.heading = None
            heading_chunk.title = document.title
            heading_chunk.link = document.link
        heading_chunk.type = ChunkType.HEADING
        heading_chunk.parent_id = None
        heading_chunks.append(heading_chunk)
    return heading_chunks

def split_chunk(larger_chunk: Chunk, chunk_size: int, overlap_size: int) -> list[Chunk]:
    splitter = RecursiveCharacterTextSplitter.from_tiktoken_encoder(chunk_size=chunk_size, chunk_overlap=overlap_size)
    tokenizer = tiktoken.get_encoding("cl100k_base")
    chunks = splitter.split_text(larger_chunk.text)
    smaller_chunks: list[Chunk] = []
    for i, chunk in enumerate(chunks):
        smaller_chunk = Chunk()
        smaller_chunk.id = "{}_{}".format(larger_chunk.id, i)
        smaller_chunk.text = chunk
        tokens = tokenizer.encode(chunk, disallowed_special=())
        smaller_chunk.token_size = len(tokens)
        smaller_chunk.headings = larger_chunk.headings
        smaller_chunk.heading = larger_chunk.heading
        smaller_chunk.title = larger_chunk.title
        smaller_chunk.link = larger_chunk.link
        smaller_chunk.type = ChunkType.LARGER if larger_chunk.type == ChunkType.HEADING else ChunkType.SMALLER
        smaller_chunk.parent_id = larger_chunk.id
        smaller_chunks.append(smaller_chunk)
    return smaller_chunks