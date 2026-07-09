using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Services;

/// <summary>Inserta líneas temáticas e indicadores base del catálogo OSD si faltan en BD.</summary>
public sealed class LineaTematicaSeedService(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    private static readonly (string Codigo, string Nombre, string[] Indicadores)[] Catalogo =
    [
        ("LT-ASEG", "Aseguramiento y prestación de servicios de salud",
        [
            "Oferta de Servicios de Salud",
            "Calidad en la atención",
            "Salud sexual y reproductiva",
            "Demográficos y estadísticas vitales",
            "Aseguramiento en salud",
            "Salud y ámbito laboral",
            "Economía de la salud",
            "Enfermedades inmunoprevenibles"
        ]),
        ("LT-ECNT", "Enfermedades Crónicas No Transmisibles",
        [
            "Derecho Humano a la Alimentación",
            "Salud mental y convivencia",
            "Enfermedades Cardiometabólicas",
            "Neoplasias",
            "Ciudades, Entornos y Ruralidades Saludables y Sostenibles",
            "Poblaciones vulnerables",
            "Accidentes de tránsito",
            "Salud oral, visual y auditiva"
        ]),
        ("LT-VSP", "Vigilancia en Salud Pública",
        [
            "Carga de enfermedad",
            "Análisis de mortalidad",
            "Análisis ecológicos",
            "Vigilancia por Laboratorio"
        ]),
        ("LT-ETC", "Enfermedades transmisibles y relacionadas con el cambio climático",
        [
            "Enfermedades transmitidas por vectores y zoonosis",
            "Salud ambiental y cambio climático",
            "Emergencias y desastres",
            "Enfermedades emergentes, re-emergentes y desatendidas"
        ]),
        ("LT-ECON", "Economía de la Salud",
        [
            "Evaluación de costos en salud",
            "Evaluación de costo-efectividad y costo utilidad",
            "Evaluación de costo-beneficio"
        ])
    ];

    /// <summary>Ejecuta seed idempotente de líneas e indicadores; retorna conteos creados.</summary>
    public async Task<(int Lineas, int Indicadores)> EnsureSeedAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var lineas = 0;
        var indicadores = 0;

        foreach (var (codigoLinea, nombreLinea, inds) in Catalogo)
        {
            var lineaId = await UpsertLineaAsync(con, codigoLinea, nombreLinea, ct);
            lineas++;
            var orden = 1;
            foreach (var nombreInd in inds)
            {
                var codigoInd = $"IND-{codigoLinea}-{orden:D2}";
                await UpsertIndicadorAsync(con, lineaId, codigoInd, nombreInd, ct);
                indicadores++;
                orden++;
            }
        }

        return (lineas, indicadores);
    }

    private static async Task<int> UpsertLineaAsync(SqlConnection con, string codigo, string nombre, CancellationToken ct)
    {
        const string sql = """
IF EXISTS (SELECT 1 FROM dbo.LineaTematica WHERE Codigo = @Codigo)
BEGIN
    UPDATE dbo.LineaTematica SET Nombre = @Nombre, Activo = 1 WHERE Codigo = @Codigo;
    SELECT Id FROM dbo.LineaTematica WHERE Codigo = @Codigo;
END
ELSE
BEGIN
    INSERT INTO dbo.LineaTematica (Codigo, Nombre) VALUES (@Codigo, @Nombre);
    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task UpsertIndicadorAsync(SqlConnection con, int lineaId, string codigo, string nombre, CancellationToken ct)
    {
        const string sql = """
MERGE dbo.Indicador AS t
USING (SELECT @LineaId AS LineaTematicaId, @Codigo AS Codigo, @Nombre AS Nombre) AS s
ON t.LineaTematicaId = s.LineaTematicaId AND t.Codigo = s.Codigo
WHEN MATCHED THEN UPDATE SET Nombre = s.Nombre, Activo = 1
WHEN NOT MATCHED THEN INSERT (LineaTematicaId, Codigo, Nombre) VALUES (s.LineaTematicaId, s.Codigo, s.Nombre);
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@LineaId", lineaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
