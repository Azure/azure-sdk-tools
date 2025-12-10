// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IssueLabeler.Shared
{
    public interface IModelHolderFactory
    {
        Task<IModelHolder[]> CreateModelHolders(string owner, string repo);
        Task<IModelHolder> CreateModelHolder(string owner, string repo, string modelType);
        Task<IPredictor> GetPredictor(string owner, string repo, string modelType);
    }
    public class ModelHolderFactory : IModelHolderFactory
    {
        private readonly ConcurrentDictionary<(string, string, string), IModelHolder> models = new ConcurrentDictionary<(string, string, string), IModelHolder>();
        private readonly ILogger<ModelHolderFactory> logger;
        private readonly IRepositoryConfigurationProvider configurationProvider;
        private SemaphoreSlim sem = new SemaphoreSlim(1,1);

        public ModelHolderFactory(
            ILogger<ModelHolderFactory> logger,
            IRepositoryConfigurationProvider configurationProvider)
        {
            this.configurationProvider = configurationProvider;
            this.logger = logger;
        }

        public async Task<IModelHolder[]> CreateModelHolders(string owner, string repo)
        {
            var modelHolders = new IModelHolder[2];

            if (this.models.TryGetValue((owner, repo, LabelType.Category), out var categoryHolder) &&
            this.models.TryGetValue((owner, repo, LabelType.Service), out var serviceHolder))
            {
                modelHolders[0] = serviceHolder;
                modelHolders[1] = categoryHolder;

                return modelHolders;
            }

            // Some models need to be initialized; acquire the semaphore and initialize.
            try
            {
                if (!this.sem.Wait(0))
                {
                    await this.sem.WaitAsync().ConfigureAwait(false);
                }
                modelHolders[0] = await CreateModelHolderInternal(owner, repo, LabelType.Service);
                modelHolders[1] = await CreateModelHolderInternal(owner, repo, LabelType.Category);
            }
            finally
            {
                if (this.sem.CurrentCount <= 0)
                {
                    this.sem.Release();
                }
            }

            return modelHolders;
        }

        public async Task<IModelHolder> CreateModelHolder(string owner, string repo, string modelType)
        {
            if (this.models.TryGetValue((owner, repo, modelType), out var modelHolder))
            {
                return modelHolder;
            }

            try
            {
                if (!this.sem.Wait(0))
                {
                    await this.sem.WaitAsync().ConfigureAwait(false);
                }

                return await CreateModelHolderInternal(owner, repo, modelType).ConfigureAwait(false);
            }
            finally
            {
                if (this.sem.CurrentCount <= 0)
                {
                    this.sem.Release();
                }
            }
        }

        public async Task<IModelHolder> CreateModelHolderInternal(string owner, string repo, string modelType)
        {
            IModelHolder modelHolder = null;

            if (IsConfigured(owner, repo))
            {
                if (this.models.TryGetValue((owner, repo, modelType), out modelHolder))
                {
                    return modelHolder;
                }

                modelHolder = await InitFor(owner, repo, modelType);
                this.models.GetOrAdd((owner, repo, modelType), modelHolder);
            }

            return modelHolder;
        }

        public async Task<IPredictor> GetPredictor(string owner, string repo, string modelType)
        {
            var modelHolder = await CreateModelHolder(owner, repo, modelType);
            if (modelHolder == null)
            {
                throw new InvalidOperationException($"Repo {repo} is not yet configured for label prediction.");
            }
            if (!modelHolder.IsIssueEngineLoaded || (!modelHolder.UseIssuesForPrsToo && !modelHolder.IsPrEngineLoaded))
            {
                throw new InvalidOperationException("Issue engine must be loaded.");
            }
            var configuration = this.configurationProvider.GetForRepository($"{owner}/{repo}");
            return new Predictor(this.logger, configuration, modelHolder);
        }

        private bool IsConfigured(string owner, string repo)
        {
            var configuration = this.configurationProvider.GetForRepository($"{owner}/{repo}");
            return !string.IsNullOrEmpty(configuration.IssueModelForCategoryLabels) 
            && !string.IsNullOrEmpty(configuration.IssueModelForServiceLabels);
        }

        private async Task<IModelHolder> InitFor(string owner, string repo, string modelType)
        {
            var configuration = this.configurationProvider.GetForRepository($"{owner}/{repo}");
            var mh = new ModelHolder(this.logger, configuration, repo, modelType);
            if (!mh.LoadRequested)
            {
                await mh.LoadEnginesAsync();
            }
            return mh;
        }
    }
}
