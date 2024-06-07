using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabricUpgradeCmdlet.Utilities
{
    public class FabricUpgradeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateHttpClient()
        {
            return new HttpClient();
        }
    }
}
