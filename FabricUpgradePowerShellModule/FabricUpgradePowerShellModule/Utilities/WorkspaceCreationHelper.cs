// <copyright file="WorkspaceCreationHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace FabricUpgradePowerShellModule.Utilities
{
    /// <summary>
    /// Helper class for workspace and capacity creation validation and naming.
    /// </summary>
    public static class WorkspaceCreationHelper
    {
        private static readonly Regex ValidWorkspaceNamePattern = new Regex(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-_\.]{0,62}[a-zA-Z0-9]$", RegexOptions.Compiled);

        /// <summary>
        /// Generate a workspace name that includes the ADF factory name.
        /// </summary>
        /// <param name="adfName">The Azure Data Factory name.</param>
        /// <param name="customName">Optional custom workspace name.</param>
        /// <returns>A valid workspace name.</returns>
        public static string GenerateWorkspaceName(string adfName, string customName = null)
        {
            if (!string.IsNullOrEmpty(customName))
            {
                return ValidateAndSanitizeWorkspaceName(customName);
            }

            string sanitizedAdfName = SanitizeAdfName(adfName);
            string uniqueSuffix = DateTime.UtcNow.ToString("MMdd-HHmmss");
            string baseName = string.IsNullOrEmpty(sanitizedAdfName) 
                ? $"ADF-Migration-{uniqueSuffix}" 
                : $"{sanitizedAdfName}-Fabric-{uniqueSuffix}";

            return ValidateAndSanitizeWorkspaceName(baseName);
        }

        /// <summary>
        /// Generate a capacity name that includes the ADF factory name.
        /// </summary>
        /// <param name="adfName">The Azure Data Factory name.</param>
        /// <param name="customName">Optional custom capacity name.</param>
        /// <returns>A valid capacity name.</returns>
        public static string GenerateCapacityName(string adfName, string customName = null)
        {
            if (!string.IsNullOrEmpty(customName))
            {
                return ValidateAndSanitizeCapacityName(customName);
            }

            string sanitizedAdfName = SanitizeAdfName(adfName);
            string uniqueSuffix = DateTime.UtcNow.ToString("MMddHHmmss"); // Remove hyphens from timestamp
            string baseName = string.IsNullOrEmpty(sanitizedAdfName) 
                ? $"adfmigration{uniqueSuffix}" // Remove hyphens for more restrictive naming
                : $"{sanitizedAdfName}fabric{uniqueSuffix}"; // Remove hyphens for more restrictive naming

            return ValidateAndSanitizeCapacityName(baseName);
        }

        /// <summary>
        /// Sanitize an ADF name for use in Azure resource names.
        /// </summary>
        /// <param name="adfName">The ADF factory name.</param>
        /// <returns>A sanitized name suitable for use in resource names.</returns>
        private static string SanitizeAdfName(string adfName)
        {
            if (string.IsNullOrEmpty(adfName))
            {
                return string.Empty;
            }

            // Remove invalid characters for resource naming - be more restrictive for capacity names
            // Only keep alphanumeric characters to avoid potential Azure validation issues
            string sanitized = Regex.Replace(adfName, @"[^a-zA-Z0-9]", "");

            // Convert to lowercase for consistency
            sanitized = sanitized.ToLowerInvariant();

            // Limit length to leave room for suffixes (keeping it reasonable for readability)
            if (sanitized.Length > 20)
            {
                sanitized = sanitized.Substring(0, 20);
            }

            // Ensure it's not empty after sanitization
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "adf";
            }

            return sanitized;
        }

        /// <summary>
        /// Validate and sanitize a workspace name.
        /// </summary>
        /// <param name="name">The proposed workspace name.</param>
        /// <returns>A valid workspace name.</returns>
        public static string ValidateAndSanitizeWorkspaceName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return $"ADF-Migration-Workspace-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            // Remove or replace invalid characters
            string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9\s\-_\.]", "");
            
            // Ensure it doesn't start or end with invalid characters
            sanitized = Regex.Replace(sanitized, @"^[^a-zA-Z0-9]+", "");
            sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9]+$", "");

            // Ensure minimum length
            if (sanitized.Length < 2)
            {
                sanitized = $"ADF-Migration-Workspace-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            // Ensure maximum length
            if (sanitized.Length > 64)
            {
                sanitized = sanitized.Substring(0, 64);
            }

            return sanitized;
        }

        /// <summary>
        /// Validate and sanitize a capacity name.
        /// </summary>
        /// <param name="name">The proposed capacity name.</param>
        /// <returns>A valid capacity name.</returns>
        public static string ValidateAndSanitizeCapacityName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return $"adfmigrationcapacity{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            // Remove or replace invalid characters - be more restrictive for Azure Fabric capacity names
            // Only allow alphanumeric characters to avoid potential Azure validation issues
            string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9]", "");
            
            // Ensure minimum length with meaningful content
            if (sanitized.Length < 3)
            {
                sanitized = $"adfmigrationcapacity{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            // Ensure maximum length (Azure capacity names are typically limited to 63 characters)
            if (sanitized.Length > 63)
            {
                // Keep the end which likely has the timestamp for uniqueness
                sanitized = sanitized.Substring(sanitized.Length - 63);
            }

            // Convert to lowercase (Azure resource naming convention)
            sanitized = sanitized.ToLowerInvariant();

            // Ensure it starts with a letter (some Azure resources require this)
            if (!char.IsLetter(sanitized[0]))
            {
                sanitized = "adf" + sanitized;
                if (sanitized.Length > 63)
                {
                    sanitized = sanitized.Substring(0, 63);
                }
            }

            return sanitized;
        }

        /// <summary>
        /// Validate that a SKU name is supported for Fabric capacities.
        /// </summary>
        /// <param name="skuName">The SKU name to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool IsValidFabricSku(string skuName)
        {
            var validSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "F2", "F4", "F8", "F16", "F32", "F64", "F128", "F256", "F512", "F1024", "F2048"
            };

            return !string.IsNullOrEmpty(skuName) && validSkus.Contains(skuName);
        }

        /// <summary>
        /// Get the default SKU if the provided SKU is invalid.
        /// </summary>
        /// <param name="skuName">The proposed SKU name.</param>
        /// <returns>A valid SKU name.</returns>
        public static string ValidateAndGetDefaultSku(string skuName)
        {
            return IsValidFabricSku(skuName) ? skuName : "F2";
        }
    }
}