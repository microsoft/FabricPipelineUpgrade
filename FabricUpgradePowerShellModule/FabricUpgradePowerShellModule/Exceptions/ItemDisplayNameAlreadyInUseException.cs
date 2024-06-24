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
    ///
    /// This is a bit annoying, because that Pipeline will _not_ show up in 
    /// the response from PublicAPI.ListItems(), so we cannot really tell that
    /// this DisplayName is taken.
    /// </remarks>
    public class ItemDisplayNameAlreadyInUseException : Exception
    {
        public ItemDisplayNameAlreadyInUseException(string displayName)
            : base($"The item name '{displayName}' is already in use.")
        {
        }
    }
}
