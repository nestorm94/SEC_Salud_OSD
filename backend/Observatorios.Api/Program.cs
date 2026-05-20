using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Endpoints;
using Observatorios.Api.Services;

var apiProjectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var repoRootForPublic = Path.GetFullPath(Path.Combine(apiProjectRoot, "..", ".."));
var publicDir = Path.Combine(repoRootForPublic, "public");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = apiProjectRoot,
    WebRootPath = Directory.Exists(publicDir) ? Path.GetFullPath(publicDir) : null
});

var repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));

builder.Services.AddObservatorioAuth(builder.Configuration);
builder.Services.AddSingleton<ObservatorioDbSchema>();
builder.Services.AddSingleton<ArchivosRepository>();
builder.Services.AddSingleton<CargasRepository>();
builder.Services.AddSingleton<UsuariosRepository>();
builder.Services.AddSingleton<DependenciasRepository>();
builder.Services.AddSingleton<RolesRepository>();
builder.Services.AddSingleton<PlantillasRepository>();
builder.Services.AddSingleton<DashboardRepository>();
builder.Services.AddSingleton<PoblacionVistasRepository>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ExcelValidationService>();
builder.Services.AddSingleton<CargaArchivoService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value;
    if (p is { Length: > 1 } && p.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && p.EndsWith('/'))
        ctx.Request.Path = new PathString(p[..^1]);
    await next();
});

if (Directory.Exists(app.Environment.WebRootPath ?? ""))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

var uploadsDir = Path.Combine(repoRoot, "uploads");
Directory.CreateDirectory(uploadsDir);

using (var scope = app.Services.CreateScope())
{
    var schema = scope.ServiceProvider.GetRequiredService<ObservatorioDbSchema>();
    await schema.EnsureAllAsync();
}

var connStr = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connStr))
{
    try
    {
        var sb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
        Console.WriteLine(
            $"[Observatorios.Api] SQL: {sb.DataSource} | {sb.InitialCatalog} | Esquema seguridad/cargas listo.");
    }
    catch { /* ignore */ }
}

Console.WriteLine($"[Observatorios.Api] wwwroot: {app.Environment.WebRootPath ?? "(no)"}");
Console.WriteLine("[Observatorios.Api] Login: POST /api/auth/login  |  Usuario inicial: admin / Admin123!");
Console.WriteLine();

app.MapObservatorioApi(repoRoot, uploadsDir);
app.Run();
