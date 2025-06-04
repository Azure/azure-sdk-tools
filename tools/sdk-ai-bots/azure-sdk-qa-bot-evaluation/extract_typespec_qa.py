import re
import json
import aiohttp
import asyncio
import time
import os
from pathlib import Path
from typing import List, Dict, Any, Tuple, Optional

class QAParser:
    """Base class for QA parsers"""
    async def parse(self, file_path: str) -> List[Tuple[str, str]]:
        raise NotImplementedError

class ExistedQAParser(QAParser):
    """Parser for existed_qa folder format with ## headers"""
    async def parse(self, file_path: str) -> List[Tuple[str, str]]:
        qa_pairs = []
        
        print(f"Opening file: {file_path}")
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            print(f"File content length: {len(content)} characters")
            
        sections = content.split('\n## ')
        
        for section in sections:
            if not section.strip():
                continue
                
            lines = section.strip().split('\n')
            question = lines[0].strip('# ')
            answer = '\n'.join(lines[1:]).strip()
            
            if question and answer:
                qa_pairs.append((question, answer))
                
        return qa_pairs

class ChannelQAParser(QAParser):
    """Parser for channel folder format with # Title, ## Question/Answer sections"""
    async def parse(self, file_path: str) -> List[Tuple[str, str]]:
        qa_pairs = []
        current_title = None
        current_question = []
        current_answer = []
        in_question_section = False
        in_answer_section = False
        
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Process file by sections (each section starts with # Title)
        sections = []
        current_section = []
        
        for line in lines:
            if line.strip().startswith('# ') and current_section:
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
                if line.startswith('# ') and not current_title:
                    current_title = line[2:].strip()
                    continue
                
                # Detect Question and Answer sections
                if line.startswith('## question') | line.startswith('## Question'):
                    in_question_section = True
                    in_answer_section = False
                    continue
                elif line.startswith('## answer') | line.startswith('## Answer'):
                    in_question_section = False
                    in_answer_section = True
                    continue
                
                # Collect question and answer content
                if in_question_section and line and not line.startswith('#'):
                    current_question.append(line)
                elif in_answer_section and line and not line.startswith('#'):
                    current_answer.append(line)
            
            # Combine title and question
            if current_title:
                full_question = current_title
                if current_question:
                    question_text = '\n'.join(current_question).strip()
                    full_question = f"title: {current_title}\n\nquestion: {question_text}"
                
                answer_text = '\n'.join(current_answer).strip()
                
                if full_question and answer_text:
                    qa_pairs.append((full_question, answer_text))
            
        return qa_pairs

def get_parser_for_file(file_path: str) -> QAParser:
    """Return appropriate parser based on file location and type"""
    path = Path(file_path)
    if 'existed_qa' in str(path):
        return ExistedQAParser()
    elif 'channel' in str(path):
        return ChannelQAParser()
    elif 'github' in str(path):
        return ChannelQAParser()
    else:
        raise ValueError(f"Unknown file type: {file_path}")

async def call_completion_api(question: str) -> Dict[str, Any]:
    """Call the completion API endpoint."""
    api_url = "http://localhost:8088/completion"
    headers = {
        "Content-Type": "application/json; charset=utf8",
        "X-API-Key": "xK9d#mP2$vR4nL7@jF5hQ8*wC3tY6bN9eZ2^mA4uW8gB"
    }
    payload = {
        "tenant_id": "azure_sdk_qa_bot",
        "message": {
            "role": "user",
            "content": question
        }
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(api_url, json=payload, headers=headers) as resp:
            if resp.status == 200:
                return await resp.json()
            else:
                raise Exception(f"API request failed with status {resp.status}")

async def process_qa_pair(question: str, ground_truth: str) -> Dict[str, Any]:
    """Process a single Q&A pair and generate test case."""
    start_time = time.time()
    
    try:
        api_response = await call_completion_api(question)
        latency = time.time() - start_time
        
        return {
            "query": question,
            "ground_truth": ground_truth,
            "response": api_response.get("answer", ""),
            "context": api_response.get("full_context", ""),
            "latency": latency,
            "response_length": len(api_response.get("answer", ""))
        }
    except Exception as e:
        print(f"Error processing question '{question}': {str(e)}")
        return None

async def process_file(input_file: str, output_file: str) -> None:
    """Process a single input file"""
    print(f"Processing file: {input_file}")
    try:
        parser = get_parser_for_file(input_file)
        qa_pairs = await parser.parse(input_file)
        total_pairs = len(qa_pairs)
        print(f"Found {total_pairs} Q&A pairs in {input_file}")
        
        for idx, (question, answer) in enumerate(qa_pairs, 1):
            print(f"Processing Q&A pair {idx}/{total_pairs} ({idx/total_pairs*100:.1f}%)...")
            result = await process_qa_pair(question, answer)
            if result:
                with open(output_file, 'a', encoding='utf-8') as f:
                    f.write(json.dumps(result, ensure_ascii=False) + '\n')
                print(f"✓ Successfully processed and saved Q&A pair {idx}/{total_pairs}")
            else:
                print(f"✗ Failed to process Q&A pair {idx}/{total_pairs}")
            
    except Exception as e:
        print(f"Error processing file {input_file}: {str(e)}")

async def main(file_prefix: str = None):
    """
    Process markdown files in the data directory and generate Q&A pairs.
    
    Args:
        file_prefix: Optional prefix to filter which markdown files to process.
                    If provided, only files starting with this prefix will be processed.
    """
    data_dir = Path("data")
    output_dir = Path("output")
    output_dir.mkdir(exist_ok=True)
    
    # Process each subdirectory in data folder
    for data_folder in ['channel', 'github']:
        folder_path = data_dir / data_folder
        if not folder_path.exists():
            continue
            
        # Create corresponding output file for each input folder
        output_file = output_dir / f"{data_folder}_generated_qa.jsonl"
        
        # Process markdown files in the folder, optionally filtered by prefix
        glob_pattern = f"{file_prefix}*.md" if file_prefix else "*.md"
        matching_files = list(folder_path.glob(glob_pattern))
        
        if matching_files:
            print(f"Found {len(matching_files)} files matching prefix '{file_prefix}' in {data_folder}/")
            for file_path in matching_files:
                await process_file(str(file_path), str(output_file))
        elif file_prefix:
            print(f"No files found matching prefix '{file_prefix}' in {data_folder}/")
            
    print("Processing complete. Results written to output directory.")

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="Process Q&A pairs from markdown files.")
    parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    args = parser.parse_args()
    
    asyncio.run(main(args.prefix))
