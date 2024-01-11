from setuptools import setup, find_packages
import os, re

PACKAGE_NAME = "apiview-gpt"

DESCRIPTION = (
    "A tool for generating APIView reviews using GPT-4."
)

with open(os.path.join("src", "_version.py"), "r") as fd:
    version = re.search(
        r'^VERSION\s*=\s*[\'"]([^\'"]*)[\'"]', fd.read(), re.MULTILINE
    ).group(1)

if not version:
    raise RuntimeError("Cannot find version information")

setup(
    name=PACKAGE_NAME,
    description=DESCRIPTION,
    version=version,
    long_description_content_type="text/markdown",
    url="https://github.com/Azure/azure-sdk-tools/",
    author="Microsoft Corporation",
    author_email="azuresdkengsysadmins@microsoft.com",
    license="MIT License",
    packages=find_packages(),
    package_data={'': ['pylintrc']},
    include_package_data=True,
    install_requires=[
        "astroid>=2.11",
        "charset-normalizer",
    ],
    python_requires=">=3.8.0",
    entry_points={"console_scripts": ["apiview-gpt=create_review",]},
    classifiers=[
        "Development Status :: 3 - Alpha",
        "Programming Language :: Python",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "License :: OSI Approved :: MIT License",
    ]
)
