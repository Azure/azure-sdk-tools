from os import path
import yaml
import glob
import json
import re
from typing import Optional, Tuple


def resolve_example_path(specs_path: str, sdk_package_path: str, reference: str) -> Tuple[str, str]:
    reference = reference.replace("\\", "/")
    source, separator, example_id = reference.partition("#")
    if not source.endswith((".yaml", ".yml")):
        example_dir, example_filename = path.split(reference)
        return (
            try_find_resource_manager_example(specs_path, sdk_package_path, example_dir, example_filename),
            example_filename,
        )

    if not separator or not example_id:
        raise RuntimeError(f"Unified example reference requires an explicit #id: {reference}")
    _check_relative_path(source)
    _check_relative_path(example_id)
    tsp_dir = _read_tsp_directory(sdk_package_path)
    project_root = path.join(specs_path, tsp_dir)
    yaml_path = path.join(project_root, source)
    if not path.isfile(yaml_path):
        raise RuntimeError(f"Example YAML file not found at path {yaml_path}")
    with open(yaml_path, encoding="utf-8") as fin:
        examples = yaml.safe_load(fin)

    api_versions = {}
    metadata_patterns = [
        "metadata.json",
        "_metadata.json",
        "testdata/_metadata.json",
        "src/main/resources/META-INF/*_metadata.json",
    ]
    for pattern in metadata_patterns:
        for metadata_path in glob.glob(path.join(sdk_package_path, pattern)):
            with open(metadata_path, encoding="utf-8") as fin:
                metadata = json.load(fin)
            for service, version in metadata.get("apiVersions", {}).items():
                if not isinstance(version, str) or not version or version in ("all", "latest"):
                    raise RuntimeError(f"No concrete API version for {service} in {metadata_path}")
                if service in api_versions and api_versions[service] != version:
                    raise RuntimeError(f"Conflicting API versions for {service} in SDK metadata")
                api_versions[service] = version
    if not api_versions:
        raise RuntimeError(f"API-version metadata not found in SDK package folder {sdk_package_path}")

    source_parts = source.split("/")
    examples_index = (
        source_parts.index("examples") if "examples" in source_parts else len(source_parts) - 1
    )
    service_parts = source_parts[:examples_index]
    namespace = examples.get("$namespace") if isinstance(examples, dict) else None
    if namespace:
        services = [namespace] if namespace in api_versions else []
    elif service_parts:
        services = [service for service in api_versions if service.split(".")[-1] == service_parts[-1]]
    else:
        services = list(api_versions)
    if len(services) != 1:
        raise RuntimeError(f"Cannot uniquely match example {reference} to an API-version metadata service")
    api_version = api_versions[services[0]]
    _check_relative_path(api_version)
    service_root = path.join(project_root, *service_parts)
    example_path = _find_swagger_example(
        specs_path, tsp_dir, api_version, example_id + ".json", service_root
    )
    if example_path is None:
        raise RuntimeError(f"Swagger example not found for {reference} at API version {api_version}")
    return path.split(path.relpath(example_path, specs_path).replace("\\", "/"))


def _check_relative_path(value: str):
    if (
        not value
        or value.startswith("/")
        or "\\" in value
        or any(part in ("", ".", "..") for part in value.split("/"))
        or re.search(r'[:"*?<>|]+', value)
    ):
        raise RuntimeError(f"Invalid relative example path {value}")


def _read_tsp_directory(sdk_package_path: str) -> str:
    tsp_location_path = path.join(sdk_package_path, "tsp-location.yaml")
    if not path.isfile(tsp_location_path):
        raise RuntimeError(f"tsp-location.yaml not found in SDK package folder {sdk_package_path}")
    with open(tsp_location_path, encoding="utf-8") as fin:
        tsp_dir = yaml.safe_load(fin).get("directory")
    if not tsp_dir:
        raise RuntimeError("'directory' property not found in tsp-location.yaml")
    tsp_dir = tsp_dir.replace("\\", "/")
    _check_relative_path(tsp_dir)
    return tsp_dir


def _find_swagger_example(
    specs_path: str, tsp_dir: str, api_version: str, relative_file: str, service_root: str
) -> Optional[str]:
    project_root = path.join(specs_path, tsp_dir)
    scopes = [(service_root, True)]
    if path.normpath(service_root) != path.normpath(project_root):
        # Combined services can share the emitted Swagger directory above their source folders.
        scopes.append((project_root, False))
    if "/resource-manager/" not in tsp_dir and "/data-plane/" not in tsp_dir:
        service_dir = "/".join(tsp_dir.split("/")[:2])
        scopes.append((path.join(specs_path, service_dir, "resource-manager"), True))

    for root, recursive in scopes:
        candidates = set()
        for status in ("stable", "preview"):
            pattern = path.join(
                glob.escape(root),
                "**" if recursive else "",
                status,
                glob.escape(api_version),
                "examples",
                glob.escape(relative_file),
            )
            candidates.update(
                file for file in glob.glob(pattern, recursive=recursive) if path.isfile(file)
            )
        if len(candidates) > 1:
            raise RuntimeError(
                f"Ambiguous Swagger example for {relative_file} at API version {api_version}: "
                + ", ".join(sorted(candidates))
            )
        if candidates:
            return candidates.pop()
    return None


def try_find_resource_manager_example(
    specs_path: str, sdk_package_path: str, example_dir: str, example_filename: str
) -> str:
    if "/resource-manager/" not in example_dir:
        # find the corresponding example file under {specs_path}/specification/{service}/resource-manager
        tsp_dir = _read_tsp_directory(sdk_package_path)
        project_root = path.join(specs_path, tsp_dir)
        example_search_glob = f"{glob.escape(project_root)}/**/examples/{example_dir}/{example_filename}"
        example_paths = glob.glob(example_search_glob, recursive=True)
        match = None
        if example_paths:
            example_path = example_paths[0]
            example_dir = path.dirname(path.relpath(example_path, specs_path).replace("\\", "/"))
            match = re.match(r"specification/([^/]+)/.*/examples/([^/]+)(.*)", example_dir)
            if match:
                api_version = match.group(2)
                relative_file = match.group(3).lstrip("/") + "/" + example_filename
                relative_file = relative_file.lstrip("/")
        else:
            reference = (example_dir + "/" + example_filename).replace("\\", "/")
            _check_relative_path(reference)
            api_version, _, relative_file = reference.partition("/")

        if not example_paths or match:
            swagger_example = _find_swagger_example(
                specs_path, tsp_dir, api_version, relative_file, project_root
            )
            if swagger_example:
                example_dir = path.dirname(path.relpath(swagger_example, specs_path).replace("\\", "/"))
            elif not example_paths:
                raise RuntimeError(
                    f"Example file not found at path {example_search_glob} or in Swagger output"
                )

    example_dir = example_dir.replace("\\", "/")
    if re.search(r"[:\"*?<>|]+", example_dir):
        # invalid character in windows path < > : " | ? *
        raise RuntimeError(f"Example directory contains invalid character {example_dir}")

    return example_dir
