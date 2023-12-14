from setuptools import setup


with open("README.md", encoding="utf-8") as f:
    readme = f.read()

setup(
    name="azure-pylint-guidelines-checker",
    version="0.3.0",
    url="http://github.com/Azure/azure-sdk-for-python",
    license="MIT License",
    description="A pylint plugin which enforces azure sdk guidelines.",
    author="Microsoft Corporation",
    author_email="azpysdkhelp@microsoft.com",
    py_modules=["pylint_guidelines_checker"],
    long_description=readme,
    long_description_content_type="text/markdown",
    python_requires=">=3.8",
)
