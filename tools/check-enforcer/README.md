# Check Enforcer


This folder contains the source code for Check Enforcer. Check Enforcer is a service built by the Azure SDK Engineering Systems team which makes it easier to work with GitHub checks in a mono-repository. When you install and configure Check Enforcer in your repository it will add a check each time you create a pull request which will not turn green until all other checks have passed?

## Why did we create Check Enforcer?

The Azure SDK team maintains reusable libraries that developers use to access Azure services. These libraries are grouped by together into a repository for each language/runtime. For example there is a repository for [Java](https://github.com/azure/azure-sdk-for-java), [.NET](https://github.com/azure/azure-sdk-for-net), [Python](https://github.com/azure/azure-sdk-for-python) and [JavaScript](https://github.com/azure/azure-sdk-for-javascript) - just to name a few.

Each repository contains a large number of seperate libraries. Even though together these libraries constitute a single SDK, they ship seperately on their own individual cadence as the underlying service evolves. As a result we have seperate build and release pipelines for say the KeyVault and the Event Hubs libraries in each repository.

Whilst Checks in GitHub are awesome, one of the limitations when setting up required checks is that you cannot make them required for just one specific path. We don't want to build all libraries for every checkin (that would take a long time and needlessly block teams if other libraries were having build reliability issues) - so we needed a way to work around it.

Check Enforcer is our solution. We use the built-in triggering w/ path filter options within Azure Pipelines (Check Enforcer is CI tool agnostic however) to control when a pipeline triggers, and we just use Check Enforcer to block until all triggered pipelines pass successfully. Each of those libraries can be optional - and you just make Check Enforcer the only required check in the repo.

## Usage

You can get started with Check Enforcer by first [installing the application](https://github.com/apps/check-enforcer) into your own repository. Once Check Enforcer is installed you need to commit a file named ```CHECKENFORCER``` into the root of the repository. The contents of the file are as follows:

```yaml
format: v0.1-alpha
minimumCheckRuns: 2
```

The presence of this file indicates to the Check Enforcer backend that you want to use Check Enforcer in this repository. The ```minimumCheckRuns``` field is used to specify the minimum number of check runs that Check Enforcer should see pass (other than itself) before it will itself turn green. Note that this is just the minimum - if Check Enforcer sees 10 check runs it will wait for them all to turn green - this option is just to guard against check runs that are slow to start and allows Check Enforcer to sit tight until they spin up.

Check Enforcer is entirely stateless - every time your checks are updated it reevaluates its status and updates the check accordingly. If you restart a build (e.g. by using the ```/azp run buildname``` command), then Check Enforcer will flick back to being in progress.

You can disable Check Enforcer anytime by uninstalling it from the repository or removing the ```CHECKENFORCER``` file. If you don't want to remove the file, you can use the ```enabled: true``` flag in the ```CHECKENFORCER``` file to temporarily stop Check Enforcer adding the check run to your PRs. Here is an example:

```yaml
format: v0.1-alpha
enabled: false
minimumCheckRuns: 2
```

## Need Help?

Check Enforcer is built primarily for use by the Azure SDK Engineering Systems teams for use within their mono-repositories. But we are happy for others to pick it up and start using it. If you have any issues feel free to log an issue on this GitHub repository and we'll do our best to help you out.

## Contributing

Got an idea for Check Enforcer? Great - a good way to start contributing is by creating an issue and discussing with us what you want to do. We are always happy to review unsolicited pull requests and if they match our goals for Check Enforcer we'll work with you to get it merged - but its probably better if you give us a heads up on what you want to achieve first.
