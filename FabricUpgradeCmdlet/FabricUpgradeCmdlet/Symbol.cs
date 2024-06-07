// <copyright file="Symbol.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet
{
    /// <summary>
    /// This class represents a generic symbol within the Fabric Upgrader.
    /// </summary>
    public class Symbol
    {
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

        public static Symbol ReadySymbol(JToken value)
        {
            return new Symbol()
            {
                State = SymbolState.Ready,
                Value = value,
            };
        }

        public static Symbol MissingSymbol()
        {
            return new Symbol()
            {
                State = SymbolState.Missing,
                Value = null,
            };
        }

        public Symbol AddProperty(
            string destinationPath,
            JToken value)
        {
            if (this.ActualValue == null)
            {
                this.ActualValue = new JObject();
            }

            (JObject targetObject, string targetKey) = this.TargetFromDestinationPath(destinationPath);
            targetObject[targetKey] = value;

            return this;
        }

        public override string ToString()
        {
            return $"{this.State}: {this.ActualValue?.ToString(Formatting.None)}";
        }

        /// <summary>
        /// Ensure that the destination path exists in this symbol's Value, and tell the caller where it is.
        /// </summary>
        /// <param name="destinationPath">The new path to create in Value.</param>
        /// <returns>The object that will hold the new property, and the key of the new property.</returns>
        private Tuple<JObject, string> TargetFromDestinationPath(string destinationPath)
        {
            /* TODO: Handle arrays! */

            JObject target = (JObject)this.ActualValue;
            string[] destinationParts = destinationPath.Split(new char[] { '.' });
            for (int n = 0; n < destinationParts.Length - 1; n++)
            {
                string dp = destinationParts[n];
                if (!target.ContainsKey(dp))
                {
                    target[dp] = new JObject();
                }

                target = (JObject)target[dp];
            }

            return Tuple.Create(target, destinationParts[destinationParts.Length - 1]);
        }
    }
}
