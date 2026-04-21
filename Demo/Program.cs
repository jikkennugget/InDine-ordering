global using Demo.Models;
global using Demo;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

// Configure request size limits for larger file uploads
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
});

// Ensure DataDirectory points to the content root (next to DB.mdf)
AppDomain.CurrentDomain.SetData("DataDirectory", builder.Environment.ContentRootPath);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddSqlServer<DB>(connectionString);

builder.Services.AddScoped<Helper>();
builder.Services.AddAuthentication().AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();

// Add hCaptcha service
builder.Services.AddHttpClient<Demo.Services.IHCaptchaService, Demo.Services.HCaptchaService>();

// Add password reset service
builder.Services.AddScoped<Demo.Services.IPasswordResetService, Demo.Services.PasswordResetService>();

var app = builder.Build();
// Import Admin users from Data/Users.txt at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    try
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "Users.txt");
        if (File.Exists(path))
        {
            var lines = File.ReadAllLines(path);
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var parts = raw.Split('\t');
                if (parts.Length < 4) continue;

                var email = parts[0].Trim();
                var hash = parts[1].Trim();
                var name = parts[2].Trim();
                var role = parts[3].Trim();

                if (!email.Contains('@')) continue;
                if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) continue;
                if (db.Users.Any(u => u.Email == email)) continue;

                db.Admins.Add(new Admin
                {
                    Email = email,
                    Name = name,
                    Hash = hash,
                });
            }
            db.SaveChanges();
        }
    }
    catch { }
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization("en-MY");
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Always show Login on startup
app.MapGet("/", (HttpContext ctx) => Results.Redirect("/Account/Login")).ExcludeFromDescription();

app.MapDefaultControllerRoute();
app.Run();
