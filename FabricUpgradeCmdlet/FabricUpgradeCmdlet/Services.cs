using FabricUpgradeCmdlet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet
{
    public class Services
    {
        public static string JunkPrefix = "";

        public static IHttpClientFactory HttpClientFactory = new FabricUpgradeHttpClientFactory();
    }
}
