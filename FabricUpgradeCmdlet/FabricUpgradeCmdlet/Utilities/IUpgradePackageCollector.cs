using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Utilities
{
    internal interface IUpgradePackageCollector
    {
        void Collect(string entryName, string entryData);
    }
}
