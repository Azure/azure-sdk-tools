# tox-monorepo

This plugin changes {toxinidir} to be the directory that executes the tox command, rather than where the tox.ini actually lives on disk. This allows a monorepo to easily share a single tox.ini file rather than have to keep copies up to date.

Deprecation Notice
------------------

With the availability of `tox 4`, `tox-monorepo` no longer has a purpose. It's usage has been entirely supplanted by the `root` argument provided with `tox 4`. Users should only consume `tox-monorepo` if they are forced onto the earlier `tox 3`.

To take advantage of `tox 4`'s new feature in your `tox.ini`...

1. Call `tox` with the addition of `--root path/to/folder/with/tox.ini` (still from the root of your package)
2. Replace all occurrences of `{toxinidir}` with `{tox_root}`.

Feel free to reference [this PR](https://github.com/Azure/azure-sdk-for-python/pull/30159/files) from azure-sdk-for-python supplanting the usage of `tox-monorepo` while upgrading to `tox 4`.

Features
--------

Once installed, `tox-monorepo` will actively post-process the loaded `tox config` and replace all instances of `{toxinidir}` with a reference to the **current working directory.**

Note that if there are any out-of-ordinary changes present within your tox config, it is highly likely that this plugin has not been tested with them. Reference `Contributing` section for how to reach out if issues crop up.

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


## Complete Set of Updated Config Values

### Base Config Values:

```
{toxinidir}
{toxworkdir}
{temp_dir}
{setupdir}
{distdir}
{sdistsrc}
{logdir}

```

### Env-Specific Config Values

```
{commands}
{commands_pre}
{commands_post}
{envdir}
{changedir}
{envtmpdir}
{envlogdir}

```

Contributing
------------
Contributions are very welcome, though the plugin is _extremely_ straightforward and shouldn't really require updates. Just submit a PR or an Issue on the the current repo!

License
-------

Distributed under the terms of the **MIT** license, `tox-monorepo` is
free and open source software.

Issues
------

If you encounter any problems, please
[file an issue](https://github.com/Azure/azure-sdk-tools)
along with `tox-monorepo` in the title.