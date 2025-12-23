namespace SSLTrack.Controllers;

[Route("api")]
[ApiController]
public class RootController : ControllerBase
{
    private readonly static ConcurrentDictionary<int, DateTime> storage = new();

    [HttpGet("health")]
    public ActionResult<Health> Health()
    {
        return Ok("healthy");
    }

    [HttpHead("ping/{id}")]
    public IActionResult Ping(int id)
    {
        storage[id] = DateTime.Now;
        return Ok();
    }

    [HttpGet("ping")]
    public IActionResult Logs()
    {
        return Ok(storage);
    }
}
