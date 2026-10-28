namespace SSLTrack.Controllers;

[Route("account")]
[Authorize(Policy = "Human")]
public sealed class AccountController(AccountService accounts, ServiceAccountService serviceAccounts, SmtpSettingsService smtp, MailService mail, IConfiguration configuration) : Controller
{
    [HttpPost("login"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || email.Length > 254 || password.Length > 128)
        {
            return Redirect("/login?error=credentials");
        }

        var user = await accounts.ValidatePasswordAsync(email, password);
        if (user is null)
        {
            return Redirect("/login?error=credentials");
        }

        await HttpContext.SignInAsync(AuthSetup.Cookie, AccountService.Principal(user));
        return LocalRedirect(user.RequirePasswordChange ? "/change-password" : Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpGet("oidc"), AllowAnonymous, EnableRateLimiting("login")]
    public IActionResult Oidc(string? returnUrl)
    {
        if (!configuration.GetValue<bool>("Authentication:Oidc:Enabled"))
        {
            return Redirect("/login?error=oidc-disabled");
        }

        return Challenge(new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/" }, "oidc");
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSetup.Cookie);
        return Redirect("/login");
    }

    [HttpPost("password"), ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Password(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await accounts.CurrentAsync(User, true);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            if (newPassword != confirmPassword)
            {
                throw new InvalidOperationException("Passwords do not match.");
            }

            await accounts.ChangePasswordAsync(user.Id, currentPassword ?? "", newPassword ?? "");
            await HttpContext.SignOutAsync(AuthSetup.Cookie);
            return Redirect("/login?message=password-changed");
        }
        catch (InvalidOperationException ex)
        {
            return Redirect("/change-password?error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string displayName, string avatarType, string? avatarUrl)
    {
        var user = await accounts.CurrentAsync(User);
        if (user is null)
        {
            return Forbid();
        }

        try
        {
            await accounts.UpdateProfileAsync(user.Id, displayName, avatarType, avatarUrl);
            return Redirect("/profile?saved=true");
        }
        catch (InvalidOperationException ex)
        {
            return Redirect("/profile?error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("users"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser([FromForm] UserEdit edit, string? password)
    {
        if ((await accounts.CurrentAsync(User))?.Role != "Admin")
        {
            return Forbid();
        }

        try
        {
            if (!ModelState.IsValid)
            {
                throw new InvalidOperationException("Check the email, display name, role, and sign-in method.");
            }

            await accounts.SaveAsync(edit, password);
            return Redirect("/users?saved=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ValidationException)
        {
            return Redirect("/users?edit=" + edit.Id + "&error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("service-accounts"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveServiceAccount([FromForm] ServiceAccountEdit edit, string? password)
    {
        if ((await accounts.CurrentAsync(User))?.Role != "Admin")
        {
            return Forbid();
        }

        try
        {
            if (!ModelState.IsValid)
            {
                throw new InvalidOperationException("Check the email and assigned agent.");
            }

            await serviceAccounts.SaveAsync(edit, password);
            return Redirect("/service-accounts?saved=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ValidationException)
        {
            return Redirect("/service-accounts?edit=" + edit.Id + "&error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("service-accounts/delete"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteServiceAccount(int id)
    {
        if ((await accounts.CurrentAsync(User))?.Role != "Admin")
        {
            return Forbid();
        }

        try
        {
            await serviceAccounts.DeleteAsync(id);
            return Redirect("/service-accounts?saved=true");
        }
        catch (InvalidOperationException ex)
        {
            return Redirect("/service-accounts?error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("smtp"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSmtp([FromForm] SmtpSettingsEdit edit)
    {
        if ((await accounts.CurrentAsync(User))?.Role != "Admin")
        {
            return Forbid();
        }

        try
        {
            if (!ModelState.IsValid)
            {
                throw new InvalidOperationException("Check the sender, recipients, host, port, and TLS mode.");
            }

            await smtp.SaveAsync(edit);
            return Redirect("/notifications?saved=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ValidationException)
        {
            return Redirect("/notifications?error=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpPost("smtp/test"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSmtp()
    {
        if ((await accounts.CurrentAsync(User))?.Role != "Admin")
        {
            return Forbid();
        }

        try
        {
            mail.Subject = "SSLTrack notification test";
            mail.Body = "This is a test email from SSLTrack.";
            await mail.SendMailAsync();
            return Redirect("/notifications?tested=true");
        }
        catch (Exception)
        {
            return Redirect("/notifications?error=" + Uri.EscapeDataString("Test email failed. Check SMTP settings and server logs."));
        }
    }
}
