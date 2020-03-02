# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from setuptools import setup, find_packages
import setuptools

import os
from io import open
import re

PACKAGE_NAME = 'doc-warden'

DESCRIPTION = 'Doc-Warden is an internal project created by the Azure SDK Team. It is intended to be used by CI Builds to ensure that documentation standards are met. See readme for more details.'

with open(os.path.join('warden', 'version.py'), 'r') as fd:
    version = re.search(r'^VERSION\s*=\s*[\'"]([^\'"]*)[\'"]',
                        fd.read(), re.MULTILINE).group(1)

if not version:
    raise RuntimeError('Cannot find version information')

with open('README.md', encoding='utf-8') as f:
    long_description = f.read()

setup(
    name=PACKAGE_NAME,
    version=version,
    description=DESCRIPTION,
    long_description=long_description,
    long_description_content_type='text/markdown',
    url='https://github.com/Azure/azure-sdk-tools/',
    author='Microsoft Corporation',
    author_email='azuresdkengsysadmins@microsoft.com',

    license='MIT License',

    classifiers=[
        'Development Status :: 3 - Alpha',

        'Programming Language :: Python',
        'Programming Language :: Python :: 2.7',
        'Programming Language :: Python :: 3',
        'Programming Language :: Python :: 3.4',
        'Programming Language :: Python :: 3.5',
        'Programming Language :: Python :: 3.6',
        'Programming Language :: Python :: 3.7',
        'License :: OSI Approved :: MIT License',
    ],
    packages=find_packages(),
    install_requires = [
        'pyyaml', # docsettings file parse
        'markdown2', # parsing markdown to html
        'docutils', # parsing rst to html
        'pygments', # docutils uses pygments for parsing rst to html
        'beautifulsoup4', # parsing of generated html
        'jinja2', # used for generation from template for index_packages
        'requests', # utilized to validate published repository URLs. 
        'pathlib2'
    ],
    entry_points = {
        'console_scripts': [
            'ward=warden:console_entry_point',
        ]
    }
)
