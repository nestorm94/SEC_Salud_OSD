using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;
using Observatorios.Api.Services;

namespace Observatorios.Api.Data;

public sealed class CargasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<int> CrearCargaAsync(
        int archivoId,
        int dependenciaId,
        int usuarioId,
        string estadoInicial,
        CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.CargasArchivo (ArchivoId, DependenciaId, UsuarioId, Estado)
OUTPUT INSERTED.Id
VALUES (@ArchivoId, @DepId, @UserId, @Estado);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@DepId", dependenciaId);
        cmd.Parameters.AddWithValue("@UserId", usuarioId);
        cmd.Parameters.AddWithValue("@Estado", estadoInicial);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarEstadoAsync(int cargaId, string estado, string? observaciones, CancellationToken ct)
    {
        const string sql = """
UPDATE dbo.CargasArchivo
SET Estado = @Estado,
    Observaciones = COALESCE(@Obs, Observaciones),
    FechaFin = CASE WHEN @Estado IN (N'VALIDADO_OK', N'VALIDADO_CON_ERRORES', N'APROBADO', N'RECHAZADO')
        THEN SYSUTCDATETIME() ELSE FechaFin END
WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        cmd.Parameters.AddWithValue("@Estado", estado);
        cmd.Parameters.AddWithValue("@Obs", (object?)observaciones ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GuardarDiccionarioAsync(
        int cargaId,
        IReadOnlyList<CampoDiccionarioDto> campos,
        CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        const string insDict = """
INSERT INTO dbo.DiccionarioArchivo (CargaArchivoId)
OUTPUT INSERTED.Id
VALUES (@CargaId);
""";
        await using var dcmd = new SqlCommand(insDict, con, tx);
        dcmd.Parameters.AddWithValue("@CargaId", cargaId);
        var dictId = Convert.ToInt32(await dcmd.ExecuteScalarAsync(ct));

        const string insCampo = """
INSERT INTO dbo.CamposDiccionario
    (DiccionarioArchivoId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden)
VALUES (@DictId, @Nombre, @Tipo, @Obl, @Desc, @Len, @Fmt, @Val, @Ord);
""";
        foreach (var c in campos)
        {
            await using var cc = new SqlCommand(insCampo, con, tx);
            cc.Parameters.AddWithValue("@DictId", dictId);
            cc.Parameters.AddWithValue("@Nombre", c.NombreCampo);
            cc.Parameters.AddWithValue("@Tipo", c.TipoDato);
            cc.Parameters.AddWithValue("@Obl", c.Obligatorio);
            cc.Parameters.AddWithValue("@Desc", (object?)c.Descripcion ?? DBNull.Value);
            cc.Parameters.AddWithValue("@Len", (object?)c.Longitud ?? DBNull.Value);
            cc.Parameters.AddWithValue("@Fmt", (object?)c.Formato ?? DBNull.Value);
            cc.Parameters.AddWithValue("@Val", (object?)c.ValoresPermitidos ?? DBNull.Value);
            cc.Parameters.AddWithValue("@Ord", c.Orden);
            await cc.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return dictId;
    }

    public async Task GuardarDatosAsync(int cargaId, IReadOnlyList<DatosFilaDto> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return;
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        const string sql = """
INSERT INTO dbo.DatosCargados (CargaArchivoId, NumeroFila, DatosJson)
VALUES (@CargaId, @Fila, @Json);
""";
        foreach (var f in filas)
        {
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CargaId", cargaId);
            cmd.Parameters.AddWithValue("@Fila", f.NumeroFila);
            cmd.Parameters.AddWithValue("@Json", f.DatosJson);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task GuardarErroresAsync(int cargaId, IReadOnlyList<ValidationErrorDto> errores, CancellationToken ct)
    {
        if (errores.Count == 0) return;
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        const string sql = """
INSERT INTO dbo.ErroresValidacion (CargaArchivoId, NumeroFila, NombreColumna, Mensaje, TipoError)
VALUES (@CargaId, @Fila, @Col, @Msg, @Tipo);
""";
        foreach (var e in errores)
        {
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CargaId", cargaId);
            cmd.Parameters.AddWithValue("@Fila", (object?)e.NumeroFila ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Col", (object?)e.NombreColumna ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Msg", e.Mensaje);
            cmd.Parameters.AddWithValue("@Tipo", (object?)e.TipoError ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task RegistrarHistorialAsync(
        int cargaId,
        int? usuarioId,
        string accion,
        string? detalle,
        CancellationToken ct)
    {
        const string sql = """
INSERT INTO dbo.HistorialCarga (CargaArchivoId, UsuarioId, Accion, Detalle)
VALUES (@CargaId, @UserId, @Accion, @Detalle);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.AddWithValue("@UserId", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CargaDetalleRow?> GetCargaAsync(int cargaId, CancellationToken ct = default)
    {
        const string sql = """
SELECT c.Id, c.ArchivoId, c.DependenciaId, d.Nombre AS DependenciaNombre,
       c.UsuarioId, u.NombreUsuario, c.Estado, c.Observaciones, c.FechaInicio, c.FechaFin,
       a.NombreOriginal, a.RutaRelativa
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
WHERE c.Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return new CargaDetalleRow(
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3),
            r.GetInt32(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.GetDateTime(8), r.IsDBNull(9) ? null : r.GetDateTime(9),
            r.GetString(10), r.GetString(11));
    }

    public async Task<IReadOnlyList<CargaListaRow>> ListarAsync(
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        var sql = """
SELECT TOP (300) c.Id, c.DependenciaId, d.Nombre, c.Estado, c.FechaInicio, c.FechaFin,
       a.NombreOriginal, u.NombreUsuario,
       (SELECT COUNT(1) FROM dbo.ErroresValidacion e WHERE e.CargaArchivoId = c.Id) AS TotalErrores
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
""";
        if (dependenciaIdFiltro.HasValue)
            sql += " WHERE c.DependenciaId = @DepId";
        sql += " ORDER BY c.FechaInicio DESC;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (dependenciaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CargaListaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new CargaListaRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3),
                r.GetDateTime(4), r.IsDBNull(5) ? null : r.GetDateTime(5),
                r.GetString(6), r.GetString(7), r.GetInt32(8)));
        }
        return list;
    }

    public async Task<IReadOnlyList<ErrorValidacionRow>> ListarErroresAsync(int cargaId, CancellationToken ct = default)
    {
        const string sql = """
SELECT Id, NumeroFila, NombreColumna, Mensaje, TipoError
FROM dbo.ErroresValidacion
WHERE CargaArchivoId = @Id
ORDER BY COALESCE(NumeroFila, 0), Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ErrorValidacionRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new ErrorValidacionRow(
                r.GetInt64(0),
                r.IsDBNull(1) ? null : r.GetInt32(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4)));
        }
        return list;
    }

    public async Task<IReadOnlyList<HistorialRow>> ListarHistorialAsync(
        int? cargaId,
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        var sql = """
SELECT h.Id, h.CargaArchivoId, h.UsuarioId, u.NombreUsuario, h.Accion, h.Detalle, h.Fecha
FROM dbo.HistorialCarga h
LEFT JOIN dbo.Usuarios u ON u.Id = h.UsuarioId
INNER JOIN dbo.CargasArchivo c ON c.Id = h.CargaArchivoId
WHERE 1=1
""";
        if (cargaId.HasValue) sql += " AND h.CargaArchivoId = @CargaId";
        if (dependenciaIdFiltro.HasValue) sql += " AND c.DependenciaId = @DepId";
        sql += " ORDER BY h.Fecha DESC;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (cargaId.HasValue) cmd.Parameters.AddWithValue("@CargaId", cargaId.Value);
        if (dependenciaIdFiltro.HasValue) cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<HistorialRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new HistorialRow(
                r.GetInt64(0), r.GetInt32(1),
                r.IsDBNull(2) ? null : r.GetInt32(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetDateTime(6)));
        }
        return list;
    }

    public async Task LimpiarResultadosValidacionAsync(int cargaId, CancellationToken ct)
    {
        const string sql = """
DELETE FROM dbo.DatosCargados WHERE CargaArchivoId = @Id;
DELETE FROM dbo.ErroresValidacion WHERE CargaArchivoId = @Id;
DELETE FROM cd FROM dbo.CamposDiccionario cd
INNER JOIN dbo.DiccionarioArchivo da ON da.Id = cd.DiccionarioArchivoId
WHERE da.CargaArchivoId = @Id;
DELETE FROM dbo.DiccionarioArchivo WHERE CargaArchivoId = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record CargaDetalleRow(
    int Id, int ArchivoId, int DependenciaId, string DependenciaNombre,
    int UsuarioId, string NombreUsuario, string Estado, string? Observaciones,
    DateTime FechaInicio, DateTime? FechaFin, string NombreOriginal, string RutaRelativa);

public sealed record CargaListaRow(
    int Id, int DependenciaId, string DependenciaNombre, string Estado,
    DateTime FechaInicio, DateTime? FechaFin, string NombreArchivo,
    string Usuario, int TotalErrores);

public sealed record ErrorValidacionRow(
    long Id, int? NumeroFila, string? NombreColumna, string Mensaje, string? TipoError);

public sealed record HistorialRow(
    long Id, int CargaArchivoId, int? UsuarioId, string? NombreUsuario,
    string Accion, string? Detalle, DateTime Fecha);
