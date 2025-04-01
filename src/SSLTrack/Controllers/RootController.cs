namespace SSLTrack.Controllers;

[Route("api")]
[ApiController]
public class RootController : ControllerBase
{
    [HttpGet("health")]
    public ActionResult<Health> Health()
    {
        return Ok("healthy");
    }
}
