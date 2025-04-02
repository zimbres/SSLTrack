namespace SSLTrack.Models;

public class LogEntry
{
    public int Id { get; set; }
    public int Agent { get; set; }
    public string Domain { get; set; }
    public DateTime DateTime { get; set; }
    public string Message { get; set; }
}

public class LogEntryDto
{
    public int Id { get; set; }
    public string Agent { get; set; }
    public string Domain { get; set; }
    public DateTime DateTime { get; set; }
    public string Message { get; set; }
    public bool ShowDetails { get; set; }
}
