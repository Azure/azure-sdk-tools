from setuptools import setup,find_packages

setup(
    name="tox-monorepo",
    packages=find_packages(),
    entry_points={"tox": ["monorepo=tox_monorepo:monorepo"]},
    classifiers=["Framework:: tox"],
)