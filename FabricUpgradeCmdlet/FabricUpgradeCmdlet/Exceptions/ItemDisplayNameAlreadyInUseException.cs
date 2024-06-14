// <copyright file="ItemDisplayNameAlreadyInUseException.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace FabricUpgradeCmdlet.Exceptions
{
    public class ItemDisplayNameAlreadyInUseException : Exception
    {
        public ItemDisplayNameAlreadyInUseException(string displayName)
            : base($"The item name '{displayName}' is already in use.")
        {
        }
    }
}
