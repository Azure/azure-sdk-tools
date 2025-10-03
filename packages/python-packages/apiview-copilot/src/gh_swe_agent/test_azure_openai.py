#!/usr/bin/env python3
"""
Test script to verify Azure OpenAI integration with the enhanced file_search.py
"""

import os
import sys
from pathlib import Path
from dotenv import load_dotenv

def test_azure_openai_config():
    """Test Azure OpenAI configuration"""
    print("\n🔧 Azure OpenAI Configuration:")
    print("-" * 40)
    
    required_vars = [
        'AZURE_OPENAI_ENDPOINT',
        'AZURE_OPENAI_API_KEY', 
        'AZURE_OPENAI_DEPLOYMENT',
        'GITHUB_TOKEN'
    ]
    
    all_set = True
    for var in required_vars:
        value = os.environ.get(var)
        if value:
            if 'KEY' in var or 'TOKEN' in var:
                print(f"✅ {var}: ***{value[-4:] if len(value) > 4 else '***'}")
            else:
                print(f"✅ {var}: {value}")
        else:
            print(f"❌ {var}: Not set")
            all_set = False
    
    optional_var = 'AZURE_OPENAI_API_VERSION'
    value = os.environ.get(optional_var)
    if value:
        print(f"✅ {optional_var}: {value}")
    else:
        print(f"ℹ️  {optional_var}: Not set (will use default)")
    
    return all_set

def test_import():
    """Test importing the enhanced file_search module"""
    print("\n📦 Testing Module Import:")
    print("-" * 40)
    
    try:
        from file_search import semantic_kernel_method_search, hybrid_method_search
        print("✅ Successfully imported enhanced file_search functions")
        return True
    except ImportError as e:
        print(f"❌ Failed to import file_search: {e}")
        return False

def test_simple_search():
    """Test a simple Azure OpenAI search"""
    print("\n🔍 Testing Azure OpenAI Search:")
    print("-" * 40)
    
    try:
        from file_search import semantic_kernel_method_search
        
        # Test with a simple search that should work
        result = semantic_kernel_method_search(
            symbol="JSON",
            repo="Azure/azure-sdk-for-python",
            package_path="sdk/schemaregistry/azure-schemaregistry",
            line_id="azure.schemaregistry.models.SchemaContentTypeValues.JSON"
        )
        
        print(f"Search Status: {result['status']}")
        print(f"Search Method: {result.get('search_method', 'unknown')}")
        
        if result['status'] == 'success':
            print("✅ Azure OpenAI search completed successfully!")
            
            ai_analysis = result.get('ai_analysis', {})
            if ai_analysis.get('best_match'):
                best_match = ai_analysis['best_match']
                print(f"🎯 Best Match: {best_match.get('file_path', 'N/A')}")
                print(f"   Confidence: {best_match.get('confidence_score', 0)}%")
                print(f"   Type: {best_match.get('definition_type', 'unknown')}")
            else:
                print("ℹ️  No best match found in AI analysis")
                
        elif result['status'] == 'missing_azure_config':
            print(f"❌ Azure OpenAI configuration missing: {result['error']}")
        else:
            print(f"❌ Search failed: {result.get('error', 'Unknown error')}")
            
        return result['status'] == 'success'
        
    except Exception as e:
        print(f"❌ Test failed with exception: {e}")
        return False

def main():
    print("🧪 Azure OpenAI Integration Test")
    print("=" * 50)
    
    # Load environment variables
    load_dotenv()
    
    # Test configuration
    config_ok = test_azure_openai_config()
    
    # Test imports
    import_ok = test_import()
    
    if config_ok and import_ok:
        # Test actual search
        search_ok = test_simple_search()
        
        if search_ok:
            print("\n🎉 All tests passed! Azure OpenAI integration is working.")
        else:
            print("\n⚠️  Configuration and imports OK, but search test failed.")
    else:
        print("\n⚠️  Basic configuration or import issues detected.")
        print("   Please check your .env file and dependencies.")
    
    print("\n📝 Next steps:")
    print("   - Run the full examples with: python test_ai_search.py")
    print("   - Use the enhanced search functions in your code")

if __name__ == "__main__":
    main()
