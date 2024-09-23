from setuptools import setup, find_packages
import os, re

PACKAGE_NAME = "tox-monorepo"

DESCRIPTION = "A tox plugin built to allow sharing of a common tox.ini file across repositories with high package counts."

with open(os.path.join("tox_monorepo", "version.py"), "r") as fd:
    version = re.search(
        r'^VERSION\s*=\s*[\'"]([^\'"]*)[\'"]', fd.read(), re.MULTILINE
    ).group(1)

if not version:
    raise RuntimeError("Cannot find version information")

with open("README.md", encoding="utf-8") as f:
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
    install_requires=["tox<4.0.0,>=3.12.0"],
    entry_points={"tox": ["monorepo=tox_monorepo:monorepo"]},
    classifiers=[
        "Framework :: tox",
        "Development Status :: 7 - Inactive",
        "Programming Language :: Python",
        "Programming Language :: Python :: 2.7",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.4",
        "Programming Language :: Python :: 3.5",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "License :: OSI Approved :: MIT License",
    ],
)
