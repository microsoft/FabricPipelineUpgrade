// <copyright file="LinkedServiceUpgrader.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Upgraders.LinkedServiceUpgraders
{
    public class LinkedServiceUpgrader : Upgrader
    {
        public class LinkedServiceTypes
        {
            public const string AzureBlobStorage = "AzureBlobStorage";
            public const string AzureSqlDatabase = "AzureSqlDatabase";
        }

        protected const string AdfLinkedServiceTypePath = "properties.type";
        protected const string AdfConnectionStringPath = "properties.typeProperties.connectionString";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
        };

        protected LinkedServiceUpgrader(
            JToken adfToken,
            IFabricUpgradeMachine machine)
            : base(adfToken, machine)
        {
            this.Name = adfToken.SelectToken("name")?.ToString();
            this.UpgraderType = FabricUpgradeResourceTypes.LinkedService;
            this.LinkedServiceType = adfToken.SelectToken(AdfLinkedServiceTypePath)?.ToString();
            this.Path = this.Name;
        }

        protected string LinkedServiceType { get; set; }

        // This dictionary is used by some DatasetUpgraders.
        // For example, the AzureSqlTableDatasetUpgrader uses it to fill in the "database"
        // field in the datasetSettings of a CopyActivity.
        public Dictionary<string, JToken> ConnectionSettings { get; set; } = new Dictionary<string, JToken>();

        /// <summary>
        /// Create the appropriate subclass of LinkedServiceUpgrader, based on the type of the ADF LinkedService.
        /// </summary>
        /// <param name="linkedServiceToken">The JToken describing the ADF LinkedService.</param>
        /// <param name="machine">The FabricUpgradeMachine that provides utilities to Upgraders.</param>
        /// <returns>A DatasetUpgrader.</returns>
        public static LinkedServiceUpgrader CreateLinkedServiceUpgrader(
            JToken linkedServiceToken,
            IFabricUpgradeMachine machine)
        {
            string linkedServiceType = linkedServiceToken.SelectToken("properties.type")?.ToString();
            // The current paradigm is "forbidden unless expressly allowed."
            // Therefore, if we don't explicitly add a LinkedService type to this list, then we will fail the upgrade.
            // We don't _upgrade_ the LinkedService, but we might need to know _something_ about it!
            return linkedServiceType switch
            {
                LinkedServiceTypes.AzureBlobStorage => new AzureBlobStorageLinkedServiceUpgrader(linkedServiceToken, machine),
                LinkedServiceTypes.AzureSqlDatabase => new AzureSqlDatabaseLinkedServiceUpgrader(linkedServiceToken, machine),
                _ => new UnsupportedLinkedServiceUpgrader(linkedServiceToken, machine),
            };
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);

            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            JToken connectionStringToken = this.AdfResourceToken.SelectToken(AdfConnectionStringPath);

            if (connectionStringToken == null)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its ConnectionString is missing.");
            }
            else if (connectionStringToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError($"Cannot upgrade LinkedService '{this.Path}' because its ConnectionString is not a string.");
            }
            else
            {
                this.BuildConnectionSettings(connectionStringToken.ToString());
            }
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolves)
            {
                List<FabricExportResolve> resolves = new List<FabricExportResolve>();

                FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.LinkedServiceToConnectionId;
                FabricExportResolve userCredentialConnectionResolve = new FabricExportResolve(
                    resolutionType,
                    this.Name,
                    "id")
                    .WithHint(this.BuildFabricConnectionHint()
                        .WithTemplate(new FabricUpgradeResolution()
                        {
                            Type = resolutionType,
                            Key = this.Name,
                            Value = "<guid>"
                        }
                   ));

                resolves.Add(userCredentialConnectionResolve);

                return Symbol.ReadySymbol(JArray.Parse(UpgradeSerialization.Serialize(resolves)));
            }
            if (symbolName == Symbol.CommonNames.FabricResource)
            {
                ConnectionExportInstruction exportInstruction = new ConnectionExportInstruction(this.Name);

                Symbol resolvesSymbol = this.ResolveExportedSymbol(Symbol.CommonNames.ExportResolves, alerts);
                if (resolvesSymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }
                if (resolvesSymbol.Value != null)
                {
                    foreach (JToken requiredResolution in (JArray)resolvesSymbol.Value)
                    {
                        FabricExportResolve resolve = FabricExportResolve.FromJToken(requiredResolution);
                        exportInstruction.Resolves.Add(resolve);
                    }
                }
                exportInstruction.Export["id"] = Guid.Empty.ToString();

                return Symbol.ReadySymbol(exportInstruction.ToJObject());
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        /// <summary>
        /// From the information in the LinkedService, construct a "hint" for the client/user
        /// about the Fabric Connection that should be used to resolve the LinkedService.
        /// </summary>
        /// <returns>A FabricConnectionHint.</returns>
        protected virtual FabricUpgradeConnectionHint BuildFabricConnectionHint()
        {
            return new FabricUpgradeConnectionHint()
                .WithLinkedServiceName(this.Name);
        }

        /// <summary>
        /// Convert the ConnectionString into a dictionary.
        /// </summary>
        /// <remarks>
        /// A LinkedService may store its "connection string" in different locations,
        /// so we require the sub-class to extract that and call this method.
        /// </remarks>
        /// <param name="connectionString">The connection string to parse.</param>
        protected void BuildConnectionSettings(
            string connectionString)
        {
            Dictionary<string, JToken> settings = new Dictionary<string, JToken>();

            // A connection string looks like "a=b;c=d;e=f'..."
            if (!string.IsNullOrEmpty(connectionString))
            {
                // Split the connection string into elements [ "a=b", "c=d", "e=f", ... ].
                string[] connectionStringParts = connectionString.Split(';');

                // parse each element as "a=b".
                foreach (string connectionStringPart in connectionStringParts)
                {
                    string[] parsedPart = connectionStringPart.Split(new char[] { '=' }, 2);
                    if (parsedPart.Length == 2)
                    {
                        settings[parsedPart[0]] = parsedPart[1];
                    }
                }
            }

            this.ConnectionSettings = settings;
        }
    }
}
