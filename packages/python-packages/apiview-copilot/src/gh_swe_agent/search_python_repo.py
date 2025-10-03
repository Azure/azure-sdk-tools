import os
import openai
import base64
from azure.core.credentials import AzureKeyCredential
from azure.search.documents import SearchClient
from azure.search.documents.models import Vector

# Configuration
AZURE_SEARCH_ENDPOINT = "https://<your-search-service>.search.windows.net"
AZURE_SEARCH_KEY = "<your-admin-key>"
AZURE_SEARCH_INDEX = "azure-sdk-python"
AZURE_OPENAI_KEY = "<your-openai-key>"
AZURE_OPENAI_ENDPOINT = "https://<your-openai-endpoint>.openai.azure.com"
AZURE_OPENAI_DEPLOYMENT = "<your-deployment-name>"
FOLDER_PATH = "azure-sdk-for-python/sdk/storage"

# Initialize clients
search_client = SearchClient(endpoint=AZURE_SEARCH_ENDPOINT,
                             index_name=AZURE_SEARCH_INDEX,
                             credential=AzureKeyCredential(AZURE_SEARCH_KEY))
openai.api_key = AZURE_OPENAI_KEY
openai.api_base = AZURE_OPENAI_ENDPOINT
openai.api_type = "azure"
openai.api_version = "2023-05-15"

def generate_embedding(text):
    response = openai.Embedding.create(
        input=text,
        engine=AZURE_OPENAI_DEPLOYMENT
    )
    return response['data'][0]['embedding']

def index_folder(folder_path):
    documents = []
    for root, _, files in os.walk(folder_path):
        for file in files:
            if file.endswith(".py") or file.endswith(".md"):
                with open(os.path.join(root, file), "r", encoding="utf-8") as f:
                    content = f.read()
                    embedding = generate_embedding(content)
                    doc_id = base64.urlsafe_b64encode(os.path.join(root, file).encode()).decode()
                    documents.append({
                        "id": doc_id,
                        "content": content,
                        "embedding": embedding
                    })
    result = search_client.upload_documents(documents)
    print(f"Uploaded {len(result)} documents.")

index_folder(FOLDER_PATH)
