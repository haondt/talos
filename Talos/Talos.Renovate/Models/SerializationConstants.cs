using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Talos.Renovate.Models
{
    internal class SerializationConstants
    {
        public static JsonSerializerSettings SerializerSettings { get; }
        public static JsonSerializerSettings SkopeoSerializerSettings { get; }

        public static IDeserializer DockerComposeDeserializer { get; }

        static SerializationConstants()
        {
            SerializerSettings = new JsonSerializerSettings();
            SerializerSettings.TypeNameHandling = TypeNameHandling.None;
            SerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
            SerializerSettings.Formatting = Formatting.None;
            SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                }
            };

            SkopeoSerializerSettings = new JsonSerializerSettings();
            SkopeoSerializerSettings.TypeNameHandling = TypeNameHandling.None;
            SkopeoSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
            SkopeoSerializerSettings.Formatting = Formatting.None;
            SkopeoSerializerSettings.NullValueHandling = NullValueHandling.Ignore;


            DockerComposeDeserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

    }
}
