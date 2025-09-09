namespace SSLTrackAgent.Models;

internal class Configurations
{
    public int Delay { get; set; }
    public string SSLTrackApiAddress { get; set; }
    public string GetDomainsEndpoint { get; set; }
    public string UpdateDomainEndpoint { get; set; }
    public string LogEndpoint { get; set; }
    public string PingEndpoint { get; set; }
    public int AgentId { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
