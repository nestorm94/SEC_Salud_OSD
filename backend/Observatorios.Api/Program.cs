using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Endpoints;
using Observatorios.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var contentRoot = builder.Environment.ContentRootPath;

string repoRoot;
string? webRootPath = null;

if (builder.Environment.IsDevelopment())
{
    var apiProjectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    var publicDir = Path.GetFullPath(Path.Combine(apiProjectRoot, "..", "..", "public"));
    if (Directory.Exists(publicDir))
        webRootPath = publicDir;
    repoRoot = Path.GetFullPath(Path.Combine(apiProjectRoot, "..", ".."));
}
else
{
    var wwwroot = Path.Combine(contentRoot, "wwwroot");
    if (Directory.Exists(wwwroot))
        webRootPath = wwwroot;
    repoRoot = contentRoot;
}

if (webRootPath is not null)
    builder.WebHost.UseWebRoot(webRootPath);

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
builder.Services.AddSingleton<CatalogoRepository>();
builder.Services.AddSingleton<AreaTematicaRepository>();
builder.Services.AddSingleton<LineaTematicaRepository>();
builder.Services.AddSingleton<IndicadorRepository>();
builder.Services.AddSingleton<ArchivoCargaRepository>();
builder.Services.AddSingleton<LineaTematicaSeedService>();
builder.Services.AddSingleton<UsuariosPruebaSeedService>();
builder.Services.AddSingleton<AuditoriaRepository>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<AuthorizationService>();
builder.Services.AddSingleton<ExcelValidationService>();
builder.Services.AddSingleton<ArchivoPrevalidacionService>();
builder.Services.AddSingleton<ArchivoFlujoService>();
builder.Services.AddSingleton<CargaArchivoService>();
builder.Services.AddSingleton<AreasTematicasSeedService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Respuestas JSON y documentos en UTF-8
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

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
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        DefaultFileNames = ["login.html", "index.html"]
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var ext = Path.GetExtension(ctx.File.Name);
            if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.ContentType = "text/html; charset=utf-8";
            else if (ext.Equals(".css", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.ContentType = "text/css; charset=utf-8";
            else if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.ContentType = "application/javascript; charset=utf-8";
        }
    });
    app.MapGet("/", () => Results.Redirect("/login.html"));
}

var uploadsDir = Path.Combine(repoRoot, "uploads");
Directory.CreateDirectory(uploadsDir);

using (var scope = app.Services.CreateScope())
{
    var schema = scope.ServiceProvider.GetRequiredService<ObservatorioDbSchema>();
    await schema.EnsureAllAsync();
    var seed = scope.ServiceProvider.GetRequiredService<AreasTematicasSeedService>();
    var seedResult = await seed.ImportarSiExisteAsync(repoRoot);
    if (seedResult.Ok)
        Console.WriteLine($"[Observatorios.Api] Seed áreas: {seedResult.Mensaje} ({seedResult.Areas} áreas)");
    var lineaSeed = scope.ServiceProvider.GetRequiredService<LineaTematicaSeedService>();
    var (nLineas, nInd) = await lineaSeed.EnsureSeedAsync();
    Console.WriteLine($"[Observatorios.Api] Líneas temáticas: {nLineas} líneas, {nInd} indicadores (seed).");
    var usrPrueba = scope.ServiceProvider.GetRequiredService<UsuariosPruebaSeedService>();
    var nuevos = await usrPrueba.EnsureSeedAsync();
    if (nuevos > 0)
        Console.WriteLine($"[Observatorios.Api] Usuarios de prueba creados: {nuevos} (rol RESPONSABLE_TEMATICO, clave {UsuariosPruebaSeedService.PasswordPrueba}).");
    Console.WriteLine("[Observatorios.Api] Prueba por línea: prueba.aseg | prueba.ecnt | prueba.vsp | prueba.etc | prueba.econ");
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
Console.WriteLine("[Observatorios.Api] Login: POST /api/auth/login  |  admin@observatorio.gov.co / Admin123*");
Console.WriteLine();

app.MapObservatorioApi(repoRoot, uploadsDir);
app.Run();
