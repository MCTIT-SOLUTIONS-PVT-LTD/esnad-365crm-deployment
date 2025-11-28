using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Serve static files (index.html by default)
app.UseDefaultFiles();
app.UseStaticFiles();

// Map /Visitor to VisitorSurvey.html
app.MapGet("/Visitor", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "VisitorSurvey.html")
    );
});

// Map /KITicket to feedback.html
app.MapGet("/KITicket", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "feedback.html")
    );
});

// If you still want SPA fallback, uncomment below:
// app.MapFallbackToFile("index.html");

app.Run();
