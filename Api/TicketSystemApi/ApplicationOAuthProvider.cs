using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;

public class ApplicationOAuthProvider : OAuthAuthorizationServerProvider
{
    public override async Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
    {
        context.Validated(); // no client app secrets — OK for first-party scenarios
    }

    public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
    {
        // validate the user (your existing logic) …

        var identity = new ClaimsIdentity(OAuthDefaults.AuthenticationType);

        // REQUIRED for your controller:
        identity.AddClaim(new Claim("crm_username", context.UserName));
        identity.AddClaim(new Claim("crm_password", context.Password));

        // optional:
        identity.AddClaim(new Claim(ClaimTypes.Name, context.UserName));

        var props = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        var ticket = new AuthenticationTicket(identity, props);
        context.Validated(ticket);
    }

    // Issue a new access token when client sends grant_type=refresh_token
    public override Task GrantRefreshToken(OAuthGrantRefreshTokenContext context)
    {
        var newIdentity = new ClaimsIdentity(context.Ticket.Identity);
        // optionally mutate claims here

        var props = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) // 8h new access token
        };

        var newTicket = new AuthenticationTicket(newIdentity, props);
        context.Validated(newTicket);
        return Task.FromResult(0);
    }

    // Add extra fields to the token response
    public override Task TokenEndpoint(OAuthTokenEndpointContext context)
    {
        var eightHours = (int)TimeSpan.FromHours(8).TotalSeconds;

        // ✅ Only add refresh_expires_in
        context.AdditionalResponseParameters["refresh_expires_in"] = eightHours;

        // ❌ Don't add expires_in here — OWIN already adds it
        return Task.FromResult(0);
    }
}
