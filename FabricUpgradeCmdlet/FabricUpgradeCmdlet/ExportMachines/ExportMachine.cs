using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static FabricUpgradeCmdlet.Models.FabricUpgradeResolution;

namespace FabricUpgradeCmdlet.ExportMachines
{
    public class ExportMachine
    {
        public ExportMachine(
            JObject exportObject,
            string workspaceId,
            List<FabricUpgradeResolution> resolutions,
            AlertCollector alerts)
        {
            this.ExportObject = exportObject;
            this.WorkspaceId = workspaceId;
            this.Resolutions = resolutions ?? new List<FabricUpgradeResolution>();
            this.Alerts = alerts;
        }

        protected List<FabricUpgradeResolution> Resolutions { get; private set; }

        protected JObject ExportObject { get; private set; } = new JObject();

        protected string WorkspaceId { get; private set; }

        protected AlertCollector Alerts { get; private set; }


        public virtual Task<FabricUpgradeProgress> ExportAsync()
        {
            return Task.FromResult(new FabricUpgradeProgress());
        }

        public string Resolve(
            FabricUpgradeResolution.ResolutionType type,
            string key,
            AlertCollector alerts)
        {
            FabricUpgradeResolution matchingResolution = this.Resolutions.Where(r => r.Type == type && r.Key == key).FirstOrDefault();

            if (matchingResolution == null)
            {
                return null;
            }

            return matchingResolution.Value;
        }

        public virtual JToken Link(
            string key,
            AlertCollector alerts)
        {
            return string.Empty;
        }
    }
}
