namespace SSLTrackAgent.Models;

internal class HttpClientConfiguration
{
    public bool UseProxy { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public bool IgnoreSsl { get; set; }
}