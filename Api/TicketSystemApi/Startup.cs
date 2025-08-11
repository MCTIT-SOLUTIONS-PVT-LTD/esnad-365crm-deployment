using System;
using Microsoft.Owin;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.Owin.Security.OAuth;
using Owin;

[assembly: OwinStartup(typeof(TicketSystemApi.Startup))]
namespace TicketSystemApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // move your auth wiring here
            ConfigureAuth(app);
        }

        private void ConfigureAuth(IAppBuilder app)
        {
            var oauthOptions = new OAuthAuthorizationServerOptions
            {
                TokenEndpointPath = new PathString("/token"),
                Provider = new ApplicationOAuthProvider(),
                AccessTokenExpireTimeSpan = TimeSpan.FromHours(8),
                AllowInsecureHttp = true,

                RefreshTokenProvider = new AuthenticationTokenProvider
                {
                    OnCreate = ctx =>
                    {
                        ctx.Ticket.Properties.IssuedUtc = DateTimeOffset.UtcNow;
                        ctx.Ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8);
                        ctx.SetToken(ctx.SerializeTicket());
                    },
                    OnReceive = ctx => ctx.DeserializeTicket(ctx.Token)
                }
            };

            app.UseOAuthAuthorizationServer(oauthOptions);
            app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions());
        }
    }
}
