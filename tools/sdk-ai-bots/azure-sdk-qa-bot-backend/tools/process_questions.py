"""
process_questions.py — Batch Q&A evaluation tool for the Azure SDK QA bot.

Usage:
    # Process all typespec_YYYY_MM_DD.md files in the current directory:
    python process_questions.py

    # Process one or more specific files (glob patterns supported):
    python process_questions.py typespec_2025_06_26.md
    python process_questions.py typespec_*.md

Prerequisites:
    The QA bot backend must be running locally on port 8088:
        uvicorn main:app --port 8088

Output:
    For each input file a result file is written in the same directory, e.g.
        typespec_2025_06_26.md  ->  typespec_qa_results_2025_06_26.md
"""

import re
import sys
import glob
import os
import requests
import json


def process_file(input_file: str) -> None:
    """Process a single input markdown file and write results to a dated output file."""

    # Derive output filename from input filename.
    # e.g. typespec_2025_06_26.md  ->  typespec_qa_results_2025_06_26.md
    basename = os.path.basename(input_file)
    date_match = re.search(r'(\d{4}_\d{2}_\d{2})', basename)
    if date_match:
        date_suffix = date_match.group(1)
        output_file = f'typespec_qa_results_{date_suffix}.md'
    else:
        # Fallback: strip extension and append _results
        stem = os.path.splitext(basename)[0]
        output_file = f'{stem}_results.md'

    print(f'\n=== Processing: {input_file} -> {output_file} ===')

    # Read the markdown file
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Split by sections (headers starting with #)
    sections = re.split(r'\n(?=# [^#])', content)

    # Extract questions from each section
    qa_pairs = []

    for section in sections:
        if not section.strip():
            continue

        # Extract title
        title_match = re.match(r'# (.+)', section)
        if not title_match:
            continue

        title = title_match.group(1).strip()

        # Extract question
        question_match = re.search(r'## question\s+(.+?)(?=## answer|\Z)', section, re.DOTALL)
        if not question_match:
            continue

        question = question_match.group(1).strip()

        # Prepare request to API
        print(f"  Processing: {title[:50]}...")

        try:
            response = requests.post(
                'http://localhost:8088/completion',
                json={
                    'tenant_id': 'azure_sdk_qa_bot',
                    'message': {
                        'role': 'user',
                        'content': question
                    }
                },
                headers={'Content-Type': 'application/json; charset=utf-8'},
                timeout=300
            )

            if response.status_code == 200:
                result = response.json()
                answer = result.get('answer', result.get('content', 'No answer received'))
            else:
                answer = f"Error: HTTP {response.status_code} - {response.text}"
        except Exception as e:
            answer = f"Error: {str(e)}"

        qa_pairs.append({
            'title': title,
            'question': question,
            'api_answer': answer
        })

    # Write results to new markdown file
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('# TypeSpec Q&A Results\n\n')

        for i, qa in enumerate(qa_pairs, 1):
            f.write(f'## {i}. {qa["title"]}\n\n')
            f.write(f'### Question\n\n{qa["question"]}\n\n')
            f.write(f'### API Response\n\n{qa["api_answer"]}\n\n')
            f.write('---\n\n')

    print(f'  Processed {len(qa_pairs)} questions. Results saved to {output_file}')


def resolve_input_files(args: list[str]) -> list[str]:
    """
    Resolve the list of input files.

    - If arguments are provided, use them (supports glob patterns).
    - If no arguments are provided, default to all typespec_YYYY_MM_DD.md files
      in the current directory, excluding files already in the old/ folder.
    """
    if args:
        files: list[str] = []
        for pattern in args:
            matched = glob.glob(pattern)
            if matched:
                files.extend(matched)
            elif os.path.isfile(pattern):
                files.append(pattern)
            else:
                print(f"Warning: no files matched '{pattern}'", file=sys.stderr)
        return sorted(set(files))

    # Default: all typespec_YYYY_MM_DD.md in current directory
    default = sorted(glob.glob('typespec_????_??_??.md'))
    if not default:
        print('No typespec_YYYY_MM_DD.md files found in current directory.', file=sys.stderr)
    return default


if __name__ == '__main__':
    input_files = resolve_input_files(sys.argv[1:])

    if not input_files:
        sys.exit(1)

    for input_file in input_files:
        process_file(input_file)

    print(f'\nDone. Processed {len(input_files)} file(s).')
