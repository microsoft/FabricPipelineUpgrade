using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet
{
    public class UpgradeFailureException : Exception
    {
        public UpgradeFailureException(string phase)
            : base($"Upgrade failed in {phase} phase.")
        {
        }
    }
}
