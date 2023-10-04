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

    """
    Splits a class into sections to be queried individually. Will include the
    class defintion, properties and method signatures.
    """
    def create_line_sections(self, section: Section) -> List[str]:
        sectioned = []
        current_section = []
        for line in section.lines:
            if line == "" and not current_section:
                continue
            elif line == "" and current_section:
                sectioned.append("\n".join(current_section))
                current_section = []
            else:
                current_section.append(line)
        return sectioned

    """
    A heuristic that breaks a code block into constituent parts when similarity search fails to find a match.
    """
    def search_documents_by_line(self, language: str, code: str, threshold: float, limit: int) -> List[VectorSearchResult]:
        section = SectionedDocument(code.splitlines(), chunk=True).sections[0]
        line_sections = self.create_line_sections(section)

        results = []
        for section in line_sections:
            results.extend(self.search_documents(language, section, threshold, limit))
        return results

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
            results = self.search_documents_by_line(language, code, threshold, limit)
        else:
            response.raise_for_status()
            results = response.json()
        
        # eliminate duplicates with the same similarity and bad_code
        seen = set()
        results = [x for x in results if not (x["similarity"], x["aiCommentModel"]["badCode"]) in seen and not seen.add((x["similarity"], x["aiCommentModel"]["badCode"]))]
        results.sort(key=lambda x: x["similarity"], reverse=True)
        return results
