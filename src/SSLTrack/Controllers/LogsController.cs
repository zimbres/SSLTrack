namespace SSLTrack.Controllers;

[Route("api/logs")]
[ApiController]
public class LogsController : ControllerBase
{
    private readonly static ConcurrentDictionary<int, Log> storage = new();
    private static int idCounter = 0;

    [HttpGet()]
    public IActionResult Logs()
    {
        return Ok(storage);
    }

    [HttpGet("{id}")]
    public IActionResult Logs(int id)
    {
        if (storage.TryGetValue(id, out var value) && value is not null)
        {
            var response = new Dictionary<int, object>
        {
            {
                id, new
                {
                    agent = value.Agent,
                    domain = value.Domain,
                    dateTime = value.DateTime,
                    message = value.Message
                }
            }
        };
            return Ok(response);
        }
        return NotFound();
    }


    [HttpGet("agent/{agentId}")]
    public IActionResult GetLogsByAgent(int agentId)
    {
        var logs = storage.Where(kv => kv.Value.Agent == agentId).ToDictionary(kv => kv.Key, kv => kv.Value);

        if (logs.Count == 0)
            return NotFound();

        return Ok(logs);
    }

    [HttpPost()]
    public IActionResult Logs(Log value)
    {
        var id = Interlocked.Increment(ref idCounter);
        storage[id] = value;
        var result = Results.Created($"/logs/{id}", new { id, value });
        return Ok(result);
    }

    [HttpPost("clear")]
    public IActionResult ClearLogs()
    {
        storage.Clear();
        return NoContent();
    }
}
