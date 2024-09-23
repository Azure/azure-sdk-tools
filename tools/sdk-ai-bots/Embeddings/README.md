## How to Run This Tool
1. Run `pip install -r requirements.txt` to install requirements. 
1. Set the environment variables that are defined in `settings/settings.py` in `.env` file.
1. Run `python main.py`.

## Environment Variables
INCREMENTAL_EMBEDDING: the option to build embedding incrementally.
METADATA_PATH: the file path of the metadata file which contains the document URL and title.
DOCUMENT_PATH: the folder path of the document which need to build embeddings.
RAG_CHUNK_PATH: the file path of the RAG chunk file which is the last version or just the file name if it doesn't exist.

AZURE_OPENAI_API_KEY: Azure OpenAI api key
AZURE_OPENAI_ENDPOINT: Azure OpenAI endpoint
AZURE_SEARCH_KEY: Azure search service key
AZURE_SEARCH_ENDPOINT: Azure serach service endpoint
AZURE_SEARCH_INDEX_NAME: Azure serach service index name
AZURE_OPENAI_EMBEDDING_MODEL: the deployed model name in Azure OpenAI service

##### DO NOT CHANGE BELOW VARIABLES' VALUE
AZURESEARCH_FIELDS_CONTENT=Text
AZURESEARCH_FIELDS_CONTENT_VECTOR=Embedding
AZURESEARCH_FIELDS_TAG=AdditionalMetadata
AZURESEARCH_FIELDS_ID=Id
