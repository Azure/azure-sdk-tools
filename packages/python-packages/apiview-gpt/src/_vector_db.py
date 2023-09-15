import os
import json
import requests
from typing import List, Union

from ._sectioned_document import SectionedDocument, Section
from ._models import VectorDocument, VectorSearchResult


if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv
    dotenv.load_dotenv()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_BASE_URL = "https://apiviewuat.azurewebsites.net/api/AIComments"
_API_KEY = os.getenv("APIVIEW_API_KEY")

class VectorDB:
    def __init__(self):
        pass

    def _get_headers(self):
        return {
            "Content-Type": "application/json",
            "ApiKey": _API_KEY
        }

    def get_document(self, document_id: str) -> VectorDocument:
        url = f"{_BASE_URL}/{document_id}"
        headers = self._get_headers()
        response = requests.get(url, headers=headers, params={})
        response.raise_for_status()
        return response.json()
    
    def create_document(self, document: VectorDocument) -> VectorDocument:
        url = f"{_BASE_URL}"
        headers = self._get_headers()
        response = requests.post(url, headers=headers, json=document)
        response.raise_for_status()
        return response.json()

    def update_document(self, document_id: str, document: VectorDocument) -> VectorDocument:
        url = f"{_BASE_URL}/{document_id}"
        headers = self._get_headers()
        body = document.to_dict()
        response = requests.post(url, headers=headers, json=body)
        response.raise_for_status()
        json_obj = response.json()
        document = VectorDocument.parse_obj(json_obj)
        return document

    def delete_document(self, document_id: str):
        url = f"{_BASE_URL}/{document_id}"
        headers = self._get_headers()
        response = requests.delete(url, headers=headers)
        response.raise_for_status()
        return

    def search_documents(self, language: str, code: str, threshold: float = 0.5, limit: int = 10) -> List[VectorSearchResult]:
        url = f"{_BASE_URL}/search"
        headers = self._get_headers()
        params = {
            "Language": language,
            "BadCode": code,
            "Threshold": threshold,
            "Limit": limit
        }
        response = requests.get(url, headers=headers, params=params)
        if response.status_code == 404:
            return []
        response.raise_for_status()
        return response.json()
