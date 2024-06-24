using Newtonsoft.Json;

namespace FabricUpgradeTests.TestConfigModels
{
    /// <summary>
    /// A description of how to update the expectedItems field to match
    /// what we actually expect the code to generate.
    /// </summary>
    /// <remarks>
    /// We do NOT want to use fixed Guids in our code; we want every test run to create
    /// unique Guids. This way, we don't ever run into a false positive where a Guid 
    /// "just happens" to work.
    ///
    /// The test configuration file cannot contain the actual Guids that will
    /// be used in each test. A GuidSubstitution allows us to update an
    /// expectedItems field to match what we really expect.
    ///
    /// Each test creates in advance a list of Guids that the TestPublicApiEndpoints will assign
    /// to a created item, and initializes the TestPublicApiEndpoints with that list.
    /// After the test loads the test config, the test tells the test config to apply that list
    /// of Guids to the expectedItems field.
    ///
    /// This technique allows us to use new, unique Guids for each test run, and then synchronize
    /// the TestPublicApiEndpoints to the TestConfig.
    /// </remarks>
    public class GuidSubstitution
    {
        /// <summary>
        /// Where in the expectedItems field to place the Guid.
        /// </summary>
        /// <remarks>
        /// This field will begin with "[n]" to select which expected item to update.
        /// </remarks>
        [JsonProperty(PropertyName = "path")]
        public string Path { get; set; }

        /// <summary>
        /// The index of the Guid to place at "path".
        /// </summary>
        [JsonProperty(PropertyName = "guidIndex")]
        public int GuidIndex { get; set; }
    }
}
