namespace SSLTrack.Services;

public sealed class LogService(LogStore store, IAgentRepository agents, AccessGuard access)
{
    public Task ClearLogs()
    {
        store.Logs.Clear();
        return Task.CompletedTask;
    }

    public Task PushLog(string message, string domain)
    {
        store.Add(new Log { Agent = 0, Domain = domain, DateTime = DateTime.Now, Message = message });
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<LogEntryDto>> GetEntriesAsync()
    {
        await access.RequireHumanAsync();
        var dictionary = (await agents.GetAll()).ToDictionary(x => x.Id, x => x.Name ?? "Unknown");
        return store.Logs.OrderByDescending(x => x.Key).Select(x => new LogEntryDto { Id = x.Key, Agent = dictionary.GetValueOrDefault(x.Value.Agent, "Local"), Domain = x.Value.Domain, DateTime = x.Value.DateTime, Message = x.Value.Message }).ToList();
    }
}
