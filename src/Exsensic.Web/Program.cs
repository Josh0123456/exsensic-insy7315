using Exsensic.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Hosting (Josh): must run first so later middleware sees the real scheme and client IP.
// Also adds HSTS and HTTPS redirection outside Development.
app.UseExsensicHosting();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
