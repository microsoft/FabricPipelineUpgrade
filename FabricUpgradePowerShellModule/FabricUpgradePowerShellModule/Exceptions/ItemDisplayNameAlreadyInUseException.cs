// <copyright file="ItemDisplayNameAlreadyInUseException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradePowerShellModule.Exceptions
{
    /// <summary>
    /// A particular instruction to throw if the PublicAPI says that
    /// the requested DisplayName is already in use.
    /// </summary>
    /// <remarks>
    /// Even if you delete a Pipeline that has a DisplayName, the PublicAPI
    /// will continue to return this error for several hours thereafter.
    /// This is a bit annoying, because that Pipeline will <em>not</em> show up in 
    /// the response from PublicApi.ListItems(), so we cannot really tell that
    /// this DisplayName is taken.
    /// </remarks>
    public class ItemDisplayNameAlreadyInUseException : Exception
    {
        public ItemDisplayNameAlreadyInUseException(string displayName)
            : base($"The item name '{displayName}' is already in use. This is likely because you had an item with this name and deleted it recently. If this is the case, please wait about a minute or two and retry the operation.")
        {
        }
    }
}
