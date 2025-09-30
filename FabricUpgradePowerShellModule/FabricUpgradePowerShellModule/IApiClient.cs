// <copyright file="IApiClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradePowerShellModule
{
    /// <summary>
    /// Interface for API clients that can import pipeline resources.
    /// </summary>
    public interface IApiClient : IDisposable
    {
        /// <summary>
        /// Get all pipelines from the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of pipeline names to pipeline definitions.</returns>
        Task<Dictionary<string, JObject>> GetPipelinesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific pipeline from the service.
        /// </summary>
        /// <param name="pipelineName">The name of the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The pipeline definition.</returns>
        Task<JObject> GetPipelineAsync(string pipelineName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all datasets from the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of dataset names to dataset definitions.</returns>
        Task<Dictionary<string, JObject>> GetDatasetsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific dataset from the service.
        /// </summary>
        /// <param name="datasetName">The name of the dataset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The dataset definition.</returns>
        Task<JObject> GetDatasetAsync(string datasetName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all linked services from the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of linked service names to linked service definitions.</returns>
        Task<Dictionary<string, JObject>> GetLinkedServicesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific linked service from the service.
        /// </summary>
        /// <param name="linkedServiceName">The name of the linked service.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The linked service definition.</returns>
        Task<JObject> GetLinkedServiceAsync(string linkedServiceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all triggers from the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of trigger names to trigger definitions.</returns>
        Task<Dictionary<string, JObject>> GetTriggersAsync(CancellationToken cancellationToken = default);
    }
}