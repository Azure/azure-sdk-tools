import json
import glob
import os

def exclude_sorry_responses(output_dir="output"):
    # Get all jsonl files in the output directory
    jsonl_files = glob.glob(os.path.join(output_dir, "*.jsonl"))
    
    for file_path in jsonl_files:
        kept_responses = []
        print(f"\nProcessing file: {file_path}")
        
        with open(file_path, 'r', encoding='utf-8') as f:
            for line in f:
                try:
                    record = json.loads(line.strip())
                    if not record.get('response', '').startswith('Sorry, '):
                        kept_responses.append(record)
                except json.JSONDecodeError:
                    continue
        
        # Create output filename based on input filename
        base_name = os.path.basename(file_path)
        filtered_file_path = os.path.join(output_dir, f"excluded_sorry_{base_name}")
        
        # Write non-sorry responses to new file
        with open(filtered_file_path, 'w', encoding='utf-8') as f:
            for record in kept_responses:
                json.dump(record, f, ensure_ascii=False)
                f.write('\n')
        
        print(f"Excluded {len(kept_responses)} responses that don't start with 'Sorry, '")
        print(f"Filtered responses written to: {filtered_file_path}")

if __name__ == "__main__":
    exclude_sorry_responses()