from collections import OrderedDict
import json
import os
from pprint import pprint
import sys

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
from typing import Literal

helps["review"] = """
    type: group
    short-summary: Commands related to APIView GPT reviews.
"""

def generate_review(language: str, path: str, model: Literal["gpt-4o-mini", "gpt-o3-mini"], chunk_input: bool = False, log_prompts: bool = False):
    """
    Generate a review for an APIView
    """
    from src._apiview_reviewer import ApiViewReview
    rg = ApiViewReview(language=language, model=model, log_prompts=log_prompts)
    filename = os.path.splitext(os.path.basename(path))[0]

    with open(path, "r") as f:
        apiview = f.read()
    review = rg.get_response(apiview, chunk_input=chunk_input)
    output_path = os.path.join('scratch', 'output', language)
    if not os.path.exists(output_path):
        os.makedirs(output_path)
    with open(os.path.join(output_path, f'{filename}.json'), 'w') as f:
        f.write(review.model_dump_json(indent=4))
    pprint(review)

class CliCommandsLoader(CLICommandsLoader):
    def load_command_table(self, args):
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
        return OrderedDict(self.command_table)
    
    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument("language", type=str, help="The language of the APIView file", options_list=("--language", "-l"))
        with ArgumentsContext(self, "review") as ac:
            ac.argument("path", type=str, help="The path to the APIView file")
            ac.argument("log_prompts", action="store_true", help="Log each prompt in ascending order in the `scratch/propmts` folder.")
            ac.argument("model", type=str, help="The model to use for the review", options_list=("--model", "-m"), choices=["gpt-4o-mini", "gpt-o3-mini"])
            ac.argument("chunk_input", action="store_true", help="Chunk the input into smaller sections.")
        super(CliCommandsLoader, self).load_arguments(command)


cli = CLI(cli_name="apigpt", commands_loader_cls=CliCommandsLoader)
exit_code = cli.invoke(sys.argv[1:])
sys.exit(exit_code)
