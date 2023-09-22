from collections import OrderedDict
import json
import os
from pprint import pprint
import sys
from typing import List

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help import CLIHelp
from knack.help_files import helps
from src import VectorDB, VectorDocument

helps["review"] = """
    type: group
    short-summary: Commands related to APIView GPT reviews.
"""

helps["guidelines"] = """
    type: group
    short-summary: Commands related to API guidelines.
"""

helps["vector"] = """
    type: group
    short-summary: Commands related to the vector database.
"""

def get_document(document_id: str):
    """
    Retrieve a vector document by ID
    """
    db = VectorDB()
    pprint(db.get_document(document_id))

def create_document(path: str):
    """
    Add an array of vector documents to the database.
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
    Delete a vector document by ID
    """
    db = VectorDB()
    db.delete_document(document_id)
    print(f"Deleted document {document_id}")

def search_documents(language: str, path: str, log_result: bool = False):
    """
    Search for vector documents by similarity
    """
    de = VectorDB()
    with open(path, "r") as f:
        code = f.read()
    results = de.search_documents(language, code)
    if log_result:
        with open('search_result_dump.json', 'w') as f:
            json.dump(results, f, indent=4)
    pprint(results)

def generate_review(language: str, path: str, log_prompts: bool = False):
    """
    Generate a review for an APIView
    """
    from src import GptReviewer
    rg = GptReviewer(log_prompts=log_prompts)
    filename = os.path.splitext(os.path.basename(path))[0]

    with open(path, "r") as f:
        apiview = f.read()
    review = rg.get_response(apiview, language)
    output_path = os.path.join('scratch', 'output', language)
    if not os.path.exists(output_path):
        os.makedirs(output_path)
    with open(os.path.join(output_path, f'{filename}.json'), 'w') as f:
        f.write(review.json(indent=4))
    pprint(review)

def parse_guidelines(language: str, path: str):
    """
    Parse API guidelines
    """
    from src import GuidelinesParser
    gp = GuidelinesParser(language)
    with open(path, "r") as f:
        guidelines = f.read()
    parsed = gp.parse(guidelines)
    with open('guidelines.json', 'w') as f:
        json.dump(parsed, f, indent=4)
    pprint(parsed)

class CliCommandsLoader(CLICommandsLoader):
    def load_command_table(self, args):
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
        with CommandGroup(self, "guidelines", "__main__#{}") as g:
            g.command("parse", "parse_guidelines")
        with CommandGroup(self, "vector", "__main__#{}") as g:
            g.command("get", "get_document")
            g.command("create", "create_document")
            g.command("delete", "delete_document")
            g.command("search", "search_documents")
        return OrderedDict(self.command_table)
    
    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument("language", type=str, help="The language of the APIView file", options_list=("--language", "-l"))
        with ArgumentsContext(self, "vector") as ac:
            ac.argument("document_id", type=str, help="The ID of the document to retrieve", options_list=("--id"))
            ac.argument("log_result", action="store_true", help="Log the search results to a file called 'search_result_dump.json'")
            ac.argument("path", type=str, help="The path to a JSON file containing an array of vector documents to add.")
        with ArgumentsContext(self, "guidelines") as ac:
            ac.argument("path", type, help="The path to the guidelines")
        with ArgumentsContext(self, "review") as ac:
            ac.argument("path", type=str, help="The path to the APIView file")
            ac.argument("log_prompts", action="store_true", help="Log each prompt in ascending order in the `scratch/propmts` folder.")
        super(CliCommandsLoader, self).load_arguments(command)


cli = CLI(cli_name="apigpt", commands_loader_cls=CliCommandsLoader)
exit_code = cli.invoke(sys.argv[1:])
sys.exit(exit_code)
