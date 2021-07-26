from setuptools import setup, find_packages
import os, re

PACKAGE_NAME = "protocol-stub-generator"

DESCRIPTION = (
    "A llc stub generator for published APIs, variables and properties in a package"
)

with open(os.path.join("protocol", "_version.py"), "r") as fd:
    version = re.search(
        r'^VERSION\s*=\s*[\'"]([^\'"]*)[\'"]', fd.read(), re.MULTILINE
    ).group(1)

if not version:
    raise RuntimeError("Cannot find version information")

with open("README.md") as f:
    long_description = f.read()

setup(
    name=PACKAGE_NAME,
    description=DESCRIPTION,
    version=version,
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/Azure/azure-sdk-tools/",
    author="Microsoft Corporation",
    author_email="azuresdkengsysadmins@microsoft.com",
    license="MIT License",
    packages=find_packages(),
    install_requires=["astroid","pyyaml","jinja2"],
    python_requires=">=3.4.0",
    entry_points={
        "console_scripts": [
            "protocolGen=protocol:console_entry_point",
        ]
    },
    classifiers=[
        "Development Status :: 3 - Alpha",
        "Programming Language :: Python",
        "Programming Language :: Python :: 3.5",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "License :: OSI Approved :: MIT License",
    ],
)
