using YamlDotNet.Serialization;


// Slimmed down version of ServiceConfiguration and friends from
// https://github.com/hudl/dotnet-microservice-cli-tools/blob/cdbb00c73c66f4cf7375f3b34f9eb8486355ed00/src/Hudl.Marvel.Tools.Shared/ServiceConfigurationModel/ServiceConfiguration.cs
public class ServiceConfigFile
{
    // [YamlMember(Alias = "service")]
    // public ServiceConfigSection Service { get; set; }

    // [YamlMember(Alias = "environment")]
    // public string Environment { get; set; }

    // [YamlMember(Alias = "build")]
    // public BuildConfigSection Build { get; set; }

    [YamlMember(Alias = "infrastructure")]
    public InfrastructureSection Infrastructure { get; set; }

    public class InfrastructureSection
    {
        [YamlMember(Alias = "web")]
        public WebServer Web { get; set; }
    }

    public class WebServer
    {
        public WebServer()
        {
            Routes = new List<string>();
        }

        [YamlMember(Alias = "routes")]
        public List<string> Routes { get; set; }

        [YamlMember(Alias = "preferences")]
        public WebServerPreferences Preferences {get;set;}
    }

    public class WebServerPreferences
    {
        [YamlMember(Alias = "janus_enabled")]
        public bool JanusEnabled { get; set; }
    }
}

