import sys
from collections import OrderedDict
from typing import List

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from src import VectorDB, VectorDocument


def get_document(document_id: str):
    """
    Retrieve a document by ID
    """
    db = VectorDB()
    document = db.get_document(document_id)
    print(document.to_json())

def create_document(language: str, bad_code: str, good_code: str, comment: str, guideline_ids: List[str]):
    """
    Create a new document
    """
    db = VectorDB()
    document = VectorDocument(
        language=language,
        bad_code=bad_code,
        good_code=good_code,
        comment=comment,
        guideline_ids=guideline_ids
    )
    document = db.create_document(document)
    print(document.to_json())

class VectorCommandsLoader(CLICommandsLoader):
    def load_command_table(self, args):
        with CommandGroup(self, "vector", "__main__#{}") as g:
            g.command("get", "get_document")
            g.command("create", "create_document")
        return OrderedDict(self.command_table)
    
    def load_arguments(self, command):
        with ArgumentsContext(self, "vector") as ac:
            ac.argument("document_id", type=str, help="The ID of the document to retrieve", options_list=("--id"))
        super(VectorCommandsLoader, self).load_arguments(command)

cli = CLI(cli_name="apigpt", commands_loader_cls=VectorCommandsLoader)
exit_code = cli.invoke(sys.argv[1:])
sys.exit(exit_code)
