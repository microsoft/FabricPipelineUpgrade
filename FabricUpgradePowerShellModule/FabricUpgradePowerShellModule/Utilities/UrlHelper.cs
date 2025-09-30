// <copyright file="WorkspaceCreationHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradePowerShellModule.Utilities
{
    public static class UrlHelper
    {
        /// <summary>
        /// Break the URL into its components.
        /// </summary>
        /// <returns>The host name and the relative URL.</returns>
        public static (string HostName, string RelativeUrl) ProcessUrl(string url)
        {
            url = UrlHelper.EnsureHttpSchemeIsPresent(url, "http");

            string hostname = null;
            string pathAndQuery = null;

            // Note: We might want to support Connections that have a HostName and a path "prefix".
            // For example, the Connection has the URL "http://abc.com/orders" and we convert
            // "http://abc.com/orders/1234" into <connectionId>, "/1234".
            // This is a little bit complicated, so, for now, we'll just support Connections that
            // point at the Host.
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                hostname = uri.Authority;
                pathAndQuery = uri.PathAndQuery;
            }

            return (hostname, pathAndQuery);
        }

        // The constructor for System.Uri will fail if the URL does
        // not include a schema. Make sure that there is one.
        private static string EnsureHttpSchemeIsPresent(
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
