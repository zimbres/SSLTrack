namespace SSLTrack.Models;

public class Configurations
{
    public int DaysToExpiration { get; set; }
    public bool AlertsEnabled { get; set; }
    public string? UpdateCron { get; set; }
    public string? AlertCron { get; set; }
}
