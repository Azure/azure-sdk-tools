import requests
import os
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
import logging

from openai import AzureOpenAI

from typing import List, Dict, Optional

# Configure logging
logger = logging.getLogger(__name__)

def github_code_search(query, repo=None, path=None, per_page=10):
    """Search GitHub code using the REST API"""
    
    # Build search query
    search_query = query
    if repo:
        search_query += f" repo:{repo}"
    if path:
        search_query += f" path:{path}"
    
    headers = {
        'Authorization': f'token {os.environ.get("GITHUB_TOKEN")}',
        'Accept': 'application/vnd.github.v3+json'
    }
    
    url = "https://api.github.com/search/code"
    params = {
        'q': search_query,
        'per_page': per_page
    }
    
    response = requests.get(url, headers=headers, params=params, timeout=30)
    
    if response.status_code == 200:
        return {
            'query': query,
            'status': 'success',
            'data': response.json()
        }
    elif response.status_code == 403:
        # Rate limit exceeded
        return {
            'query': query,
            'status': 'rate_limited',
            'error': f"Rate limit exceeded. Reset time: {response.headers.get('X-RateLimit-Reset', 'unknown')}"
        }
    else:
        return {
            'query': query,
            'status': 'error',
            'error': f"HTTP {response.status_code}: {response.text}"
        }

def github_batch_search(queries: List[Dict], max_workers=3, delay_between_requests=1.0):
    """
    Search GitHub code for multiple queries concurrently with rate limiting.
    
    Args:
        queries: List of dictionaries with 'query', 'repo' (optional), 'path' (optional)
        max_workers: Maximum number of concurrent requests
        delay_between_requests: Delay in seconds between requests to avoid rate limiting
    
    Returns:
        List of results for each query
    """
    results = []
    
    def search_with_delay(query_info):
        time.sleep(delay_between_requests)  # Rate limiting
        return github_code_search(
            query=query_info['query'],
            repo=query_info.get('repo'),
            path=query_info.get('path'),
            per_page=query_info.get('per_page', 10)
        )
    
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        # Submit all queries
        future_to_query = {
            executor.submit(search_with_delay, query_info): query_info 
            for query_info in queries
        }
        
        # Collect results as they complete
        for future in as_completed(future_to_query):
            query_info = future_to_query[future]
            try:
                result = future.result()
                results.append(result)
                print(f"✓ Completed search for: {query_info['query']}")
            except Exception as exc:
                print(f"✗ Error searching for {query_info['query']}: {exc}")
                results.append({
                    'query': query_info['query'],
                    'status': 'exception',
                    'error': str(exc)
                })
    
    return results

def process_search_results(results: List[Dict]):
    """Process and display search results in a formatted way"""
    
    for result in results:
        print(f"\n{'='*60}")
        print(f"Query: {result['query']}")
        print(f"Status: {result['status']}")
        
        if result['status'] == 'success':
            data = result['data']
            total_count = data.get('total_count', 0)
            print(f"Total matches: {total_count}")
            
            if data.get('items'):
                print(f"Showing first {len(data['items'])} results:")
                for i, item in enumerate(data['items'], 1):
                    print(f"  {i}. File: {item['path']}")
                    print(f"     URL: {item['html_url']}")
                    print(f"     Score: {item['score']}")
                    
                    # Display generation status if available
                    if 'generation_status' in item:
                        gen_status = item['generation_status']
                        if gen_status['is_generated'] is True:
                            print(f"     🤖 GENERATED FILE - {gen_status['message']}")
                        elif gen_status['is_generated'] is False:
                            print(f"     ✍️  HANDWRITTEN FILE - {gen_status['message']}")
                        else:
                            print(f"     ❓ STATUS UNKNOWN - {gen_status['message']}")
                    
                    if i < len(data['items']):
                        print()
            else:
                print("No results found.")
        else:
            print(f"Error: {result.get('error', 'Unknown error')}")

def search_apiview_comments(comments_data: Dict):
    """
    Search for file locations based on APIView comments data.
    
    Args:
        comments_data: Dictionary containing package and comments information
    
    Returns:
        Search results for each LineID
    """
    package = comments_data.get('package', '')
    repo = "Azure/azure-sdk-for-python"  # Assuming Python SDK
    
    queries = []
    for comment in comments_data.get('comments', []):
        line_id = comment.get('LineID', '')
        
        # Extract the symbol name from LineID (e.g., "JSON" from "azure.schemaregistry.models.SchemaContentTypeValues.JSON")
        if '.' in line_id:
            symbol = line_id.split('.')[-1]
            # Also search for the class/enum name
            class_parts = line_id.split('.')
            if len(class_parts) >= 2:
                class_name = class_parts[-2]  # e.g., "SchemaContentTypeValues"
                
                queries.append({
                    'query': f"{class_name}.{symbol}",
                    'repo': repo,
                    'path': f"sdk/{package.replace('azure-', '')}"
                })
    
    return github_batch_search(queries)

def check_file_generation_status(repo, file_path, github_token=None):
    """
    Check if a file is generated by examining its header for the Microsoft code generator comment.
    
    Args:
        repo: Repository name in format "owner/repo"
        file_path: Path to the file in the repository
        github_token: GitHub token for authentication (uses env var if not provided)
    
    Returns:
        Dict with 'is_generated' (bool) and 'status' information
    """
    if github_token is None:
        github_token = os.environ.get("GITHUB_TOKEN")
    
    headers = {
        'Authorization': f'token {github_token}',
        'Accept': 'application/vnd.github.v3.raw'
    }
    
    # GitHub API URL to get raw file content
    url = f"https://api.github.com/repos/{repo}/contents/{file_path}"
    
    try:
        response = requests.get(url, headers=headers, timeout=30)
        
        if response.status_code == 200:
            # Get the first few lines to check for the header
            content = response.text
            first_lines = content.split('\n')[:10]  # Check first 10 lines
            
            # Look for the specific generated code header
            generated_header = "Code generated by Microsoft (R) Python Code Generator."
            
            for line in first_lines:
                if generated_header in line:
                    return {
                        'file_path': file_path,
                        'is_generated': True,
                        'status': 'success',
                        'message': 'File is generated by Microsoft (R) Python Code Generator'
                    }
            
            return {
                'file_path': file_path,
                'is_generated': False,
                'status': 'success',
                'message': 'File is handwritten (no generation header found)'
            }
            
        elif response.status_code == 404:
            return {
                'file_path': file_path,
                'is_generated': None,
                'status': 'not_found',
                'message': 'File not found in repository'
            }
        else:
            return {
                'file_path': file_path,
                'is_generated': None,
                'status': 'error',
                'message': f"HTTP {response.status_code}: {response.text}"
            }
            
    except Exception as e:
        return {
            'file_path': file_path,
            'is_generated': None,
            'status': 'exception',
            'message': str(e)
        }

def batch_check_generation_status(file_checks: List[Dict], max_workers=3, delay_between_requests=1.0):
    """
    Check generation status for multiple files concurrently.
    
    Args:
        file_checks: List of dicts with 'repo' and 'file_path' keys
        max_workers: Maximum number of concurrent requests
        delay_between_requests: Delay in seconds between requests
    
    Returns:
        List of generation status results
    """
    results = []
    
    def check_with_delay(file_info):
        time.sleep(delay_between_requests)
        return check_file_generation_status(
            repo=file_info['repo'],
            file_path=file_info['file_path']
        )
    
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_to_file = {
            executor.submit(check_with_delay, file_info): file_info
            for file_info in file_checks
        }
        
        for future in as_completed(future_to_file):
            file_info = future_to_file[future]
            try:
                result = future.result()
                results.append(result)
                status_msg = "✓ Generated" if result['is_generated'] else "✓ Handwritten" if result['is_generated'] is False else "✗ Error"
                print(f"{status_msg}: {file_info['file_path']}")
            except Exception as exc:
                print(f"✗ Error checking {file_info['file_path']}: {exc}")
                results.append({
                    'file_path': file_info['file_path'],
                    'is_generated': None,
                    'status': 'exception',
                    'message': str(exc)
                })
    
    return results

def enhance_search_results_with_generation_check(search_results: List[Dict]):
    """
    Enhance search results by checking if found files are generated or handwritten.
    
    Args:
        search_results: Results from github_batch_search
    
    Returns:
        Enhanced results with generation status information
    """
    file_checks = []
    
    # Extract file paths from successful search results
    for result in search_results:
        if result['status'] == 'success' and result['data'].get('items'):
            for item in result['data']['items']:
                repo_name = item['repository']['full_name']
                file_path = item['path']
                file_checks.append({
                    'repo': repo_name,
                    'file_path': file_path,
                    'search_query': result['query']
                })
    
    if not file_checks:
        return search_results
    
    print(f"\nChecking generation status for {len(file_checks)} files...")
    generation_results = batch_check_generation_status(file_checks)
    
    # Create a lookup dictionary for generation results
    generation_lookup = {
        f"{result.get('file_path', '')}": result
        for result in generation_results
    }
    
    # Enhance the original search results
    enhanced_results = []
    for result in search_results:
        enhanced_result = result.copy()
        
        if result['status'] == 'success' and result['data'].get('items'):
            enhanced_items = []
            for item in result['data']['items']:
                enhanced_item = item.copy()
                file_path = item['path']
                
                if file_path in generation_lookup:
                    gen_info = generation_lookup[file_path]
                    enhanced_item['generation_status'] = {
                        'is_generated': gen_info['is_generated'],
                        'status': gen_info['status'],
                        'message': gen_info['message']
                    }
                
                enhanced_items.append(enhanced_item)
            
            enhanced_result['data'] = enhanced_result['data'].copy()
            enhanced_result['data']['items'] = enhanced_items
        
        enhanced_results.append(enhanced_result)
    
    return enhanced_results

def ai_lineid_definition_search(
    line_id: str,
    repo: str,
    package_path: Optional[str] = None,
    model: str = "gpt-4-1106-preview",
    max_results: int = 5
) -> Dict:
    """
    Use Azure OpenAI to search for the actual method definition
    for a given LineID in a GitHub repository.
    The LineID is treated as a logical identifier, not a literal code string.
    """
    try:
        azure_endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
        azure_api_key = os.environ.get("AZURE_OPENAI_API_KEY")
        azure_deployment = os.environ.get("AZURE_OPENAI_DEPLOYMENT")
        azure_api_version = os.environ.get("AZURE_OPENAI_API_VERSION")
        if not all([azure_endpoint, azure_api_key, azure_deployment]):
            return {
                'line_id': line_id,
                'status': 'missing_azure_config',
                'error': 'Azure OpenAI configuration missing. Required: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT',
                'ai_analysis': None
            }
        client = AzureOpenAI(
            azure_endpoint=azure_endpoint,
            api_key=azure_api_key,
            api_version=azure_api_version or "2024-02-15-preview"
        )
        # Flexible query construction: always use full LineID, plus last/second-to-last parts as fallback
        queries = [line_id]
        parts = line_id.split('.')
        if len(parts) > 1:
            queries.append(parts[-1])  # Most specific part (e.g., method, member, param, etc.)
        if len(parts) > 2:
            queries.append(parts[-2])  # Likely class/enum name
        # Add package path if available
        if package_path:
            queries = [q + f" path:{package_path}" for q in queries]
        github_results = None
        items = []
        for q in queries:
            github_results = github_code_search(
                query=q,
                repo=repo,
                per_page=max_results * 2
            )
            if github_results['status'] == 'success' and github_results['data'].get('items'):
                items = github_results['data']['items']
                break  # Use first successful non-empty result
        if not github_results or github_results['status'] != 'success':
            return {
                'line_id': line_id,
                'status': 'github_search_failed',
                'error': github_results.get('error') if github_results else 'No search attempted',
                'ai_analysis': None
            }
        if not items:
            return {
                'line_id': line_id,
                'status': 'no_results',
                'error': 'No files found in GitHub search',
                'ai_analysis': None
            }
        # Build the search context for AI (concise)
        search_context = f"LineID: {line_id}\nRepo: {repo}"
        if package_path:
            search_context += f"\nPath: {package_path}"
        code_analysis_prompt = f"""
{search_context}

The LineID is a logical identifier for a code element. It becomes more specific from left to right (e.g., namespace → Class → method → param/ivar).

Your task: Given the GitHub code search results, identify the file that contains the actual definition (not just a reference or usage) of the symbol described by the LineID.

How to identify definitions:
- For classes/enums: look for 'class ClassName' or 'class EnumName'
- For methods/functions: look for 'def method_name(' or 'def function_name('
- For enum members: look for 'MEMBER =' in enum classes
- For params/ivars: look for the parameter or variable in the relevant class/method definition
- Ignore: imports, calls, references, or documentation mentions

GitHub Search Results:"""
        for i, item in enumerate(items[:max_results]):
            code_analysis_prompt += f"""
Result {i+1}:
- File: {item['path']}
- Repo: {item['repository']['full_name']}
- Score: {item['score']}
- URL: {item['html_url']}"""
        code_analysis_prompt += """

Based on these results, return only JSON in this format:
{
    "best_match": {
        "file_path": "path/to/definition/file.py",
        "confidence_score": 95,
        "definition_type": "enum|function|method|class|constant|property|typed_dict_key|ivar",
        "reasoning": "Why this is the definition"
    },
    "alternative_matches": [
        {
            "file_path": "path/to/alternative/file.py",
            "confidence_score": 70,
            "definition_type": "reference|usage",
            "reasoning": "Brief explanation"
        }
    ],
    "analysis_summary": "Short summary of the search results"
}
Respond with ONLY the JSON, no extra text.
"""
        try:
            response = client.chat.completions.create(
                model=azure_deployment,
                messages=[
                    {
                        "role": "system",
                        "content": "You are an expert code analyst. Analyze the provided GitHub search results and return only valid JSON."
                    },
                    {
                        "role": "user",
                        "content": code_analysis_prompt
                    }
                ],
                temperature=0.1,
                max_tokens=1000
            )
            ai_response = response.choices[0].message.content or ""
            try:
                json_start = ai_response.find('{')
                json_end = ai_response.rfind('}') + 1
                if json_start >= 0 and json_end > json_start:
                    json_text = ai_response[json_start:json_end]
                    ai_analysis = json.loads(json_text)
                else:
                    ai_analysis = {
                        "analysis_summary": ai_response,
                        "best_match": None,
                        "alternative_matches": []
                    }
            except json.JSONDecodeError:
                ai_analysis = {
                    "analysis_summary": ai_response,
                    "best_match": None,
                    "alternative_matches": [],
                    "parse_error": "Failed to parse AI response as JSON"
                }
        except Exception as e:
            logger.error("Error in Azure OpenAI call: %s", str(e))
            ai_analysis = {
                "analysis_summary": f"Azure OpenAI error: {str(e)}",
                "best_match": None,
                "alternative_matches": [],
                "parse_error": "Azure OpenAI call failed"
            }
        return {
            'line_id': line_id,
            'status': 'success',
            'github_results': github_results,
            'ai_analysis': ai_analysis,
            'total_files_analyzed': len(items),
            'search_method': 'azure_openai_ai'
        }
    except Exception as e:
        logger.error("Error in ai_lineid_definition_search: %s", str(e))
        return {
            'line_id': line_id,
            'status': 'error',
            'error': str(e),
            'ai_analysis': None,
            'search_method': 'azure_openai_ai'
        }

def hybrid_method_search(
    line_id: str,
    repo: str,
    package_path: Optional[str] = None,
    use_ai: bool = True,
    max_results: int = 5
) -> Dict:
    """
    Hybrid search that combines traditional GitHub code search with AI-powered analysis using LineID.
    
    Args:
        line_id: The full LineID to search for
        repo: Repository name in format "owner/repo"
        package_path: Optional package path to limit search scope
        use_ai: Whether to use AI analysis (defaults to True)
        max_results: Maximum number of results to return
    
    Returns:
        Dict containing both traditional and AI-enhanced search results
    """
    results: Dict = {
        'line_id': line_id,
        'repo': repo,
        'search_timestamp': time.time(),
        'github_search': None,
        'ai_search': None,
        'recommendation': None
    }
    
    try:
        if use_ai:
            with ThreadPoolExecutor(max_workers=2) as executor:
                github_future = executor.submit(
                    github_code_search,
                    query=line_id + (f" path:{package_path}" if package_path else ""),
                    repo=repo,
                    per_page=max_results
                )
                ai_future = executor.submit(
                    ai_lineid_definition_search,
                    line_id=line_id,
                    repo=repo,
                    package_path=package_path,
                    max_results=max_results
                )
                results['github_search'] = github_future.result()
                results['ai_search'] = ai_future.result()
        else:
            results['github_search'] = github_code_search(
                query=line_id + (f" path:{package_path}" if package_path else ""),
                repo=repo,
                per_page=max_results
            )
        github_search_results = results.get('github_search')
        ai_search_results = results.get('ai_search')
        results['recommendation'] = _generate_search_recommendation(
            github_search_results if isinstance(github_search_results, dict) else None,
            ai_search_results if isinstance(ai_search_results, dict) else None
        )
        return results
    except Exception as e:
        logger.error("Error in hybrid_method_search: %s", str(e))
        results['error'] = str(e)
        return results

def _generate_search_recommendation(github_results: Optional[Dict], ai_results: Optional[Dict]) -> Dict:
    """
    Generate a recommendation based on both GitHub and AI search results.
    """
    recommendation = {
        'method': 'unknown',
        'confidence': 0,
        'file_path': None,
        'reasoning': 'No results available'
    }
    # If AI search succeeded and found a high-confidence match
    if (ai_results and isinstance(ai_results, dict) and ai_results.get('status') == 'success' and 
        ai_results.get('ai_analysis', {}).get('best_match')):
        best_match = ai_results['ai_analysis']['best_match']
        confidence = best_match.get('confidence_score', 0)
        if confidence >= 80:
            recommendation.update({
                'method': 'ai_high_confidence',
                'confidence': confidence,
                'file_path': best_match.get('file_path'),
                'reasoning': f"AI analysis with {confidence}% confidence: {best_match.get('reasoning', '')}"
            })
            return recommendation
    # Fall back to GitHub search if AI didn't provide high confidence
    if github_results and isinstance(github_results, dict) and github_results.get('status') == 'success':
        items = github_results.get('data', {}).get('items', [])
        if items:
            # Use the highest scored result from GitHub
            best_item = max(items, key=lambda x: x.get('score', 0))
            recommendation.update({
                'method': 'github_top_result',
                'confidence': min(best_item.get('score', 0) * 10, 75),  # Scale score to percentage, cap at 75%
                'file_path': best_item.get('path'),
                'reasoning': f"Top GitHub search result with score {best_item.get('score', 0)}"
            })
            return recommendation
    # If AI had some results but low confidence
    if (ai_results and isinstance(ai_results, dict) and ai_results.get('status') == 'success' and 
        ai_results.get('ai_analysis', {}).get('best_match')):
        best_match = ai_results['ai_analysis']['best_match']
        recommendation.update({
            'method': 'ai_low_confidence',
            'confidence': best_match.get('confidence_score', 0),
            'file_path': best_match.get('file_path'),
            'reasoning': f"AI analysis with lower confidence: {best_match.get('reasoning', '')}"
        })
    return recommendation

def batch_hybrid_search(search_requests: List[Dict], use_ai: bool = True, max_workers: int = 3) -> List[Dict]:
    """
    Perform hybrid search for multiple LineIDs concurrently.
    
    Args:
        search_requests: List of dicts with 'line_id', 'repo', 'package_path' keys
        use_ai: Whether to use AI analysis
        max_workers: Maximum number of concurrent searches
    
    Returns:
        List of hybrid search results
    """
    results = []
    
    def search_single(request):
        return hybrid_method_search(
            line_id=request['line_id'],
            repo=request['repo'],
            package_path=request.get('package_path'),
            use_ai=use_ai,
            max_results=request.get('max_results', 5)
        )
    
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_to_request = {
            executor.submit(search_single, request): request
            for request in search_requests
        }
        
        for future in as_completed(future_to_request):
            request = future_to_request[future]
            try:
                result = future.result()
                results.append(result)
                print(f"✓ Completed hybrid search for: {request['line_id']}")
            except Exception as exc:
                print(f"✗ Error in hybrid search for {request['line_id']}: {exc}")
                results.append({
                    'line_id': request['line_id'],
                    'status': 'exception',
                    'error': str(exc)
                })
    
    return results

def search_apiview_comments_with_ai(comments_data: Dict, use_ai: bool = True) -> List[Dict]:
    """
    Enhanced version of search_apiview_comments that uses AI-powered search.
    
    Args:
        comments_data: Dictionary containing package and comments information
        use_ai: Whether to use AI analysis (defaults to True)
    
    Returns:
        List of hybrid search results for each LineID
    """
    package_path = comments_data.get('package_path', '')
    repo = "Azure/azure-sdk-for-python"  # Assuming Python SDK
    
    search_requests = []
    for comment in comments_data.get('comments', []):
        line_id = comment.get('LineID', '')
        
        search_requests.append({
            'line_id': line_id,
            'repo': repo,
            'package_path': package_path,
            'max_results': 5
        })
    
    return batch_hybrid_search(search_requests, use_ai=use_ai)

def find_all_patch_py_files(base_path: str) -> List[str]:
    """
    Recursively locate all _patch.py files under the given base path.
    Returns a list of absolute file paths.
    """
    patch_files = []
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file.endswith('_patch.py'):
                patch_files.append(os.path.join(root, file))
    return patch_files

# Example usage
#if __name__ == "__main__":
#    # Example 1: Traditional GitHub search (legacy approach)
#    queries = [
#        {
#            'query': 'SchemaContentTypeValues.JSON',
#            'repo': 'Azure/azure-sdk-for-python',
#            'path': 'sdk/schemaregistry/azure-schemaregistry'
#        },
#        {
#            'query': 'SchemaFormat.JSON',
#            'repo': 'Azure/azure-sdk-for-python',
#            'path': 'sdk/schemaregistry/azure-schemaregistry'
#        },
#        {
#            'query': 'JsonSchemaEncoder.decode',
#            'repo': 'Azure/azure-sdk-for-python',
#            'path': 'sdk/schemaregistry/azure-schemaregistry'
#        }
#    ]
#    
#    print("Running traditional batch search...")
#    results = github_batch_search(queries, max_workers=2, delay_between_requests=1.5)
#    enhanced_results = enhance_search_results_with_generation_check(results)
#    process_search_results(enhanced_results)
#    
#    print("\n" + "="*80)
#    print("EXAMPLE 2: AI-POWERED HYBRID SEARCH (RECOMMENDED)")
#    print("="*80)
#    
#    # Example 2: AI-powered hybrid search for method definitions
#    search_requests = [
#        {
#            'symbol': 'JSON',
#            'repo': 'Azure/azure-sdk-for-python',
#            'package_path': 'sdk/schemaregistry/azure-schemaregistry',
#            'line_id': 'azure.schemaregistry.models.SchemaContentTypeValues.JSON'
#        },
#        {
#            'query': 'SchemaFormat.JSON',
#            'repo': 'Azure/azure-sdk-for-python',
#            'package_path': 'sdk/schemaregistry/azure-schemaregistry',
#            'line_id': 'azure.schemaregistry.models.SchemaFormat.JSON'
#        },
#        {
#            'query': 'decode',
#            'repo': 'Azure/azure-sdk-for-python',
#            'package_path': 'sdk/schemaregistry/azure-schemaregistry',
#            'line_id': 'azure.schemaregistry.JsonSchemaEncoder.decode'
#        }
#    ]
#    
#    print("Running AI-powered hybrid search...")
#    hybrid_results = batch_hybrid_search(search_requests, use_ai=True, max_workers=2)
#    
#    for result in hybrid_results:
#        print(f"\n{'='*60}")
#        print(f"Symbol: {result['symbol']}")
#        print(f"Repository: {result['repo']}")
#        
#        recommendation = result.get('recommendation', {})
#        print(f"🎯 RECOMMENDATION:")
#        print(f"   Method: {recommendation.get('method', 'unknown')}")
#        print(f"   Confidence: {recommendation.get('confidence', 0)}%")
#        print(f"   File: {recommendation.get('file_path', 'N/A')}")
#        print(f"   Reasoning: {recommendation.get('reasoning', 'N/A')}")
#        
#        # Show AI analysis details if available
#        ai_results = result.get('ai_search', {})
#        if ai_results and ai_results.get('ai_analysis'):
#            ai_analysis = ai_results['ai_analysis']
#            if ai_analysis.get('best_match'):
#                best_match = ai_analysis['best_match']
#                print(f"\n🤖 AI ANALYSIS:")
#                print(f"   Definition Type: {best_match.get('definition_type', 'unknown')}")
#                print(f"   AI Reasoning: {best_match.get('reasoning', 'N/A')}")
#    
#    print("\n" + "="*80)
#    print("EXAMPLE 3: Enhanced APIView Comments Search with AI")
#    print("="*80)
#    
#    # Example 3: Using APIView comments data structure with AI
#    sample_comments = {
#        "package": "azure-schemaregistry",
#        "branch": "main",
#        "comments": [
#            {
#                "LineID": "azure.schemaregistry.SchemaFormat.JSON",
#                "Line": "JSON = \"Json\"",
#                "Comment": "Should include PROTOBUF and APPLEPIE."
#            },
#            {
#                "LineID": "azure.schemaregistry.models.SchemaContentTypeValues.PROTOBUF",
#                "Line": "PROTOBUF = \"text/vnd.ms.protobuf\"",
#                "Comment": "Missing additional schema type APPLEPIE."
#            },
#            {
#                "LineID": "azure.schemaregistry.models.SchemaContentTypeValues.JSON",
#                "Line": "JSON = \"application/json; serialization=Json\"",
#                "Comment": "Need to add schema type APPLEPIE."
#            }
#        ]
#    }
#    
#    print("Note: For production use, load comments from JSON file using:")
#    print("  from run_pr_generation import load_comments_json")
#    print("  comments_data = load_comments_json('your_comments.json')")
#    print()
#    
#    # Compare traditional vs AI-powered search
#    print("Traditional search results:")
#    traditional_results = search_apiview_comments(sample_comments)
#    enhanced_traditional = enhance_search_results_with_generation_check(traditional_results)
#    process_search_results(enhanced_traditional)
#    
#    print("\nAI-powered search results:")
#    ai_enhanced_results = search_apiview_comments_with_ai(sample_comments, use_ai=True)
#    for result in ai_enhanced_results:
#        symbol = result['symbol']
#        recommendation = result.get('recommendation', {})
#        print(f"🎯 {symbol}: {recommendation.get('file_path', 'N/A')} "
#              f"(confidence: {recommendation.get('confidence', 0)}%)")
#    
#    print("\n" + "="*80)
#    print("EXAMPLE 4: Direct generation status check")
#    print("="*80)
#    
#    # Example 4: Direct file generation check (unchanged)
#    file_checks = [
#        {
#            'repo': 'Azure/azure-sdk-for-python',
#            'file_path': 'sdk/schemaregistry/azure-schemaregistry/azure/schemaregistry/models/_enums.py'
#        },
#        {
#            'repo': 'Azure/azure-sdk-for-python', 
#            'file_path': 'sdk/schemaregistry/azure-schemaregistry/azure/schemaregistry/_patch.py'
#        }
#    ]
#    
#    generation_results = batch_check_generation_status(file_checks)
#    
#    print("\nGeneration Status Results:")
#    for result in generation_results:
#        status = "🤖 GENERATED" if result['is_generated'] else "✍️ HANDWRITTEN" if result['is_generated'] is False else "❓ UNKNOWN"
#        print(f"{status}: {result['file_path']}")
#        print(f"  Message: {result['message']}")
#        print()
#    
#    print("\n" + "="*80)
#    print("EXAMPLE 5: Single AI-powered method search")
#    print("="*80)
#    
#    # Example 5: Single method search using AI
#    single_result = semantic_kernel_method_search(
#        line="JSON",
#        repo="Azure/azure-sdk-for-python",
#        package_path="sdk/schemaregistry/azure-schemaregistry",
#        line_id="azure.schemaregistry.models.SchemaContentTypeValues.JSON"
#    )
#    
#    print(f"AI Search Result for 'JSON':")
#    print(f"Status: {single_result['status']}")
#    if single_result.get('ai_analysis'):
#        analysis = single_result['ai_analysis']
#        print(f"Analysis Summary: {analysis.get('analysis_summary', 'N/A')}")
#        if analysis.get('best_match'):
#            best = analysis['best_match']
#            print(f"Best Match: {best.get('file_path', 'N/A')} "
#                  f"(confidence: {best.get('confidence_score', 0)}%)")
#            print(f"Type: {best.get('definition_type', 'unknown')}")
#            print(f"Reasoning: {best.get('reasoning', 'N/A')}")