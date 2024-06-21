// <copyright file="FabricUpgradeConnectionHint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace FabricUpgradeCmdlet.Models
{
    /// <summary>
    /// This class describes a "hint" returned when the user needs to
    /// create/find a Fabric Connection which should replace an ADF LinkedService.
    /// </summary>
    public class FabricUpgradeConnectionHint
    {
        /// <summary>
        /// Gets or sets the name of the ADF LinkedService to upgrade.
        /// </summary>
        [DataMember(Name = "linkedServiceName", Order = 10)]
        [JsonProperty("linkedServiceName")]
        public string LinkedServiceName { get; set; }

        /// <summary>
        /// Gets or sets the type of the Fabric Connection to find/create.
        /// </summary>
        [DataMember(Name = "connectionType", Order = 30)]
        [JsonProperty("connectionType")]
        public string ConnectionType { get; set; }

        /// <summary>
        /// Gets or sets the name of the datasource of the LinkedService.
        /// This is a "best effort" by the FabricUpgrader, that should be
        /// helpful to the client/user.
        /// </summary>
        [DataMember(Name = "datasource", Order = 40)]
        [JsonProperty("datasource")]
        public string Datasource { get; set; }

        // If we don't find this Resolution during Export, then tell the user
        // what the Resolution should "look like."
        [JsonProperty(PropertyName = "template")]
        public FabricUpgradeResolution Template { get; set; }

        /// <summary>
        /// Set the LinkedServiceName of the hint.
        /// </summary>
        /// <param name="linkedServiceName">The name of the ADF LinkedService.</param>
        /// <returns>this, for chaining.</returns>
        public FabricUpgradeConnectionHint WithLinkedServiceName(string linkedServiceName)
        {
            this.LinkedServiceName = linkedServiceName;
            return this;
        }

        /// <summary>
        /// Set the ConnectionType of the hint.
        /// </summary>
        /// <param name="connectionType">The connection type.</param>
        /// <returns>this, for chaining.</returns>
        public FabricUpgradeConnectionHint WithConnectionType(string connectionType)
        {
            this.ConnectionType = connectionType;
            return this;
        }

        /// <summary>
        /// Set the Datasource of the hint.
        /// </summary>
        /// <param name="datasource">The datasource.</param>
        /// <returns>this, for chaining.</returns>
        public FabricUpgradeConnectionHint WithDatasource(string datasource)
        {
            this.Datasource = datasource;
            return this;
        }

        /// <summary>
        /// Set the Tempate of the hint.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <returns>this, for chaining.</returns>
        public FabricUpgradeConnectionHint WithTemplate(FabricUpgradeResolution template)
        {
            this.Template = template;
            return this;
        }
    }
}
