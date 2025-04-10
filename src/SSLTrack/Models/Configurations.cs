namespace SSLTrack.Models;

public class Configurations
{
    public int DaysToExpiration { get; set; }
    public bool AlertsEnabled { get; set; }
    public string? UpdateCron { get; set; }
    public string? AlertCron { get; set; }
    public string? ClearLogsCron { get; set; }
    public string ApiBaseAddress { get; set; }
    public string LogsEndpoint { get; set; }
    public string ClearLogsEndpoint { get; set; }
    public string AgentsStatusEndpoint { get; set; }
}
