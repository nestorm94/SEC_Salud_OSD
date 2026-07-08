using Observatorios.Api.Auth;
using Observatorios.Api.Health;
using Observatorios.Api.Data;
using Observatorios.Api.Endpoints;
using Observatorios.Api.Services;

if (args is ["debug-osc", var xlsxPath, ..])
{
    using var fs = File.OpenRead(xlsxPath);
    var r = new OscPlantillaValidacionService().Validar(fs);
    Console.WriteLine($"Valido={r.EsValido} dict={r.TotalErroresDiccionario} data={r.TotalErroresData}");
    foreach (var e in r.ErroresDiccionario) Console.WriteLine($"[DIC] {e}");
    foreach (var e in r.ErroresData) Console.WriteLine($"[DATA] {e}");
    return;
}

if (args is ["debug-osc-gen", var outPath, ..])
{
    GenerarExcelPruebaOsc(outPath);
    using var fs = File.OpenRead(outPath);
    var r = new OscPlantillaValidacionService().Validar(fs);
    Console.WriteLine($"Generado: {outPath}");
    Console.WriteLine($"Valido={r.EsValido} dict={r.TotalErroresDiccionario} data={r.TotalErroresData}");
    foreach (var e in r.ErroresData) Console.WriteLine($"[DATA] {e}");
    return;
}

static void GenerarExcelPruebaOsc(string path)
{
    using var wb = new ClosedXML.Excel.XLWorkbook();
    var dict = wb.Worksheets.Add("Diccionario_datos");
    string[] enc = ["Id", "Nombre de la variable", "Descripción de la variable", "Llave Primaria", "Llave Foránea",
        "Campo obligatorio", "Id. de la variable", "Tipo de datos", "Longitud", "Dominios (Categorías, valores)",
        "Unidad de medida", "Campo calculado", "Fórmula aplicada"];
    for (var c = 0; c < enc.Length; c++) dict.Cell(5, c + 1).Value = enc[c];
    void Var(int row, string nombre, string tipo, string obl = "S", string dom = "")
    {
        dict.Cell(row, 1).Value = row - 5;
        dict.Cell(row, 2).Value = nombre;
        dict.Cell(row, 3).Value = nombre;
        dict.Cell(row, 4).Value = "N";
        dict.Cell(row, 5).Value = "N";
        dict.Cell(row, 6).Value = obl;
        dict.Cell(row, 7).Value = row - 5;
        dict.Cell(row, 8).Value = tipo;
        dict.Cell(row, 9).Value = "10";
        dict.Cell(row, 10).Value = string.IsNullOrEmpty(dom) ? "N/A" : dom;
        dict.Cell(row, 11).Value = "N";
        dict.Cell(row, 12).Value = "N";
        dict.Cell(row, 13).Value = "N/A";
    }
    Var(6, "CODIGO DIVIPOLA", "Numérico");
    Var(7, "AÑO", "Numérico");
    Var(8, "MUNICIPIO", "Texto");
    Var(9, "SEXO", "Texto", "S", "Total;Hombres;Mujeres");
    Var(10, "POBLACION", "Numérico");
    Var(11, "OBSERVACION", "Texto", "N");
    var data = wb.Worksheets.Add("DATA");
    data.Cell(1, 1).Value = "CODIGO DIVIPOLA";
    data.Cell(1, 2).Value = "AÑO";
    data.Cell(1, 3).Value = "MUNICIPIO";
    data.Cell(1, 4).Value = "SEXO";
    data.Cell(1, 5).Value = "POBLACION";
    data.Cell(1, 6).Value = "OBSERVACION";
    data.Cell(2, 1).Value = "85011"; data.Cell(2, 2).Value = 2024; data.Cell(2, 3).Value = "Yopal";
    data.Cell(2, 4).Value = "Total"; data.Cell(2, 5).Value = 190456;
    data.Cell(3, 1).Value = "ABC"; data.Cell(3, 2).Value = 2024; data.Cell(3, 3).Value = "Aguazul";
    data.Cell(3, 4).Value = "Hombres"; data.Cell(3, 5).Value = 20750;
    data.Cell(4, 1).Value = "85125"; data.Cell(4, 2).Value = 2024; data.Cell(4, 3).Value = "";
    data.Cell(4, 4).Value = "Mujeres"; data.Cell(4, 5).Value = 6800;
    data.Cell(5, 1).Value = "85400"; data.Cell(5, 2).Value = "dosm"; data.Cell(5, 3).Value = "Támara";
    data.Cell(5, 4).Value = "Total"; data.Cell(5, 5).Value = "cinco mil";
    wb.SaveAs(path);
}

var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Production;
var isDevelopment = envName.Equals(Environments.Development, StringComparison.OrdinalIgnoreCase);

string repoRoot;
string? webRootPath = null;
var apiProjectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

if (isDevelopment)
{
    repoRoot = Path.GetFullPath(Path.Combine(apiProjectRoot, "..", ".."));
    var angularDir = Path.Combine(repoRoot, "frontend", "dist", "frontend", "browser");
    var publicDir = Path.Combine(repoRoot, "public");
    if (Directory.Exists(angularDir))
        webRootPath = angularDir;
    else if (Directory.Exists(publicDir))
        webRootPath = publicDir;
}
else
{
    var contentRoot = Path.GetFullPath(AppContext.BaseDirectory);
    var wwwroot = Path.Combine(contentRoot, "wwwroot");
    if (Directory.Exists(wwwroot))
        webRootPath = wwwroot;
    repoRoot = contentRoot;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRootPath
});

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
builder.Services.AddSingleton<AsisRepository>();
builder.Services.AddSingleton<AsisExcelExportService>();
builder.Services.AddSingleton<CatalogoRepository>();
builder.Services.AddSingleton<PoblacionCatalogosRepository>();
builder.Services.AddSingleton<ICatalogoService, CatalogoService>();
builder.Services.AddSingleton<AreaTematicaRepository>();
builder.Services.AddSingleton<LineaTematicaRepository>();
builder.Services.AddSingleton<IndicadorRepository>();
builder.Services.AddSingleton<IndicadoresRepository>();
builder.Services.AddSingleton<ArchivoCargaRepository>();
builder.Services.AddSingleton<LineaTematicaSeedService>();
builder.Services.AddSingleton<UsuariosPruebaSeedService>();
builder.Services.AddSingleton<AuditoriaRepository>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<AuthorizationService>();
builder.Services.AddSingleton<ExcelValidationService>();
builder.Services.AddSingleton<IGeografiaValidacionService, GeografiaValidacionService>();
builder.Services.AddSingleton<OscPlantillaValidacionService>();
builder.Services.AddSingleton<ArchivoPrevalidacionService>();
builder.Services.AddSingleton<ArchivoFlujoService>();
builder.Services.AddSingleton<CargaArchivoService>();
builder.Services.AddSingleton<AreasTematicasSeedService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddHealthChecks()
    .AddCheck("live", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API en ejecución"), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["db"]);

// Respuestas JSON y documentos en UTF-8
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

var app = builder.Build();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("db")
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Observatorios API v1");
});
app.UseCors();
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = ex?.Message ?? "Error interno del servidor.",
            tipo = ex?.GetType().Name
        });
    });
});
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value;
    if (p is { Length: > 1 } && p.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && p.EndsWith('/'))
        ctx.Request.Path = new PathString(p[..^1]);
    await next();
});

var webRoot = app.Environment.WebRootPath ?? "";
var sirveAngularSpa = EsUiAngular(webRoot);

if (Directory.Exists(webRoot))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        DefaultFileNames = sirveAngularSpa ? ["index.html"] : ["login.html", "index.html"]
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
}

var uploadsDir = Path.Combine(repoRoot, "uploads");
Directory.CreateDirectory(uploadsDir);
Console.WriteLine($"[Observatorios.Api] uploads: {uploadsDir}");

var skipBootstrap = builder.Configuration.GetValue("Observatorio:SkipSchemaBootstrap", false);
var skipSeeds = builder.Configuration.GetValue("Observatorio:SkipStartupSeeds", false)
    || envName.Equals(Environments.Staging, StringComparison.OrdinalIgnoreCase)
    || envName.Equals("Testing", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("OBSERVATORIO_SKIP_STARTUP_SEEDS"), "true",
        StringComparison.OrdinalIgnoreCase);

using (var scope = app.Services.CreateScope())
{
    if (!skipBootstrap)
    {
        var schema = scope.ServiceProvider.GetRequiredService<ObservatorioDbSchema>();
        await schema.EnsureAllAsync();
    }

    if (!skipSeeds)
    {
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
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    try
    {
        var sb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        Console.WriteLine(
            $"[Observatorios.Api] SQL: {sb.DataSource} | {sb.InitialCatalog} | Esquema seguridad/cargas listo.");
    }
    catch { /* ignore */ }
}

Console.WriteLine($"[Observatorios.Api] wwwroot: {app.Environment.WebRootPath ?? "(no)"}");
Console.WriteLine("[Observatorios.Api] Login: POST /api/auth/login  |  admin@observatorio.gov.co / Admin123*");
Console.WriteLine();

app.MapObservatorioApi(repoRoot, uploadsDir);
app.MapControllers();

if (sirveAngularSpa)
{
    app.MapGet("/", () => Results.Redirect("/login"));
    var indexSpa = Path.Combine(webRoot, "index.html");
    app.MapFallback(async (HttpContext ctx) =>
    {
        if (EsRutaApiOSistema(ctx.Request.Path))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = "Recurso API no encontrado. Reinicie la API si acaba de desplegar cambios." });
            return;
        }
        if (File.Exists(indexSpa))
            await ctx.Response.SendFileAsync(indexSpa);
        else
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    });
    Console.WriteLine("[Observatorios.Api] UI: Angular (frontend/dist). Recargue con Ctrl+Shift+R tras npm run build.");
}
else if (Directory.Exists(webRoot))
{
    app.MapGet("/", () => Results.Redirect("/login.html"));
    Console.WriteLine("[Observatorios.Api] UI: portal HTML (public/). Para Angular: npm run build en frontend/ y reinicie la API.");
}

app.Run();

static bool EsRutaApiOSistema(PathString path) =>
    path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

static bool EsUiAngular(string webRoot)
{
    if (string.IsNullOrWhiteSpace(webRoot) || !Directory.Exists(webRoot))
        return false;

    if (webRoot.Contains("dist", StringComparison.OrdinalIgnoreCase)
        && webRoot.Contains("browser", StringComparison.OrdinalIgnoreCase))
        return true;

    var indexPath = Path.Combine(webRoot, "index.html");
    if (!File.Exists(indexPath))
        return false;

    if (File.Exists(Path.Combine(webRoot, "login.html")))
        return false;

    try
    {
        using var sr = new StreamReader(indexPath);
        var chunk = new char[4096];
        var read = sr.Read(chunk, 0, chunk.Length);
        return read > 0 && new string(chunk, 0, read).Contains("<app-root>", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}
