from .document import Document
from .chunk import Chunk, ChunkType, RagChunk, RagTextType
from settings.settings import *

import json

def read_metadata() -> dict:
    with open(METADATA_PATH, "r") as file:
        return json.load(file)

def read_document(document_path: str) -> str:
    with open(document_path, "r", encoding="utf-8") as file:
        return file.read()

def read_documents(metadata: dict) -> list[Document]:
    documents: list[Document] = []
    for filename in os.listdir(DOCUMENT_PATH):
        if filename not in metadata:
            print("WARN: document {} not in metadata".format(filename))
            continue
        document_path = os.path.join(DOCUMENT_PATH, filename)
        content = read_document(document_path)
        document = Document()
        document.id = filename
        document.text = content
        document.title = metadata[filename]["title"]
        document.link = metadata[filename]["url"]
        documents.append(document)
    return documents

def read_rag_chunks() -> list[RagChunk]:
    with open(RAG_CHUNK_PATH, "r") as file:
        data = json.load(file)
        return [json_to_rag_chunk(obj) for obj in data["chunks"]]
    
def document_chunks_to_json(document: Document, chunks: list[Chunk]):
    return {
        "id": document.id,
        "title": document.title,
        "link": document.link,
        "chunks": [
            {
                "id": chunk.id,
                "text": chunk.text,
                "tokenSize": chunk.token_size,
                "type": chunk.type.value,
                "parentId": chunk.parent_id,
                "heading": chunk.heading,
                "title": chunk.title,
                "link": chunk.link,
                "headings": chunk.headings
            }
            for chunk in chunks
        ]
    }

def document_chunks_to_rag_chunks(document: Document, chunks: list[Chunk]) -> list[RagChunk]:
    chunks_dict = {chunk.id: chunk for chunk in chunks}
    rag_chunks: list[RagChunk] = []
    for chunk in chunks:
        if chunk.type == ChunkType.SMALLER:
            rag_chunk = RagChunk()
            rag_chunk.id = chunk.id
            rag_chunk.text = chunk.text
            rag_chunk.document_title = document.title
            rag_chunk.document_link = document.link
            rag_chunk.heading_title = chunk.title
            rag_chunk.heading_link = chunk.link
            heading_chunk_text = chunks_dict[chunks_dict[chunk.parent_id].parent_id].text
            heading_chunk_token_size = chunks_dict[chunks_dict[chunk.parent_id].parent_id].token_size
            larger_chunk_text = chunks_dict[chunk.parent_id].text
            if heading_chunk_token_size <= LARGER_CHUNK_SIZE:
                rag_chunk.rag_text_type = RagTextType.HEADING
                rag_chunk.rag_text = "{}{}".format(chunk.heading + "\n\n" if chunk.heading else "", heading_chunk_text)
            else:
                rag_chunk.rag_text_type = RagTextType.LARGER
                rag_chunk.rag_text = larger_chunk_text
            rag_chunks.append(rag_chunk)
    return rag_chunks

def rag_chunk_to_json(rag_chunk: RagChunk) -> dict:
    return {
        "id": rag_chunk.id,
        "text": rag_chunk.text,
        "documentTitle": rag_chunk.document_title,
        "documentLink": rag_chunk.document_link,
        "headingTitle": rag_chunk.heading_title,
        "headingLink": rag_chunk.heading_link,
        "ragText": rag_chunk.rag_text,
        "ragTextType": rag_chunk.rag_text_type.value
    }

def json_to_rag_chunk(obj: dict) -> RagChunk:
    rag_chunk = RagChunk()
    rag_chunk.id = obj["id"]
    rag_chunk.text = obj["text"]
    rag_chunk.document_title = obj["documentTitle"]
    rag_chunk.document_link = obj["documentLink"]
    rag_chunk.heading_title = obj["headingTitle"]
    rag_chunk.heading_link = obj["headingLink"]
    rag_chunk.rag_text = obj["ragText"]
    rag_chunk.rag_text_type = RagTextType(obj["ragTextType"])
    return rag_chunk

def rag_chunks_to_json(rag_chunks: list[RagChunk]) -> dict:
    return {
        "chunks": [rag_chunk_to_json(rag_chunk) for rag_chunk in rag_chunks]
    }

def save_document_chunks(document: Document, chunks: list[Chunk]):
    filename = document.id
    chunk_path = os.path.join(CHUNK_PATH, "{}.json".format(os.path.splitext(filename)[0]))
    with open(chunk_path, "w") as file:
        json.dump(document_chunks_to_json(document, chunks), file, indent=4)

def save_rag_chunks(rag_chunks: list[RagChunk]):
    with open(RAG_CHUNK_PATH, "w") as file:
        json.dump(rag_chunks_to_json(rag_chunks), file, indent=4)