from collections import OrderedDict
import json
import os
from pprint import pprint
import sys
from typing import List

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from src import VectorDB, VectorDocument


def get_document(document_id: str):
    """
    Retrieve a document by ID
    """
    db = VectorDB()
    pprint(db.get_document(document_id))

def create_document(path: str):
    """
    Create a new document
    """
    db = VectorDB()
    # resolve full path
    if not path.startswith("/"):
        path = f"{os.getcwd()}/{path}"
    with open(path, "r") as f:
        documents = json.load(f)
    if isinstance(documents, list):
        for document in documents:
            doc = VectorDocument.parse_obj(document)
            pprint(db.create_document(document))
    else:
        doc = VectorDocument.parse_obj(documents)
        pprint(db.create_document(doc))

def delete_document(document_id: str):
    """
    Delete a document by ID
    """
    db = VectorDB()
    db.delete_document(document_id)
    print(f"Deleted document {document_id}")

def search_documents(language: str, path: str):
    """
    Search for documents
    """
    de = VectorDB()
    results = de.search_documents(language, path)
    with open('results.json', 'w') as f:
        json.dump(results, f, indent=4)
    pprint(results)

class VectorCommandsLoader(CLICommandsLoader):
    def load_command_table(self, args):
        with CommandGroup(self, "vector", "__main__#{}") as g:
            g.command("get", "get_document")
            g.command("create", "create_document")
            g.command("delete", "delete_document")
            g.command("search", "search_documents")
        return OrderedDict(self.command_table)
    
    def load_arguments(self, command):
        with ArgumentsContext(self, "vector") as ac:
            ac.argument("document_id", type=str, help="The ID of the document to retrieve", options_list=("--id"))
        super(VectorCommandsLoader, self).load_arguments(command)


cli = CLI(cli_name="apigpt", commands_loader_cls=VectorCommandsLoader)
exit_code = cli.invoke(sys.argv[1:])
sys.exit(exit_code)
