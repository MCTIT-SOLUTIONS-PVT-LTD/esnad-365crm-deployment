using Microsoft.Owin;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.Owin.Security.OAuth;
using Owin;
using Serilog;
using System;
using System.IO;

[assembly: OwinStartup(typeof(TicketSystemApi.Startup))]
namespace TicketSystemApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Get the log folder path from an environment variable or default path
            var logFolderPath = Environment.GetEnvironmentVariable("LOG_FOLDER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            // Ensure the directory exists
            Directory.CreateDirectory(logFolderPath);

            // Set up logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(logFolderPath, "tokens.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

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
