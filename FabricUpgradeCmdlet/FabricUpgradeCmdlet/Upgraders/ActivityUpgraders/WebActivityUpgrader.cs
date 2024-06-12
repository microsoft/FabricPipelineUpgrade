using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FabricUpgradeCmdlet.Models;
using FabricUpgradeCmdlet.UpgradeMachines;
using FabricUpgradeCmdlet.Utilities;
using System.Diagnostics.Eventing.Reader;

namespace FabricUpgradeCmdlet.Upgraders.ActivityUpgraders
{
    public class WebActivityUpgrader : ActivityUpgrader
    {
        private const string adfUrlPath = "typeProperties.url";

        private readonly List<string> requiredAdfProperties = new List<string>
        {
            adfUrlPath
        };

        public WebActivityUpgrader(
            string parentPath,
            JToken activityToken,
            IFabricUpgradeMachine machine)
            : base(ActivityUpgrader.ActivityTypes.Web, parentPath, activityToken, machine)
        {
        }

        /// <inheritdoc/>
        public override void Compile(AlertCollector alerts)
        {
            base.Compile(alerts);
            this.CheckRequiredAdfProperties(this.requiredAdfProperties, alerts);

            this.ValidateUrl(alerts);

            if (this.CountArrayElements("typeProperties.linkedServices") > 0)
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because it includes LinkedServices");
            }

            if (this.CountArrayElements("typeProperties.datasets") > 0)
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because it includes Datasets");
            }

        }

        /// <inheritdoc/>
        public override void PreLink(
            List<Upgrader> allUpgraders,
            AlertCollector alerts)
        {
            base.PreLink(allUpgraders, alerts);
        }

        /// <inheritdoc/>
        public override Symbol ResolveExportedSymbol(
            string symbolName,
            AlertCollector alerts)
        {
            if (symbolName == Symbol.CommonNames.ExportResolves)
            {
                List<FabricExportResolve> resolves = new List<FabricExportResolve>();

                // The ADF WebActivity directly declares its URL, but the Fabric WebActivity uses a Connection instead.
                // Therefore, the client must provide a UrlHostToConnectionId Resolution to help us out.

                (string hostName, _) = this.ProcessUrl();

                FabricUpgradeResolution.ResolutionType resolutionType = FabricUpgradeResolution.ResolutionType.UrlHostToConnectionId;
                FabricExportResolve userCredentialConnectionResolve = new FabricExportResolve(
                    resolutionType,
                    hostName,
                    "externalReferences.connection")
                    .WithHint(new FabricUpgradeConnectionHint()
                        {
                            LinkedServiceName = null,
                            ConnectionType = "Web v2",
                            Datasource = hostName
                        }
                        .WithTemplate(new FabricUpgradeResolution()
                            {
                                Type = resolutionType,
                                Key = hostName,
                                Value = "<guid>"
                            }
                   ));

                resolves.Add(userCredentialConnectionResolve);

                return Symbol.ReadySymbol(JArray.Parse(JsonConvert.SerializeObject(resolves)));

            }
            if (symbolName == Symbol.CommonNames.Activity)
            {
                Symbol activitySymbol = base.ResolveExportedSymbol(Symbol.CommonNames.Activity, alerts);

                if (activitySymbol.State != Symbol.SymbolState.Ready)
                {
                    // TODO!
                }

                JObject fabricActivityObject = (JObject)activitySymbol.Value;

                (_, string relativeUrl) = this.ProcessUrl();

                PropertyCopier copier = new PropertyCopier(this.Path, this.AdfResourceToken, fabricActivityObject, alerts);
                copier.Copy("description");
                copier.Copy("policy");

                copier.Copy("typeProperties.method", allowNull: false);
                copier.Copy("typeProperties.headers");
                copier.Set("typeProperties.relativeUrl", relativeUrl);
                copier.Copy("typeProperties.body");
                copier.Copy("typeProperties.disableCertValidation");
                copier.Copy("typeProperties.httpRequestTimeout");
                copier.Copy("typeProperties.turnOffAsync");

                // This property cannot be set until the Export operation phase.
                // We include this property in the "exportResolve" symbol.
                copier.Set("externalReferences.connection", Guid.Empty.ToString());

                return Symbol.ReadySymbol(fabricActivityObject);
            }

            return base.ResolveExportedSymbol(symbolName, alerts);
        }

        private int CountArrayElements(
            string path)
        {
            JToken token = this.AdfResourceToken.SelectToken(path);
            if (token == null) return 0;
            if (token.Type != JTokenType.Array) return 0;
            return ((JArray)token).Count;
        }

        private void ValidateUrl(AlertCollector alerts)
        {
            // We cannot upgrade a WebActivity that has an Expression for its URL.
            // The ADF WebActivity directly declares its URL, but the Fabric WebActivity uses a Connection instead.
            // If the ADF WebActivity's URL is a String, then we can make this work with a special UrlHostToConnection Resolution.
            JToken urlToken = this.AdfResourceToken.SelectToken("typeProperties.url");
            
            if (urlToken == null)
            {
                // We have already generated an alert for this.
                return;
            }

            // Validate that the URL is a string.
            if (urlToken.Type != JTokenType.String)
            {
                alerts.AddPermanentError(
                    $"{this.Path}.typeProperties.url is not a string and the FabricUpgrader cannot upgrade a WebActivity whose URL is not a string.");
                return;
            }

            // Validate that the URL is properly formatted.
            try
            {
                this.ProcessUrl();
            }
            catch (UriFormatException)
            {
                alerts.AddPermanentError($"Cannot upgrade Web Activity '{this.Path}' because its URL is improperly formatted.");
            }
        }

        /// <summary>
        /// Break the URL into its components.
        /// </summary>
        /// <returns>The host name and the relative URL.</returns>
        private (string HostName, string RelativeUrl) ProcessUrl()
        {
            // We already verified that this property exists!
            string url = this.AdfResourceToken.SelectToken(adfUrlPath).ToString();
            url = this.EnsureHttpSchemeIsPresent(url, "http");

            // Note: We might want to support Connections that have a HostName and a path "prefix".
            // For example, the Connection has the URL "http://abc.com/orders" and we convert
            // "http://abc.com/orders/1234" into <connectionId>, "/1234".
            // This is a little bit complicated, so, for now, we'll just support Connections that
            // point at the Host.
            Uri uri = new Uri(url);

            string hostname = uri.Authority;
            string pathAndQuery = uri.PathAndQuery;

            return (hostname, pathAndQuery);
        }

        // The constructor for System.Uri will fail if the URL does
        // not include a schema. Make sure that there is one.
        private string EnsureHttpSchemeIsPresent(
            string url,
            string defaultHttpScheme)
        {
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return url;
            }

            return defaultHttpScheme + "://" + url;
        }
    }
}
