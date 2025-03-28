#-------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
#--------------------------------------------------------------------------

import dotenv
from bs4 import BeautifulSoup
import json
import markdown_it
import os
import re
import sys
from typing import List, Optional, Tuple

dotenv.load_dotenv()

# Create a new MarkdownIt instance
md = markdown_it.MarkdownIt()

# API Doc Constants
MAY_PATTERN = r'{% include requirement/MAY\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MAY_REPLACE = 'YOU MAY'
MUST_DO_PATTERN = r'{% include requirement/MUST\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MUST_NO_ID_PATTERN = r'{% include requirement/MUST %}'
MUST_DO_REPLACE = 'DO'
MUST_NOT_PATTERN = r'{% include requirement/MUSTNOT\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
MUST_NOT_REPLACE = 'DO NOT'
SHOULD_PATTERN = r'{% include requirement/SHOULD\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
SHOULD_NO_ID_PATTERN = r'{% include requirement/SHOULD %}'
SHOULD_REPLACE = 'YOU SHOULD'
SHOULD_NOT_PATTERN = r'{% include requirement/SHOULDNOT\s*id=\\?"[a-zA-Z0-9_-]+\\?" %}'
SHOULD_NOT_REPLACE = 'YOU SHOULD NOT'
INCLUDE_PATTERN = r'{%\s*(include|include_relative)\s*([^\s%}]+)\s*%}'
INCLUDE_NOTE_PATTERN = r'{% include note.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_NOTE_REPLACE = r'**NOTE:** \1'
INCLUDE_DRAFT_PATTERN = r'{% include draft.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_DRAFT_REPLACE = r'**DRAFT:** \1'
INCLUDE_IMPORTANT_PATTERN = r'{% include important.html content=\\?"([^\\]+)\\?" %}'
INCLUDE_IMPORTANT_REPLACE = r'**IMPORTANT:** \1'

ICON_PATTERN = r'^:[a-z_]+: '
ICON_REPLACE = ''


# Parse the markdown file
def parse_markdown(file, root_path) -> List[dict]:
    with open(file, 'r', encoding='utf-8') as f:
        md_text = f.read()

    entries = []
    html = md.render(md_text)
    soup = BeautifulSoup(html, features="html.parser")
    category = None

    for item in soup.find_all():
        if item.name in ['h1', 'h2', 'h3', 'h4', 'h5', 'h6']:
            category = item.text
        # Skip the explanations of rule types in introduction section
        if category == 'Prescriptive Guidance':
            continue
        
        if item.name == 'p':
            text, id = _split_tags(item, file)
            text = _add_links(text, item)
            text = _expand_include_tags(text, root_path, os.path.dirname(file))

            if id:
                entries.append({
                    'id': id,
                    'category': category,
                    'text': text,
                })
            else:
                try:
                    entries[-1]['text'] += '\n\n' + text
                except IndexError:
                    continue
        elif item.name in ["pre"]:
            raw_html = ''.join(str(tag) for tag in item.contents)
            markdown_text = _convert_code_tag_to_markdown(raw_html)
            markdown_text = _expand_include_tags(markdown_text, root_path, os.path.dirname(file))
            try:
                entries[-1]['text'] += '\n\n' + markdown_text
            except IndexError:
                continue
        elif item.name in ['ol', 'ul']:
            items = item.find_all('li')
            for item in items:
                item_text, id = _split_tags(item, file)
                item_text = _add_links(item_text, item)
                item_text = _expand_include_tags(item_text, root_path, os.path.dirname(file))
                if id:
                    entries.append({
                        'id': id,
                        'category': category,
                        'text': item_text,
                    })
                else:
                    try:
                        entries[-1]['text'] += '\n' + item_text
                    except IndexError:
                        continue
        else:
            continue
    return entries


def _add_links(text, item):
    """Find any links associated with the text and add them in format: text (link)
    """
    links = [link for link in item.find_all("a") if link.get("href", "").startswith("http")]
    if not links:
        return text

    for link in links:
        index = text.find(link.text)
        if index == -1:
            continue
        text = f"{text[:index]}{link.text} ({link['href']}) {text[len(link.text)+1 + index:]}"
    return text


def _expand_include_tags(text, root_path, rel_path) -> str:
    matches = re.findall(INCLUDE_PATTERN, text)
    if not matches:
        return text
    for match in matches:
        include_tag = match[0]
        include_path = match[1]
        if include_tag == 'include_relative':
            include_path = os.path.join(rel_path, include_path)
            with open(include_path, 'r', encoding='utf-8') as f:
                text = f.read()
        else:
            include_path = os.path.join(root_path, "_includes", include_path)
            with open(include_path, 'r', encoding='utf-8') as f:
                text = f.read()
    # if text looks like html, convert it to markdown
    if text.startswith('<'):
        return _convert_html_to_markdown(text)
    else:
        return text


def _convert_html_to_markdown(html) -> str:
    # convert HTML text to markdown
    markdown = md.render(html)
    return markdown


def _convert_code_tag_to_markdown(html):
    # Define the regular expression to match the code tag
    code_tag_pattern = r'<code class="language-(.+)">([\s\S]*?)</code>'

    match = re.search(code_tag_pattern, html)
    if match:
        language = match[1]
        code = match[2]
        markdown = f'```{language}\n{code}\n```'
        return markdown
    else:
        return html
 

# Split the tag from the ID
def _split_tags(item, file) -> Tuple[str, Optional[str]]:
    text = item.text
    id = _extract_id_from_inline(item)
    text = re.sub(MAY_PATTERN, MAY_REPLACE, text)
    text = re.sub(MUST_DO_PATTERN, MUST_DO_REPLACE, text)
    text = re.sub(MUST_NO_ID_PATTERN, MUST_DO_REPLACE, text)
    text = re.sub(MUST_NOT_PATTERN, MUST_NOT_REPLACE, text)
    text = re.sub(SHOULD_PATTERN, SHOULD_REPLACE, text)
    text = re.sub(SHOULD_NO_ID_PATTERN, SHOULD_REPLACE, text)
    text = re.sub(SHOULD_NOT_PATTERN, SHOULD_NOT_REPLACE, text)
    text = re.sub(ICON_PATTERN, ICON_REPLACE, text)
    text = re.sub(INCLUDE_NOTE_PATTERN, INCLUDE_NOTE_REPLACE, text)
    text = re.sub(INCLUDE_IMPORTANT_PATTERN, INCLUDE_IMPORTANT_REPLACE, text)
    text = re.sub(INCLUDE_DRAFT_PATTERN, INCLUDE_DRAFT_REPLACE, text)

    # REST API guidelines don't actually have IDs.
    if not file.endswith("Guidelines.md"):
        segments = file.split(os.sep)
        relevant_segments = segments[segments.index("docs") + 1:]
        prefix = "_".join(relevant_segments).replace(".md", ".html")
        id = f"{prefix}#{id}" if id else id

    return text, id


# Extract the id from the inline text
def _extract_id_from_inline(item):
    id = re.search(r'id="([a-zA-Z0-9_-]+)"', item.text)
    if id:
        return id.group(1)
    try:
        id = item.next_element.attrs["name"]
    except:
        id = None
    return id
