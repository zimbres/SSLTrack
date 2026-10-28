namespace SSLTrack.Models;

public class AuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.HasClaim("kind", "human") && user.IsInRole("Admin") && !user.HasClaim("password_change", "True");
    }
}
