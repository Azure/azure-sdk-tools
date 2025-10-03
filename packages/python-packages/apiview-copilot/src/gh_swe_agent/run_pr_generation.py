#!/usr/bin/env python3
"""
APIView Comment Resolution and PR Generation Script

This script processes APIView comments from a JSON file, searches for the relevant code locations,
checks if files are generated or handwritten, and prepares information for PR generation.

Environment Variables:
- GITHUB_TOKEN: Required for GitHub API access. Can be set in environment or .env file.

Usage:
    python run_pr_generation.py <comments_json_file> [output_plan_file]
    
Example:
    python run_pr_generation.py azure-schemaregistry.json
"""

import json
import os
import dotenv
import sys
from pathlib import Path
from file_search import (
    github_batch_search,
    enhance_search_results_with_generation_check,
    process_search_results,
    search_apiview_comments
)

def load_comments_json(json_file_path):
    """
    Load APIView comments from a JSON file.
    
    Args:
        json_file_path: Path to the JSON file containing APIView comments
    
    Returns:
        Dictionary containing the parsed JSON data
    """
    try:
        with open(json_file_path, 'r') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File '{json_file_path}' not found.")
        return None
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in '{json_file_path}': {e}")
        return None

def analyze_apiview_comments(json_file_path):
    """
    Analyze APIView comments and search for relevant code locations.
    
    Args:
        json_file_path: Path to the JSON file containing APIView comments
    
    Returns:
        Enhanced search results with generation status information
    """
    print(f"Loading APIView comments from: {json_file_path}")
    comments_data = load_comments_json(json_file_path)
    
    if not comments_data:
        return None
    
    package = comments_data.get('package', 'unknown')
    branch = comments_data.get('branch', 'main')
    num_comments = len(comments_data.get('comments', []))
    
    print(f"Package: {package}")
    print(f"Branch: {branch}")
    print(f"Number of comments: {num_comments}")
    print(f"{'='*60}")
    
    # Search for code locations based on APIView comments
    print("Searching for code locations...")
    search_results = search_apiview_comments(comments_data)
    
    if not search_results:
        print("No search results found.")
        return None
    
    # Enhance results with generation status checking
    enhanced_results = enhance_search_results_with_generation_check(search_results)
    
    return enhanced_results

def generate_pr_plan(enhanced_results, comments_data):
    """
    Generate a plan for PR creation based on search results and generation status.
    
    Args:
        enhanced_results: Search results enhanced with generation status
        comments_data: Original APIView comments data
    
    Returns:
        Dictionary containing PR plan information
    """
    pr_plan = {
        'package': comments_data.get('package', 'unknown'),
        'branch': comments_data.get('branch', 'main'),
        'repositories_to_modify': set(),
        'generated_files': [],
        'handwritten_files': [],
        'unknown_files': [],
        'changes_summary': []
    }
    
    # Analyze each result to categorize files and plan changes
    for result in enhanced_results:
        if result['status'] == 'success' and result['data'].get('items'):
            for item in result['data']['items']:
                repo_name = item['repository']['full_name']
                file_path = item['path']
                pr_plan['repositories_to_modify'].add(repo_name)
                
                if 'generation_status' in item:
                    gen_status = item['generation_status']
                    file_info = {
                        'repo': repo_name,
                        'file_path': file_path,
                        'query': result['query']
                    }
                    
                    if gen_status['is_generated'] is True:
                        pr_plan['generated_files'].append(file_info)
                    elif gen_status['is_generated'] is False:
                        pr_plan['handwritten_files'].append(file_info)
                    else:
                        pr_plan['unknown_files'].append(file_info)
    
    # Convert set to list for JSON serialization
    pr_plan['repositories_to_modify'] = list(pr_plan['repositories_to_modify'])
    
    # Generate change recommendations
    for comment in comments_data.get('comments', []):
        change_summary = {
            'line_id': comment.get('LineID', ''),
            'comment': comment.get('Comment', ''),
            'line': comment.get('Line', ''),
            'recommended_action': determine_recommended_action(comment, pr_plan)
        }
        pr_plan['changes_summary'].append(change_summary)
    
    return pr_plan

def determine_recommended_action(comment, pr_plan):
    """
    Determine the recommended action for a specific comment based on file analysis.
    
    Args:
        comment: Individual comment data
        pr_plan: PR plan containing file categorization
    
    Returns:
        String describing the recommended action
    """
    line_id = comment.get('LineID', '')
    
    # Check if this comment relates to generated or handwritten files
    for file_info in pr_plan['generated_files']:
        if line_id in file_info['query']:
            return "Modify TypeSpec source or use _patch.py (generated file)"
    
    for file_info in pr_plan['handwritten_files']:
        if line_id in file_info['query']:
            return "Modify file directly (handwritten file)"
    
    return "Manual investigation required"

def save_pr_plan(pr_plan, output_file):
    """
    Save the PR plan to a JSON file.
    
    Args:
        pr_plan: PR plan dictionary
        output_file: Path to save the PR plan
    """
    try:
        with open(output_file, 'w') as f:
            json.dump(pr_plan, f, indent=2)
        print(f"PR plan saved to: {output_file}")
    except Exception as e:
        print(f"Error saving PR plan: {e}")

def main():
    """Main function to run the APIView comment analysis and PR generation."""
    # Load environment variables from .env file
    dotenv.load_dotenv()
    
    if len(sys.argv) < 2:
        print("Usage: python run_pr_generation.py <comments_json_file> [output_plan_file]")
        print("Example: python run_pr_generation.py azure-schemaregistry.json")
        sys.exit(1)
    
    json_file_path = sys.argv[1]
    output_file = sys.argv[2] if len(sys.argv) > 2 else f"{Path(json_file_path).stem}_pr_plan.json"
    
    # Check if GitHub token is set
    github_token = os.environ.get('GITHUB_TOKEN')
    if not github_token:
        print("Warning: GITHUB_TOKEN environment variable not set.")
        print("Please set GITHUB_TOKEN in your environment or create a .env file with:")
        print("GITHUB_TOKEN=your_github_token_here")
        print("Some features may not work without authentication.")
    else:
        print("✓ GitHub token loaded successfully")
    
    print("APIView Comment Analysis and PR Generation")
    print("=" * 50)
    
    # Load and analyze comments
    comments_data = load_comments_json(json_file_path)
    if not comments_data:
        sys.exit(1)
    
    # Analyze APIView comments and search for code locations
    enhanced_results = analyze_apiview_comments(json_file_path)
    if not enhanced_results:
        sys.exit(1)
    
    # Display search results
    print("\nSearch Results with Generation Status:")
    print("=" * 50)
    process_search_results(enhanced_results)
    
    # Generate PR plan
    print("\nGenerating PR Plan...")
    print("=" * 50)
    pr_plan = generate_pr_plan(enhanced_results, comments_data)
    
    # Display PR plan summary
    print(f"Package: {pr_plan['package']}")
    print(f"Target Branch: {pr_plan['branch']}")
    print(f"Repositories to modify: {len(pr_plan['repositories_to_modify'])}")
    for repo in pr_plan['repositories_to_modify']:
        print(f"  - {repo}")
    
    print(f"\nFile Analysis:")
    print(f"  Generated files: {len(pr_plan['generated_files'])}")
    print(f"  Handwritten files: {len(pr_plan['handwritten_files'])}")
    print(f"  Unknown status files: {len(pr_plan['unknown_files'])}")
    
    print(f"\nChange Summary:")
    for i, change in enumerate(pr_plan['changes_summary'], 1):
        print(f"  {i}. {change['line_id']}")
        print(f"     Comment: {change['comment']}")
        print(f"     Action: {change['recommended_action']}")
        print()
    
    # Save PR plan
    save_pr_plan(pr_plan, output_file)
    
    print(f"\nNext Steps:")
    print("1. Review the generated PR plan")
    print("2. For generated files, locate TypeSpec sources or modify _patch.py files")
    print("3. For handwritten files, make changes directly")
    print("4. Create and test the changes")
    print("5. Submit PR for review")

if __name__ == "__main__":
    main()
