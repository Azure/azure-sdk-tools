# Parse TypeSpec config YAML files to extract language-specific metadata
import sys
import os
import yaml
import re

def _fill_vars(s, data):
    """Replace {var} in string s with value from data dict, recursively."""
    if not isinstance(s, str):
        return s
    def replacer(match):
        key = match.group(1)
        val = data.get(key)
        return str(val) if val is not None else match.group(0)
    prev = None
    while prev != s:
        prev = s
        s = re.sub(r'\{([^{}]+)\}', replacer, s)
    return s

def _parse_python(data):
    package_name = data.get('package-name') or data.get('package_name')
    namespace = data.get('namespace')
    if package_name and not namespace:
        namespace = package_name.replace('-', '.')
    elif namespace and not package_name:
        package_name = namespace.replace('.', '-')
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def _parse_java(data):
    package_name = data.get('package-name') or data.get('package_name')
    namespace = data.get('namespace')
    if namespace and namespace.startswith('com.'):
        ns = namespace[4:]
    else:
        ns = namespace
    if not package_name:
        package_name = ns.replace('.', '-') if ns else None
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def _parse_csharp(data):
    package_name = data.get('package-name') or data.get('package_name')
    namespace = data.get('namespace')
    if package_name and not namespace:
        namespace = package_name
    elif namespace and not package_name:
        package_name = namespace
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def _parse_typescript(data):
    package_details = data.get('package-details', {})
    package_name = package_details.get('name')
    namespace = package_details.get('namespace')
    if package_name and not namespace:
        namespace = package_name
    elif namespace and not package_name:
        package_name = namespace
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def _parse_go(data):
    package_name = data.get('package-name') or data.get('package_name')
    namespace = data.get('namespace')
    mod = _fill_vars(data.get('module'), data)
    after = mod.split('azure-sdk-for-go/', 1)[-1] if mod and 'azure-sdk-for-go/' in mod else mod
    if not package_name:
        package_name = after
    if not namespace:
        namespace = after
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def _parse_rust(data):
    package_name = data.get('crate-name')
    namespace = data.get('namespace')
    if package_name and not namespace:
        namespace = package_name
    elif namespace and not package_name:
        package_name = namespace
    package_name = _fill_vars(package_name, data)
    namespace = _fill_vars(namespace, data)
    return package_name, namespace

def parse_language_metadata(yaml_data):
    """
    Extract language, package name, and namespace from a TypeSpec config dict.
    Returns a list of dicts: [{language, package_name, namespace}]
    """
    result = []
    options = yaml_data.get('options', {})
    # Gather parameters for curly-brace substitution
    params = {}
    if 'parameters' in yaml_data:
        for k, v in yaml_data['parameters'].items():
            if isinstance(v, dict) and 'default' in v:
                params[k] = v['default']
            else:
                params[k] = v
    lang_map = {
        '@azure-tools/typespec-python': ('python', _parse_python),
        '@azure-tools/typespec-csharp': ('csharp', _parse_csharp),
        '@azure-tools/typespec-java': ('java', _parse_java),
        '@azure-tools/typespec-ts': ('typescript', _parse_typescript),
        '@azure-tools/typespec-go': ('go', _parse_go),
        '@azure-tools/typespec-rust': ('rust', _parse_rust),
    }
    for key, (lang, extractor) in lang_map.items():
        if key in options:
            lang_opts = dict(options[key])
            lang_opts['_params'] = params
            package_name, namespace = extractor(lang_opts)
            if namespace or package_name:
                result.append({
                    'language': lang,
                    'package_name': package_name,
                    'namespace': namespace
                })
    return result

def parse_yaml_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)

def main():
    if len(sys.argv) < 2:
        print('Usage: python parse_tspconfig.py <yaml-file1> [<yaml-file2> ...]')
        sys.exit(1)
    for yaml_path in sys.argv[1:]:
        if not os.path.isfile(yaml_path):
            print(f"File not found: {yaml_path}")
            continue
        data = parse_yaml_file(yaml_path)
        meta = parse_language_metadata(data)
        print(f"--- {os.path.basename(yaml_path)} ---")
        for entry in meta:
            print(entry)

if __name__ == '__main__':
    main()
