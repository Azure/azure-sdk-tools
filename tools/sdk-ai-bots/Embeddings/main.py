from splitting.chunk import RagChunk
from splitting.document import Document
from splitting.split import split_document
from splitting.utils import *
from settings.settings import *
from embedding.embedding import Embedding

def main():
    print("incremental embedding:", INCREMENTAL_EMBEDDING)

    metadata = read_metadata()
    current_rag_chunks = read_rag_chunks() if INCREMENTAL_EMBEDDING else None
    documents: list[Document] = read_documents(metadata)

    print("Splitting {} documents into chunks...".format(len(documents)))
    rag_chunks: list[RagChunk] = []
    for document in documents:
        chunks = split_document(document, HEADING_LEVEL, LARGER_CHUNK_SIZE, SMALLER_CHUNK_SIZE, OVERLAP_SIZE)
        rag_chunks.extend(document_chunks_to_rag_chunks(document, chunks))

        if CHUNK_PATH is not None:
            save_document_chunks(document, chunks)
    print("Done splitting documents into {} chunks.".format(len(rag_chunks)))

    embedding = Embedding()
    if INCREMENTAL_EMBEDDING:
        document_chunks_dict = dict[str, list[RagChunk]]()
        for rag_chunk in current_rag_chunks:
            doc_id = "_".join(rag_chunk.id.split("_")[:-3])
            if doc_id not in document_chunks_dict:
                document_chunks_dict[doc_id] = []
            document_chunks_dict[doc_id].append(rag_chunk)
        
        rag_chunks_to_delete: set[str] = set()
        for doc_id in metadata:
            if doc_id in document_chunks_dict:
                for rag_chunk in document_chunks_dict[doc_id]:
                    rag_chunks_to_delete.add(rag_chunk.id)

        rag_chunks_to_retain: list[RagChunk] = []
        for rag_chunk in current_rag_chunks:
            if rag_chunk.id not in rag_chunks_to_delete:
                rag_chunks_to_retain.append(rag_chunk)

        embedding.add_incremental_rag_chunks(rag_chunks_to_delete, rag_chunks)
        rag_chunks = rag_chunks_to_retain + rag_chunks
    else:
        embedding.add_rag_chunks(rag_chunks)
    
    save_rag_chunks(rag_chunks)

if __name__ == "__main__":
    main()