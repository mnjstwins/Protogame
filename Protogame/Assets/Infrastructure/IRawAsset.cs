﻿namespace Protogame
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents data contained in a raw asset.
    /// </summary>
    public interface IRawAsset
    {
        /// <summary>
        /// Whether or not the raw asset is compiled.
        /// </summary>
        bool IsCompiled { get; }

        /// <summary>
        /// A read-only copy of the properties associated with this raw asset.
        /// </summary>
        ReadOnlyCollection<KeyValuePair<string, object>> Properties { get; }

        /// <summary>
        /// Retrieve a property value from a raw asset.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="defaultValue">The default value to return if the property is not present.</param>
        /// <typeparam name="T">The type of data to retrieve.</typeparam>
        /// <returns>The data on the raw asset.</returns>
        T GetProperty<T>(string name, T defaultValue = default(T));

        /// <summary>
        /// Retrieve a property value from a raw asset.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <param name="name">The property name.</param>
        /// <param name="defaultValue">The default value to return if the property is not present.</param>
        /// <returns>The data on the raw asset.</returns>
        object GetProperty(System.Type type, string name, object defaultValue = default(object));
    }
}