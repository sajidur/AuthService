using AuthMicroservice.Model;

public class Application : BaseEntity
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public string AppKey { get; set; }
    public string AppSecret { get; set; }

    // JWT aud/iss claims for this tenant's issued tokens — per-app rather than a single
    // shared value, matching AppSecret's per-app signing key (see UserService.GetToken).
    public string? Audience { get; set; }
    public string? Issuer { get; set; }

    public string? RedirectUri { get; set; }
    public string? ContactEmail { get; set; }

}


