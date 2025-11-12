"""Parser for data folder format with # Title, ## Question/Answer sections"""

import argparse
import asyncio
import json
import os
from pathlib import Path
from typing import List, Tuple


async def parse_data(file_path: str) -> List[Tuple[str, str, str]]:
    qa_pairs = []
    current_title = None
    current_question = []
    current_answer = []
    in_question_section = False
    in_answer_section = False
    in_code_block = False

    with open(file_path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    # Process file by sections (each section starts with # Title)
    sections = []
    current_section = []

    for line in lines:
        # Detect start or end of a fenced code block
        if line.strip().startswith("```"):
            in_code_block = not in_code_block
            current_section.append(line)
            continue

        # If not in a code block and line is a first-level title
        if not in_code_block and line.strip().startswith("# ") and current_section:
            sections.append(current_section)
            current_section = []

        current_section.append(line)

    if current_section:
        sections.append(current_section)

    # Process each section
    for section in sections:
        current_title = None
        current_question = []
        current_answer = []
        in_question_section = False
        in_answer_section = False

        for line in section:
            line = line.strip()

            # Extract title (starts with single #)
            if line.startswith("# ") and not current_title:
                current_title = line[2:].strip()
                continue

            # Detect Question and Answer sections
            if line.startswith("## question") or line.startswith("## Question"):
                in_question_section = True
                in_answer_section = False
                continue
            elif line.startswith("## answer") or line.startswith("## Answer"):
                in_question_section = False
                in_answer_section = True
                continue

            # Collect question and answer content
            if in_question_section and line and not line.startswith("#"):
                current_question.append(line)
            elif in_answer_section and line and not line.startswith("#"):
                current_answer.append(line)

        # Combine title and question
        if current_title:
            full_question = current_title
            if current_question:
                question_text = "\n".join(current_question).strip()
                full_question = f"title: {current_title}\n\nquestion: {question_text}"

            answer_text = "\n".join(current_answer).strip()

            if full_question and answer_text:
                qa_pairs.append((current_title, full_question, answer_text))

    return qa_pairs


def convert_md_to_jsonl(md_file_path: str, json_file_path: str):
    qa_pairs = asyncio.run(parse_data(md_file_path))
    jsonLFile = open(json_file_path, "a", encoding="utf-8")
    for idx, (title, question, answer) in enumerate(qa_pairs, 1):
        test_data = {
            "testcase": title,
            "query": question,
            "ground_truth": answer,
            "context": "",
            "response_length": len(answer),
        }
        jsonLFile.write(json.dumps(test_data, ensure_ascii=False) + "\n")
        jsonLFile.flush()

    jsonLFile.close()


if __name__ == "__main__":
    print("🚀 Starting converting ...")

    parser = argparse.ArgumentParser(description="Convert md to jsonL.")

    parser.add_argument("--source_md_path", type=str, help="the path to the source md file or folder")
    parser.add_argument("--dest_jsonl_folder", type=str, help="the path to the dest jsonl folder.")
    args = parser.parse_args()

    script_directory = os.path.dirname(os.path.abspath(__file__))
    print("Script directory:", script_directory)

    if args.dest_jsonl_folder == None:
        args.dest_jsonl_folder = os.path.join(script_directory, "tests")

    output_test_dir = Path(args.dest_jsonl_folder)
    output_test_dir.mkdir(exist_ok=True)

    if os.path.isfile(args.source_md_path):
        output_file = os.path.join(
            args.dest_jsonl_folder, f"{os.path.splitext(os.path.basename(args.source_md_path))[0]}.jsonl"
        )
        convert_md_to_jsonl(args.source_md_path, output_file)
    elif os.path.isdir(args.source_md_path):
        for md_file in Path(args.source_md_path).glob("*.md"):
            output_file = os.path.join(
                args.dest_jsonl_folder, f"{os.path.splitext(os.path.basename(md_file))[0]}.jsonl"
            )
            convert_md_to_jsonl(md_file, output_file)
    else:
        print("Path does not exist.")
