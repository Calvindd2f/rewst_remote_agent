namespace RewstAgent.Configuration.Models
{
    public class ConfigurationData
    {
        public string RewstOrgId { get; set; }
        public string RewstEngineHost { get; set; }
        public string AzureIoTHubHost { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
    }
}