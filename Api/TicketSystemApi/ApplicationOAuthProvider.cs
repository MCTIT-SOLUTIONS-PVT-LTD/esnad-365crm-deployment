using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using Microsoft.Crm.Sdk.Messages;   // WhoAmI
using TicketSystemApi.Services;     // CrmService
using Serilog;

public class ApplicationOAuthProvider : OAuthAuthorizationServerProvider
{
    private const string CLAIM_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    // Log Token Generation and Expiration
    private void LogTokenGeneration(string username, string tokenType, DateTime issuedUtc, DateTime expiresUtc)
    {
        // Log token generation (you can replace this with any other logging framework like NLog, Serilog, etc.)
        Log.Information("{TokenType} generated for user: {Username}, Issued at: {IssuedUtc}, Expires at: {ExpiresUtc}",
            tokenType, username, issuedUtc, expiresUtc);
    }

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

            // Validate username/password against CRM
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

            // Store creds for use in controller (no service account)
            identity.AddClaim(new Claim("crm_username", username));
            identity.AddClaim(new Claim("crm_password", password));

            var props = new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            // Log the Access Token generation
            LogTokenGeneration(username, "Access Token", DateTime.UtcNow, DateTime.UtcNow.AddHours(8));

            context.Validated(new AuthenticationTicket(identity, props));
        }
        catch
        {
            context.SetError("invalid_grant", "Invalid username or password.");
        }
    }

    public override Task GrantRefreshToken(OAuthGrantRefreshTokenContext context)
    {
        // Extract existing identity for the refresh token
        var newIdentity = new ClaimsIdentity(context.Ticket.Identity);

        var props = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        // Log the Refresh Token generation
        var username = newIdentity.FindFirst("crm_username")?.Value;
        LogTokenGeneration(username, "Refresh Token", DateTime.UtcNow, DateTime.UtcNow.AddHours(8));

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
