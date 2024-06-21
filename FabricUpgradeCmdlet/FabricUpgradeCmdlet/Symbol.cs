// <copyright file="Symbol.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet
{
    /// <summary>
    /// This class represents a generic symbol within the Fabric Upgrader.
    /// Upgraders query other Upgraders for symbol values.
    /// Exporters query other Exporters for symbol values.
    /// The UpgradeMachine queries some Upgraders for their exportInstructions.
    /// </summary>
    public class Symbol
    {
        public class CommonNames
        {
            // The ConvertTo-FabricResources command generates a set of
            // exportResolves, exportLinks, and exportInstructions
            // that are passed to Export-FabricResources.

            // The exportResolves tell the Exporter which properties to extract from the Resolutions
            // and insert into the FabricResource.
            public const string ExportResolves = "exportResolves";

            // The exportLinks tell the Exporter to find the FabricResource ID from another exported
            // Fabric Resource, and insert that ID into the Resource we are currently exporting.
            // (Note: this is why we need to sort the FabricResources before we start Exporting them).
            public const string ExportLinks = "exportLinks";

            // The "exportFabricResource" tells the Exporter what to send to Create/Update Item.
            public const string ExportInstructions = "exportInstructions";

            // This symbol's value will describe the JSON definition of one Activity inside a Pipeline
            // or inside another Activity.
            // (For example, the IfCondition Activity needs to query its sub-activities).
            public const string Activity = "activity";

            // A Copy Activity (and a few other Activities) need to fetch the datasetSettings
            // from each of its Datasets.
            // (ADF treats Datasets as separate resources, but Fabric moves these settings into the Activity itself).
            public const string DatasetSettings = "datasetSettings";

            // A Dataset may need to query its LinkedService for a databaseName.
            // The Dataset will include that databaseName in the datasetSettings that it builds for (e.g.) its Copy Activity.
            public const string LinkedServiceDatabaseName = "databaseName";
        }

        public Symbol()
        {
        }

        public enum SymbolState
        {
            /// <summary>Every enum deserves an Unknown value.</summary>
            Unknown = 0,

            /// <summary>The exported symbol is ready to use.</summary>
            Ready = 1,

            /// <summary>The exported symbol is not (yet!) ready to use.</summary>
            Pending = 2,

            /// <summary>The exported symbol would include one or more invalid expressions.</summary>
            Invalid = 3,

            /// <summary>The exported symbol cannot be constructed.</summary>
            Missing = 4,
        }

        public SymbolState State { get; set; }

        public JToken Value
        {
            get => (this.State == SymbolState.Ready) ? this.ActualValue : null;
            set => this.ActualValue = value;
        }

        protected JToken ActualValue { get; set; }

        /// <summary>
        /// Build a Symbol with state Ready with the specified value.
        /// </summary>
        /// <param name="value">The value to insert into the Symbol.</param>
        /// <returns>A Symbol whose state is Ready.</returns>
        public static Symbol ReadySymbol(JToken value)
        {
            return new Symbol()
            {
                State = SymbolState.Ready,
                Value = value,
            };
        }

        /// <summary>
        /// Build a Symbol with state Missing.
        /// </summary>
        /// <returns>A Symbol whose state is Missing.</returns>
        public static Symbol MissingSymbol()
        {
            return new Symbol()
            {
                State = SymbolState.Missing,
                Value = null,
            };
        }
    }
}
