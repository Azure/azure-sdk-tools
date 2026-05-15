#!/usr/bin/env python3
"""
Execute KQL queries against a Kusto cluster and output results in various formats.

Usage:
    python kusto-query-executor.py --cluster <url> --database <db> --query <kql> \
        --output-format {json|csv|tsv} [--output-file <path>]

Environment Variables:
    KUSTO_CLIENT_ID: Service principal client ID (optional)
    KUSTO_CLIENT_SECRET: Service principal client secret (optional)
    KUSTO_TENANT_ID: Azure tenant ID (optional)
    If not set, uses DefaultAzureCredential for authentication.
"""

import argparse
import csv
import json
import os
import sys
from io import StringIO
from pathlib import Path

from azure.kusto.data import KustoClient, KustoConnectionStringBuilder
from azure.kusto.data.exceptions import KustoServiceError
import re


def is_query_safe(query):
    """
    Validate that a KQL query is read-only and not destructive.
    
    Rejects queries that:
    - Modify data (INSERT, UPDATE, DELETE, SET)
    - Alter structure (CREATE, DROP, ALTER, RENAME)
    - Manage access (GRANT, DENY, REVOKE)
    - Execute arbitrary commands (execute, execute_and_cache)
    
    Args:
        query: KQL query string
        
    Returns:
        Tuple of (is_safe, error_message)
        
    Raises:
        ValueError: If query contains destructive operations.
    """
    # Normalize query: remove comments and convert to uppercase for checking
    query_upper = query.strip().upper()
    
    # Remove single-line comments
    query_upper = re.sub(r'//.*$', '', query_upper, flags=re.MULTILINE)
    
    # Remove multi-line comments
    query_upper = re.sub(r'/\*.*?\*/', '', query_upper, flags=re.DOTALL)
    
    # List of destructive KQL operations that should be blocked
    destructive_patterns = [
        r'\b(INSERT|UPDATE|DELETE|SET|CREATE|DROP|ALTER|RENAME)\b',
        r'\b(GRANT|DENY|REVOKE)\b',
        r'\b(EXECUTE|EXECUTE_AND_CACHE)\b',
        r'\.ingest\b',  # Ingestion operations
        r'\.append\b',  # Data append
        r'\.set\b',     # Data set
        r'\.set-or-append\b',  # Data set or append
        r'\.set-or-replace\b', # Data set or replace
    ]
    
    for pattern in destructive_patterns:
        if re.search(pattern, query_upper):
            match = re.search(pattern, query_upper)
            operation = match.group(0) if match else "unknown"
            raise ValueError(
                f"Destructive operation '{operation}' is not allowed. "
                f"Only read-only queries (SELECT, etc.) are permitted."
            )
    
    # Ensure query starts with a read-only operation
    # Allow: SELECT, LET, DATATABLE, bare table names, and bracketed references
    if not re.match(r'^\s*(select|let|datatable|print)', query_upper):
        # Allow bare table references (identifiers, qualified names, bracketed names)
        # Examples: MyTable | ..., [external] | ..., database.table | ..., [db].table | ...
        if not re.match(r'^\s*(?:\w+|`[^`]+`|\[[^\]]+\])(?:\s*\||$)', query_upper):
            raise ValueError(
                f"Query must be read-only. Queries must start with SELECT, LET, DATATABLE, PRINT, "
                f"or a table reference. Got: {query_upper[:50]}..."
            )
    
    return True


def get_credentials(cluster):
    """
    Build a KustoConnectionStringBuilder for authentication.
    
    Uses service principal credentials from environment variables if available,
    otherwise falls back to managed service identity authentication.
    
    Args:
        cluster: Kusto cluster URL
        
    Returns:
        KustoConnectionStringBuilder configured for the appropriate auth method.
    """
    client_id = os.getenv('KUSTO_CLIENT_ID')
    client_secret = os.getenv('KUSTO_CLIENT_SECRET')
    tenant_id = os.getenv('KUSTO_TENANT_ID')

    if client_id and client_secret and tenant_id:
        return KustoConnectionStringBuilder.with_aad_application_key_authentication(
            cluster, client_id, client_secret, tenant_id
        )
    else:
        return KustoConnectionStringBuilder.with_aad_managed_service_identity_authentication(cluster)


def execute_kusto_query(cluster, database, query):
    """
    Execute a KQL query against a Kusto cluster.
    
    Validates that the query is read-only before execution.
    
    Args:
        cluster: Kusto cluster URL (e.g., https://mycluster.kusto.windows.net)
        database: Database name
        query: KQL query string
        
    Returns:
        List of dictionaries representing query results.
        
    Raises:
        ValueError: If query contains destructive operations.
        KustoServiceError: If Kusto service returns an error.
        Exception: For other execution errors.
    """
    # Validate query is safe before execution
    is_query_safe(query)
    
    kcsb = get_credentials(cluster)
    client = KustoClient(kcsb)
    response = client.execute(database, query)
    
    # Convert response to list of dictionaries
    results = []
    for row in response.primary_table.rows:
        row_dict = {}
        for i, col in enumerate(response.primary_table.columns):
            row_dict[col.name] = row[i]
        results.append(row_dict)
    
    return results


def format_results(results, output_format):
    """
    Format query results in the specified format.
    
    Args:
        results: List of dictionaries (query results)
        output_format: Format string ('json', 'csv', or 'tsv')
        
    Returns:
        Formatted output string.
    """
    output_format = output_format.lower()
    
    if output_format == 'json':
        return json.dumps(results, indent=2, default=str)
    elif output_format == 'csv':
        if results:
            output = StringIO()
            writer = csv.DictWriter(output, fieldnames=results[0].keys())
            writer.writeheader()
            writer.writerows(results)
            return output.getvalue()
        else:
            return ""
    elif output_format == 'tsv':
        if results:
            lines = ['\t'.join(results[0].keys())]
            for row in results:
                lines.append('\t'.join(str(v) for v in row.values()))
            return '\n'.join(lines)
        else:
            return ""
    else:
        # Default to JSON
        return json.dumps(results, indent=2, default=str)


def main():
    parser = argparse.ArgumentParser(
        description='Execute KQL queries against a Kusto cluster'
    )
    parser.add_argument(
        '--cluster',
        required=True,
        help='Kusto cluster URL (e.g., https://mycluster.kusto.windows.net)'
    )
    parser.add_argument(
        '--database',
        required=True,
        help='Kusto database name'
    )
    parser.add_argument(
        '--query',
        required=True,
        help='KQL query to execute'
    )
    parser.add_argument(
        '--output-format',
        default='json',
        choices=['json', 'csv', 'tsv'],
        help='Output format (default: json)'
    )
    parser.add_argument(
        '--output-file',
        default='kusto-query-result.txt',
        help='Output file path (default: kusto-query-result.txt)'
    )
    parser.add_argument(
        '--github-output',
        help='Path to GITHUB_OUTPUT file for setting workflow outputs'
    )

    args = parser.parse_args()

    try:
        print(f"Executing Kusto query on cluster: {args.cluster}")
        print(f"Database: {args.database}")
        
        # Validate query before execution
        try:
            is_query_safe(args.query)
            print("Query validation passed (read-only confirmed)")
        except ValueError as e:
            print(f"Query validation failed: {e}", file=sys.stderr)
            return 1
        
        results = execute_kusto_query(args.cluster, args.database, args.query)
        
        print(f"Query executed successfully. Results: {len(results)} rows")
        
        # Format output
        output_content = format_results(results, args.output_format)
        
        # Write output to file
        output_path = Path(args.output_file)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w') as f:
            f.write(output_content)
        
        print(f"Results written to: {args.output_file}")
        print(output_content)
        
        # Set GitHub Actions outputs if requested
        if args.github_output:
            with open(args.github_output, 'a') as f:
                f.write(f'result={json.dumps(results, default=str)}\n')
                f.write(f'file={args.output_file}\n')
            print(f"GitHub outputs set")
        
        return 0

    except KustoServiceError as e:
        print(f"Kusto service error: {e}", file=sys.stderr)
        return 1
    except ValueError as e:
        print(f"Query validation error: {e}", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"Error executing Kusto query: {e}", file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
