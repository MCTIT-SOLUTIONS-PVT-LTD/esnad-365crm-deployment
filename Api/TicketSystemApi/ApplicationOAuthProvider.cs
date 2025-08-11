using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using Microsoft.Crm.Sdk.Messages;   // WhoAmI
using TicketSystemApi.Services;     // CrmService

public class ApplicationOAuthProvider : OAuthAuthorizationServerProvider
{
    // Avoid ClaimTypes enum issues entirely
    private const string CLAIM_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    public override async Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
    {
        context.Validated();
    }

    public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
    {
        try
        {
            var username = context.UserName;
            var password = context.Password;

            // ✅ Validate username/password against CRM
            var org = new CrmService().GetService1(username, password);
            var who = (WhoAmIResponse)org.Execute(new WhoAmIRequest());
            if (who == null || who.UserId == Guid.Empty)
            {
                context.SetError("invalid_grant", "Invalid username or password.");
                return;
            }

            // Build identity (token is encrypted/protected by OWIN)
            var identity = new ClaimsIdentity(context.Options.AuthenticationType);
            identity.AddClaim(new Claim(CLAIM_NAME, username));

            // 🔐 Store creds so controller can connect AS THE USER (no service account)
            identity.AddClaim(new Claim("crm_username", username));
            identity.AddClaim(new Claim("crm_password", password));

            var props = new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            context.Validated(new AuthenticationTicket(identity, props));
        }
        catch
        {
            context.SetError("invalid_grant", "Invalid username or password.");
        }
    }

    public override Task GrantRefreshToken(OAuthGrantRefreshTokenContext context)
    {
        var newIdentity = new ClaimsIdentity(context.Ticket.Identity); // keep claims incl. creds
        var props = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };
        context.Validated(new AuthenticationTicket(newIdentity, props));
        return Task.FromResult(0);
    }

    public override Task TokenEndpoint(OAuthTokenEndpointContext context)
    {
        var eightHours = (int)TimeSpan.FromHours(8).TotalSeconds;
        context.AdditionalResponseParameters["refresh_expires_in"] = eightHours;
        return Task.FromResult(0);
    }
}
