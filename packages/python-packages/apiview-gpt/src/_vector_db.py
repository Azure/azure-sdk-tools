import os
import dotenv
import json
import json
import requests
from typing import List, Union

from ._sectioned_document import SectionedDocument, Section
from ._models import VectorDocument, VectorSearchResult

dotenv.load_dotenv()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_BASE_URL = "https://apiviewuat.azurewebsites.net"
_API_KEY = os.getenv("APIVIEW_API_KEY")

class VectorDB:
    def __init__(self):
        pass

    def _get_headers(self):
        return {
            #"Content-Type": "application/json",
            "ApiKey": _API_KEY
        }

    def get_document(self, document_id: str) -> VectorDocument:
        url = f"{_BASE_URL}/CopilotComments/GetDocument"
        headers = self._get_headers()
        params = {
            "id": document_id
        }
        response = requests.get(url, headers=headers, params=params)
        response.raise_for_status()
        document = VectorDocument.from_dict(response.json())
        return document
    
    def create_document(self, document: VectorDocument) -> VectorDocument:
        url = f"{_BASE_URL}/CopilotComments/CreateDocument"
        headers = self._get_headers()
        body = document.to_dict()
        response = requests.post(url, headers=headers, json=body)
        response.raise_for_status()
        document = VectorDocument.from_dict(response.json())
        return document

    def update_document(self, document_id: str, document: VectorDocument) -> VectorDocument:
        url = f"{_BASE_URL}/CopilotComments/UpdateDocument"
        headers = self._get_headers()
        body = document.to_dict()
        response = requests.put(url, headers=headers, json=body)
        response.raise_for_status()
        document = VectorDocument.from_dict(response.json())
        return document

    def delete_document(self, document_id: str):
        url = f"{_BASE_URL}/CopilotComments/DeleteDocument"
        headers = self._get_headers()
        params = {
            "id": document_id
        }
        response = requests.delete(url, headers=headers, params=params)
        response.raise_for_status()
        return

    def search_document(self, language: str, code: str, threshold: float, limit: int) -> List[VectorSearchResult]:
        url = f"{_BASE_URL}/CopilotComments/SearchDocument"
        raise NotImplementedError("VectorDB is not implemented yet")
