# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Dump the AVC CLI command tree with group and command descriptions."""

import re
import pathlib


def main():
    cli_path = pathlib.Path(__file__).resolve().parent.parent / "cli.py"
    content = cli_path.read_text(encoding="utf-8")

    # Extract group help strings from helps["<group>"] = """..."""
    groups = {}
    for m in re.finditer(r'helps\[\s*"(\w+)"\s*\]\s*=\s*"""(.*?)"""', content, re.DOTALL):
        name = m.group(1)
        body = m.group(2)
        summary_match = re.search(r"short-summary:\s*(.+)", body)
        if summary_match:
            groups[name] = summary_match.group(1).strip()

    # Extract command registrations per group
    commands = {}
    loader_match = re.search(r"def load_command_table\(self, args\):(.*?)return OrderedDict", content, re.DOTALL)
    if not loader_match:
        print("Could not find load_command_table")
        return
    loader_body = loader_match.group(1)
    for m in re.finditer(
        r'CommandGroup\(self,\s*"([\w-]+)".*?\) as g:(.*?)(?=with CommandGroup|$)', loader_body, re.DOTALL
    ):
        group = m.group(1)
        block = m.group(2)
        cmds = re.findall(r'g\.command\("([\w-]+)",\s*"(\w+)"\)', block)
        commands[group] = cmds

    # Extract first-line docstrings for command functions
    func_docs = {}
    for m in re.finditer(r'def (\w+)\([^)]*\).*?:\s*"""(.*?)"""', content, re.DOTALL):
        func_docs[m.group(1)] = m.group(2).strip().split("\n")[0].strip()

    # Print tree
    for group in commands:
        desc = groups.get(group, "")
        print(f"{group}: {desc}")
        for cmd_name, func_name in commands[group]:
            cmd_desc = func_docs.get(func_name, "")
            print(f"  {cmd_name}: {cmd_desc}")
        print()


if __name__ == "__main__":
    main()
