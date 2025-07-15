var builder = WebApplication.CreateBuilder(args);

// Optional: Add services if needed
// builder.Services.AddRazorPages(); 

var app = builder.Build();

// Ensure it serves wwwroot static files
app.UseDefaultFiles();      // 👈 serves index.html by default if available
app.UseStaticFiles();       // 👈 serves all files from wwwroot

// Optional: Fallback to index.html for SPA apps (like React/Vue/Angular)
// app.MapFallbackToFile("index.html");

app.Run();
