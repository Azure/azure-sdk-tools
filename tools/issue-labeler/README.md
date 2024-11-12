# dotnet/issue-labeler

This repo contains the code to build the dotnet issue labeler.

## Which github repositories use this issue labeler today?

The dotnet organization contains repositories with many incoming issues and pull requests. In order to help with the triage process, issues get categorized with area labels. The issues related to each area get labeled with a specific `area-` label, and then these label assignments get treated as learning data for an issue labeler to be built. 

The following repositories currently triage their incoming issues semi-automatically, by manually selecting one of top 3 predictions received from a dotnet/issue-labeler:

* [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)
* [dotnet/extensions](https://github.com/dotnet/extensions)

The following repositories allow dotnet/issue-labeler to automatically set `area-` labels for incoming issues and pull requests:

* [dotnet/runtime](https://github.com/dotnet/runtime)
* [dotnet/corefx](https://github.com/dotnet/corefx) (archived)
* [dotnet/roslyn](https://github.com/dotnet/roslyn)
* [dotnet/dotnet-api-docs](https://github.com/dotnet/dotnet-api-docs)
* [dotnet/docker-tools](https://github.com/dotnet/docker-tools)
* [dotnet/dotnet-docker](https://github.com/dotnet/dotnet-docker)
* [dotnet/dotnet-buildtools-prereqs-docker](https://github.com/dotnet/dotnet-buildtools-prereqs-docker)
* [microsoft/dotnet-framework-docker](https://github.com/microsoft/dotnet-framework-docker)

Of course with automatic labeling there is always a margin of error. But the good thing is that the labeler can learns from mistakes so long as wrong label assignments get corrected manually.

To help with this process, any new issue that gets created also takes an `untriaged` label which then is expected to get removed by area owner for the assigned area label as they go through their triage process. Once being reviewed by the area owner, if they deem the automatic label as incorrect they may remove incorrect label and allow for correct one to get added manually.

## How to use this issue labeler today?

To get the most out of this issue labeler, prior setup, the repository needs to get to a point where it has been pre-populated with a portion of issues with `area-` labels on them. 

It is possible to still get usage out of this issue labeler, even if you decided to continue doing manual label assignments, e.g. to get top-N predictions recommendations only.

But once the issue labeling is automated, it is recommended to make sure:

- Contributors have a habit of manually applying `area-` labels even when the labeler was not confident enough to select one.
- Contributors have a habit of manually correcting prediction mistakes done.

These two habits help the issue labeler learn better over time.

Also note, the labeler does not learn in real-time, but instead ML trainings need to be redone every once in a while (e.g. every two months depending on issue traffic).

## How to get started?

The [docs](Documentation/) page explains in more detail steps involved in setting up an issue labeler for a github repository.

## Useful Links

* [ML.NET](ML.NET) 
* [.NET home repo](https://github.com/Microsoft/dotnet) - links to 100s of .NET projects, from Microsoft and the community.
* [ASP.NET Core home](https://docs.microsoft.com/aspnet/core/?view=aspnetcore-3.1) - the best place to start learning about ASP.NET Core.

## License

.NET is licensed under the [MIT](LICENSE.TXT) license.
