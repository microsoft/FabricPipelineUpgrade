// <copyright file="UpgradeSerialization.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FabricUpgradeCmdlet.Utilities
{

    /// <summary>
    /// This class performs the serialization and deserialization used by the FabricUpgrader.
    /// </summary>
    internal static class UpgradeSerialization
    {
        /// <summary>
        /// This object contains the JsonSerializerSettings used to (de)serialize the models defined in the Upgrader.
        /// </summary>
        private static readonly ParserSettings UpgraderParserSettings = ConstructUpgraderParserSettings();

        /// <summary>
        /// Deserialize a JToken into an object of type T.
        /// </summary>
        /// <remarks>
        /// This method uses the TransformationJsonConverter to allow us to
        /// simplify the JSON description of the target object.
        /// </remarks>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>The new object of type T.</returns>
        public static T FromJToken<T>(JToken token)
        {
            return JsonConvert.DeserializeObject<T>(token.ToString(), UpgraderParserSettings.DeserializationSettings);
        }

        public static string Serialize<T>(T theObject)
        {
            return JsonConvert.SerializeObject(theObject, UpgraderParserSettings.SerializationSettings);
        }

        /// <summary>
        /// Serialize any (serializable) object to a JToken.
        /// </summary>
        /// <remarks>
        /// This method uses the TransformationJsonConverter to allow us to
        /// simplify the JSON description of the target object.
        /// </remarks>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="theObject">The object to serialize.</param>
        /// <returns>A JToken constructed from the object.</returns>
        public static JToken ToJToken<T>(T theObject)
        {
            return JToken.Parse(Serialize(theObject));
        }

        /// <summary>
        /// Construct the ParserSettings for the classes defined within the Upgrader.
        /// </summary>
        /// <returns>The [de]serialization objects used to parse and serialize Upgrader objects.</returns>
        private static ParserSettings ConstructUpgraderParserSettings()
        {
            JsonSerializerSettings deserializationSettings = new JsonSerializerSettings()
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                /*Converters = new List<JsonConverter>
                {
                    // This is the magic bit that allows us to specify [JsonProperty(PropertyName = "a.b.c")]
                    new TransformationJsonConverter(),
                },*/
            };

            JsonSerializerSettings serializationSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                /*Converters = new List<JsonConverter>
                {
                    // This is the magic bit that allows us to specify [JsonProperty(PropertyName = "a.b.c")]
                    new TransformationJsonConverter(),
                },*/
            };

            return new ParserSettings()
            {
                DeserializationSettings = deserializationSettings,
                SerializationSettings = serializationSettings,
            };
        }

        /// <summary>
        /// A struct to hold serialization and deserialization settings.
        /// </summary>
        private class ParserSettings
        {
            public JsonSerializerSettings SerializationSettings { get; set; }

            public JsonSerializerSettings DeserializationSettings { get; set; }
        }
    }
}
