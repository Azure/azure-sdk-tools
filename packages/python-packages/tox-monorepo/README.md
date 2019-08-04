# tox-monorepo

This plugin changes {toxinidir} to be the directory that executes the tox command, rather than where the tox.ini actually lives on disk. This allows a monorepo to easily share a single tox.ini file rather than have to keep copies up to date.

Features
--------

Once installed, `tox-monorepo` will actively post-process the loaded `tox config` and replace all instances of `{toxinidir}` with a reference to the **current working directory.**


Requirements
------------

Tested on `tox 3.1+`, `Python 2.7+`


Installation
------------

You can install "tox-monorepo" via [pip](https://pypi.org/project/pip/) from [PyPI](https://pypi.org):

```
pip install tox-monorepo
```

Usage
-----

Install the plugin, then try to reference a tox config from within a package directory.

```
tox -c <otherpath>/to/tox.ini

```

Note that all the `.tox` folder + any environments are now created _relative to the directory that executed tox_. 


Contributing
------------
Contributions are very welcome, though the plugin is _extremely_ straightforward and shouldn't really require updates. Just submit a PR or an Issue!

License
-------

Distributed under the terms of the **MIT** license, `tox-monorepo` is
free and open source software.


Issues
------

If you encounter any problems, please
[file an issue](https://github.com/Azure/azure-sdk-tools)
along with `tox-monorepo` in the title.