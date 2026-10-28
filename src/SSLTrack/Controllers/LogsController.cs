namespace SSLTrack.Controllers;

[Route("api/logs"), ApiController, Authorize(Policy = "AgentOrHuman")]
public class LogsController(LogStore store) : ControllerBase
{
    [HttpGet, Authorize(Policy = "Human")]
    public IActionResult Logs() => Ok(store.Logs);
    [HttpGet("{id:int}"), Authorize(Policy = "Human")]
    public IActionResult Logs(int id) => store.Logs.TryGetValue(id, out var log) ? Ok(new Dictionary<int, Log> { { id, log } }) : NotFound();
    [HttpGet("agent/{agentId:int}"), Authorize(Policy = "Human")]
    public IActionResult GetLogsByAgent(int agentId) => Ok(store.Logs.Where(x => x.Value.Agent == agentId).ToDictionary());
    [HttpPost]
    public IActionResult Logs(Log value)
    {
        if (User.FindFirstValue("kind") == "agent" && User.FindFirstValue("agent_id") != value.Agent.ToString())
        {
            return Forbid();
        }

        if (User.FindFirstValue("kind") == "human" && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var id = store.Add(value);
        return Created($"/api/logs/{id}", new
        {
            id
        });
    }

    [HttpPost("clear"), Authorize(Policy = "Admin")]
    public IActionResult ClearLogs()
    {
        store.Logs.Clear();
        return NoContent();
    }
}
