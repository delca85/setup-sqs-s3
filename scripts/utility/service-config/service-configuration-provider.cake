#load "service-configuration-file.cake"

using System;
using YamlDotNet.Serialization;

public class ServiceConfigurationProvider
{
    private readonly string _configFilePathname;

    private ServiceConfigurationProvider(string configFilePathname)
    {
        _configFilePathname = configFilePathname;
    }

    private async System.Threading.Tasks.Task<ServiceConfigFile> Load()
    {
        if (!System.IO.File.Exists(_configFilePathname))
        {
            throw new FileNotFoundException($"Could not find service config file at {_configFilePathname}.");
        }

        var yaml = await System.IO.File.ReadAllTextAsync(_configFilePathname);

        var deserializer = new DeserializerBuilder()
            // Ignore extra properties in the YAML. We only need to model the properties we need to read.
            .IgnoreUnmatchedProperties()
            .Build();

        try
        {
            return deserializer.Deserialize<ServiceConfigFile>(yaml);
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to parse service config YAML from {_configFilePathname}. Ensure YAML is formatted correctly.", e);
        }
    }

    private static Lazy<System.Threading.Tasks.Task<ServiceConfigFile>> LoadServiceDefinitionFileTask = new Lazy<System.Threading.Tasks.Task<ServiceConfigFile>>(() => {
        return new ServiceConfigurationProvider(RepoMetadata.Current.ServiceDefinitionFilePath).Load();
    });

    public static System.Threading.Tasks.Task<ServiceConfigFile> LoadServiceDefinitionFile() => LoadServiceDefinitionFileTask.Value;
}
