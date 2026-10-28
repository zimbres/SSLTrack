namespace SSLTrack.Controllers;

[Route("api"), ApiController, Authorize(Policy = "AgentOrHuman")]
public class RootController(LogStore store) : ControllerBase
{
    [HttpGet("health"), AllowAnonymous]
    public IActionResult Health() => Ok("healthy");
    [HttpHead("ping/{id:int}")]
    public IActionResult Ping(int id)
    {
        if (User.FindFirstValue("kind") == "agent" && User.FindFirstValue("agent_id") != id.ToString())
        {
            return Forbid();
        }

        if (User.FindFirstValue("kind") == "human" && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        store.Heartbeats[id] = DateTime.Now;
        return Ok();
    }

    [HttpGet("ping"), Authorize(Policy = "Human")]
    public IActionResult Logs() => Ok(store.Heartbeats);
}
