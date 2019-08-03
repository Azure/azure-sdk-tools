[![Latest version on
PyPi](https://badge.fury.io/py/monorepo.svg)](https://badge.fury.io/py/monorepo)
[![Supported Python
versions](https://img.shields.io/pypi/pyversions/monorepo.svg)](https://pypi.org/project/monorepo/)
[![Azure Pipelines build
status](https://dev.azure.com/scbedd/monorepo/_apis/build/status/tox%20ci?branchName=master)](https://dev.azure.com/scbedd/monorepo/_build/latest?definitionId=9&branchName=master)
[![Documentation
status](https://readthedocs.org/projects/monorepo/badge/?version=latest&style=flat-square)](https://monorepo.readthedocs.io/en/latest/?badge=latest)
[![Code style:
black](https://img.shields.io/badge/code%20style-black-000000.svg)](https://github.com/python/black)

# monorepo

This plugin changes {toxinidir} to be the directory that executes the tox command, rather than where the tox.ini actually lives on disk. This allows a monorepo to easily share a single tox.ini file rather than have to keep copies up to date.

Features
--------

* TODO


Requirements
------------

* TODO


Installation
------------

You can install "tox-monorepo" via [pip](https://pypi.org/project/pip/) from [PyPI](https://pypi.org):

```
pip install tox-monorepo
```

Usage
-----

* TODO

Contributing
------------
Contributions are very welcome. Tests can be run with [tox](https://tox.readthedocs.io/en/latest/), please ensure
the coverage at least stays the same before you submit a pull request.

License
-------

Distributed under the terms of the **MIT** license, `tox-monorepo` is
free and open source software.


Issues
------

If you encounter any problems, please
[file an issue](https://github.com/scbedd/tox-monorepo/issues)
along with a detailed description.
