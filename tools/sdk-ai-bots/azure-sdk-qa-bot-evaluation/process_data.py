import json
import glob
import os
import re

def filter_nonsense_responses(output_dir="output"):
    # Get all jsonl files in the output directory
    jsonl_files = glob.glob(os.path.join(output_dir, "*.jsonl"))
    
    for file_path in jsonl_files:
        kept_responses = []
        processed_queries = set()  # To track duplicate queries
        excluded_counts = {
            "sorry_responses": 0,
            "github_links": 0,
            "image_references": 0,
            "duplicate_queries": 0
        }
        
        print(f"\nProcessing file: {file_path}")
        
        with open(file_path, 'r', encoding='utf-8') as f:
            for line in f:
                try:
                    record = json.loads(line.strip())
                    query = record.get('query', '')
                    response = record.get('response', '')
                    
                    # Skip responses starting with "Sorry,"
                    if response.startswith('Sorry, '):
                        excluded_counts["sorry_responses"] += 1
                        continue
                    
                    # Skip queries containing GitHub links
                    if re.search(r'github\.com|github\.|githubusercontent', query, re.IGNORECASE):
                        excluded_counts["github_links"] += 1
                        continue
                    
                    # Skip queries containing image references
                    if re.search(r'\.(png|jpg|jpeg|gif|svg|bmp|ico|webp)($|\s|[?#])', query, re.IGNORECASE):
                        excluded_counts["image_references"] += 1
                        continue
                    
                    # Skip duplicate queries
                    query_normalized = query.strip().lower()
                    if query_normalized in processed_queries:
                        excluded_counts["duplicate_queries"] += 1
                        continue
                    
                    # Add to kept responses if passed all filters
                    processed_queries.add(query_normalized)
                    kept_responses.append(record)
                    
                except json.JSONDecodeError:
                    continue
        
        # Create output filename based on input filename
        base_name = os.path.basename(file_path)
        filtered_file_path = os.path.join(output_dir, f"filtered_{base_name}")
        
        # Write filtered responses to new file
        with open(filtered_file_path, 'w', encoding='utf-8') as f:
            for record in kept_responses:
                json.dump(record, f, ensure_ascii=False)
                f.write('\n')
        
        # Print statistics
        total_excluded = sum(excluded_counts.values())
        print(f"Filtering statistics for {base_name}:")
        print(f"  - Excluded 'Sorry,' responses: {excluded_counts['sorry_responses']}")
        print(f"  - Excluded GitHub links: {excluded_counts['github_links']}")
        print(f"  - Excluded image references: {excluded_counts['image_references']}")
        print(f"  - Excluded duplicate queries: {excluded_counts['duplicate_queries']}")
        print(f"  - Total excluded: {total_excluded}")
        print(f"  - Total kept: {len(kept_responses)}")
        print(f"Filtered responses written to: {filtered_file_path}")

if __name__ == "__main__":
    filter_nonsense_responses()