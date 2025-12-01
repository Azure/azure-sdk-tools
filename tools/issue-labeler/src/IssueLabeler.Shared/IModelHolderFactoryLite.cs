// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IssueLabeler.Shared
{
    public interface IModelHolderFactoryLite
    {
        Task<IModelHolder[]> CreateModelHolders(string repo);
        Task<IModelHolder> CreateModelHolder(string repo, string modelType);
        Task<IPredictor> GetPredictor(string repo, string modelType);
    }
    public class ModelHolderFactoryLite : IModelHolderFactoryLite
    {
        private readonly ConcurrentDictionary<(string, string), IModelHolder> _models = new ConcurrentDictionary<(string, string), IModelHolder>();
        private readonly ILogger<ModelHolderFactoryLite> _logger;
        private readonly IRepositoryConfigurationProvider _configurationProvider;
        private SemaphoreSlim _sem = new SemaphoreSlim(1,1);

        public ModelHolderFactoryLite(
            ILogger<ModelHolderFactoryLite> logger,
            IRepositoryConfigurationProvider configurationProvider)
        {
            _configurationProvider = configurationProvider;
            _logger = logger;
        }

        public async Task<IModelHolder[]> CreateModelHolders(string repo)
        {
            var modelHolders = new IModelHolder[2];

            if (_models.TryGetValue((repo, LabelType.Service), out var categoryHolder) &&
            _models.TryGetValue((repo, LabelType.Service), out var serviceHolder))
            {
                modelHolders[0] = serviceHolder;
                modelHolders[1] = categoryHolder;

                return modelHolders;
            }

            // Some models need to be initialized; acquire the semaphore and initialize.
            try
            {
                if (!_sem.Wait(0))
                {
                    await _sem.WaitAsync().ConfigureAwait(false);
                }
                modelHolders[0] = await CreateModelHolderInternal(repo, LabelType.Service);
                modelHolders[1] = await CreateModelHolderInternal(repo, LabelType.Category);
            }
            finally
            {
                if (_sem.CurrentCount <= 0)
                {
                    _sem.Release();
                }
            }

            return modelHolders;
        }

        public async Task<IModelHolder> CreateModelHolder(string repo, string modelType)
        {
            if (_models.TryGetValue((repo, modelType), out var modelHolder))
            {
                return modelHolder;
            }

            try
            {
                if (!_sem.Wait(0))
                {
                    await _sem.WaitAsync().ConfigureAwait(false);
                }

                return await CreateModelHolderInternal(repo, modelType).ConfigureAwait(false);
            }
            finally
            {
                if (_sem.CurrentCount <= 0)
                {
                    _sem.Release();
                }
            }
        }

        public async Task<IModelHolder> CreateModelHolderInternal(string repo, string modelType)
        {
            IModelHolder modelHolder = null;

            if (IsConfigured(repo))
            {
                if (_models.TryGetValue((repo, modelType), out modelHolder))
                {
                    return modelHolder;
                }

                modelHolder = await InitFor(repo, modelType);
                _models.GetOrAdd((repo, modelType), modelHolder);
            }

            return modelHolder;
        }

        public async Task<IPredictor> GetPredictor(string repo, string modelType)
        {
            var modelHolder = await CreateModelHolder(repo, modelType);
            if (modelHolder == null)
            {
                throw new InvalidOperationException($"Repo {repo} is not yet configured for label prediction.");
            }
            if (!modelHolder.IsIssueEngineLoaded || (!modelHolder.UseIssuesForPrsToo && !modelHolder.IsPrEngineLoaded))
            {
                throw new InvalidOperationException("Issue engine must be loaded.");
            }
            var configuration = _configurationProvider.GetForRepository(repo);
            return new Predictor(_logger, configuration, modelHolder);
        }

        private bool IsConfigured(string repo)
        {
            var configuration = _configurationProvider.GetForRepository(repo);
            return !string.IsNullOrEmpty(configuration.IssueModelForCategoryLabels) 
            && !string.IsNullOrEmpty(configuration.IssueModelForServiceLabels);
        }

        private async Task<IModelHolder> InitFor(string repo, string modelType)
        {
            var configuration = _configurationProvider.GetForRepository(repo);
            var mh = new ModelHolder(_logger, configuration, repo, modelType);
            if (!mh.LoadRequested)
            {
                await mh.LoadEnginesAsync();
            }
            return mh;
        }
    }
}
