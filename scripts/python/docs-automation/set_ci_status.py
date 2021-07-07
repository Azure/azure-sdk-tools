import argparse
import os

# by default, yaml does not maintain insertion order of the dicts
# given that this is intended to generate TABLE OF CONTENTS values,
# maintaining this order is important.
# The drop-in replacement oyaml is a handy solution for us.
import oyaml as yaml



if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="""
        Sets CI status for a given docs.ms moniker
        """)
    parser.add_argument("-s", "--status", help="Status", required=True)
    parser.add_argument("-m", "--status_markdown_file", help="Status markdown file", required=True)
    parser.add_argument("-t", "--table_of_contents", help="Table of contents file", required=True)
    parser.add_argument("-l", "--language", help="Language", required=True)
    parser.add_argument("-o", "--moniker", help="Moniker", default="latest")
    parser.add_argument("-r", "--repo_root", help="Path to root of docs repo", default=os.getcwd())

    args = parser.parse_args()

    # Set contents of status markdown file
    status_markdown_file_path = os.path.join(args.repo_root, args.status_markdown_file)
    with open(status_markdown_file_path, "w") as status_markdown_file:
        status_markdown_content = f"""\
# Docs Generation Status

{args.status}
"""
        status_markdown_file.write(status_markdown_content)

    # Add to ToC
    toc_file_path = os.path.join(args.repo_root, args.table_of_contents)
    with open(toc_file_path, "r") as toc:
        toc = yaml.safe_load(toc)

    # Normalize the path to match the rest of the ToC
    toc_relative_path = os.path.relpath(args.status_markdown_file, os.getcwd()).replace('\\', '/')
    new_entry = { 
        'name': 'Docs Build Status',
        'uid': f"azure.{args.language}.landingpage.docscistatus.{args.moniker}",
        'href': f"~/{toc_relative_path}"
    }
    toc[0]['items'] = [new_entry] + toc[0]['items']

    with open(toc_file_path, "w") as toc_out:
        toc_out.write(yaml.dump(toc))