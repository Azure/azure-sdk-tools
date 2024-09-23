# This Script Inserts Pixels Into Your Readme Files

### Context
Github currently doesn't offer a method to see visitor traffic patterns. The `PATHS` API merely returns the _top 10_ most popular paths on your repository. There is no guarantee of coverage. So, what happens if you want to see whether location `A` in your repository is more popular than location `B`?. Only way that we've found is to insert an image who's number of loads can be counted. 

This python script inserts a pixel image whos _name corresponds to the location of the rendered readme in the repository_.

### Before Running, Some Details
This script was built and tested on Python 3.6/

[This project](../../../tools/pixel-server/README.md) is the code involved for counting the number of impressions per rendered readme view. Update it however you want to store the data. Currently it leverages `Application Insights` as the data store.

So there are prep steps before running this tool:

1. Go [host the code](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/?view=aspnetcore-2.2) from the [folder above](../../../tools/pixel-server/README.md) in a publicly accessible location.
2. Update the `HOSTNAME` variable to point at wherever your site is hosted.
3. Run the script against your repository

### Running the Script
`python <script_location> -d <target_directory> -i <rep_identifier>`

**Target Directory**:
The top level directory that the tool will search _below_ for all `readme.rst` or `readme.md` files.

**Repo Identifier**:
If this is run on multiple repositories, some way needs to be maintained to tell readmes apart when the requests are being fired from the same relative URL within each repository. The tool places this `repo identifier` as a lead value before the relative URL.

### Example Usage and Results
Given the below repository structure:

```
<repo-root, located at C:/repo/cool-repo>
│   README.md
│
└───<folder1>
    └───README.rst
└───<other files and folders>
```

Let's walk through an example usage. The `repo id` used here will be `cool-repo`.

```
/:> python <script_location> -d C:/repo/cool-repo -i cool-repo
```

With the sample repo structure above and the inputs provided, `/README.md` and `/folder1/README.rst` would be affected after the script run. A new image would appear in both of them.

For markdown, like `/README.md`:
```
![Impressions](<your HOSTNAME>/api/impressions/cool-repo%2FREADME.png)
```


For restructured text, like `/folder1/README.rst`:
```
.. image::  <your HOSTNAME>/api/impressions/cool-repo%2Ffolder1%2FREADME.png
```





