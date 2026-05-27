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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_Crear", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_Crear", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@ArchivoId", archivoId);
            cmdSp.Parameters.AddWithValue("@DependenciaId", dependenciaId);
            cmdSp.Parameters.AddWithValue("@UsuarioId", usuarioId);
            cmdSp.Parameters.AddWithValue("@Estado", estadoInicial);
            return Convert.ToInt32(await cmdSp.ExecuteScalarAsync(ct));
        }

        const string sql = """
INSERT INTO dbo.CargasArchivo (ArchivoId, DependenciaId, UsuarioId, Estado)
OUTPUT INSERTED.Id
VALUES (@ArchivoId, @DepId, @UserId, @Estado);
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@DepId", dependenciaId);
        cmd.Parameters.AddWithValue("@UserId", usuarioId);
        cmd.Parameters.AddWithValue("@Estado", estadoInicial);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarEstadoAsync(int cargaId, string estado, string? observaciones, CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_ActualizarEstado", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_ActualizarEstado", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@Id", cargaId);
            cmdSp.Parameters.AddWithValue("@Estado", estado);
            cmdSp.Parameters.AddWithValue("@Observaciones", (object?)observaciones ?? DBNull.Value);
            await cmdSp.ExecuteNonQueryAsync(ct);
            return;
        }

        const string sql = """
UPDATE dbo.CargasArchivo
SET Estado = @Estado,
    Observaciones = COALESCE(@Obs, Observaciones),
    FechaFin = CASE WHEN @Estado IN (N'VALIDADO_OK', N'VALIDADO_EXITOSO', N'VALIDADO_CON_ERRORES', N'APROBADO', N'RECHAZADO', N'CARGADO_BD')
        THEN SYSUTCDATETIME() ELSE FechaFin END
WHERE Id = @Id;
""";
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

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_GuardarDiccionario", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_GuardarDiccionario", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            cmdSp.Parameters.Add(SqlTvpHelper.TvpParameter("@Campos", "dbo.Tvp_CampoDiccionario", SqlTvpHelper.CampoDiccionario(campos)));
            var outId = new SqlParameter("@DiccionarioId", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmdSp.Parameters.Add(outId);
            await cmdSp.ExecuteNonQueryAsync(ct);
            return outId.Value is int i ? i : Convert.ToInt32(outId.Value);
        }

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

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_GuardarDatosBulk", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_GuardarDatosBulk", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 300
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            cmdSp.Parameters.Add(SqlTvpHelper.TvpParameter("@Filas", "dbo.Tvp_DatosCargados", SqlTvpHelper.DatosCargados(filas)));
            await cmdSp.ExecuteNonQueryAsync(ct);
            return;
        }

        const string sql = """
INSERT INTO dbo.DatosCargados (CargaArchivoId, NumeroFila, DatosJson)
VALUES (@CargaId, @Fila, @Json);
""";
        foreach (var f in filas)
        {
            await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 300 };
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

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_GuardarErroresBulk", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_GuardarErroresBulk", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 120
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            cmdSp.Parameters.Add(SqlTvpHelper.TvpParameter("@Errores", "dbo.Tvp_ErrorValidacion", SqlTvpHelper.ErroresValidacion(errores)));
            await cmdSp.ExecuteNonQueryAsync(ct);
            return;
        }

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
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_RegistrarHistorial", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_RegistrarHistorial", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            cmdSp.Parameters.AddWithValue("@UsuarioId", (object?)usuarioId ?? DBNull.Value);
            cmdSp.Parameters.AddWithValue("@Accion", accion);
            cmdSp.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
            await cmdSp.ExecuteNonQueryAsync(ct);
            return;
        }

        const string sql = """
INSERT INTO dbo.HistorialCarga (CargaArchivoId, UsuarioId, Accion, Detalle)
VALUES (@CargaId, @UserId, @Accion, @Detalle);
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.AddWithValue("@UserId", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CargaDetalleRow?> GetCargaAsync(int cargaId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_Obtener", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_Obtener", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@Id", cargaId);
            await using var rSp = await cmdSp.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await rSp.ReadAsync(ct)) return null;
            return LeerCargaDetalle(rSp);
        }

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
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return LeerCargaDetalle(r);
    }

    public async Task<IReadOnlyList<CargaListaRow>> ListarAsync(
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_Listar", ct))
            return await ListarDesdeSpAsync(con, dependenciaIdFiltro, ct);

        var sql = """
SELECT c.Id, c.DependenciaId, d.Nombre, c.Estado, c.FechaInicio, c.FechaFin,
       a.NombreOriginal, u.NombreUsuario,
       (SELECT COUNT(1) FROM dbo.ErroresValidacion e WHERE e.CargaArchivoId = c.Id) AS TotalErrores
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
""";
        if (dependenciaIdFiltro.HasValue)
            sql += " WHERE c.DependenciaId = @DepId";
        sql += " ORDER BY COALESCE(c.FechaFin, c.FechaInicio) DESC, c.Id DESC;";

        await using var cmd = new SqlCommand(sql, con);
        if (dependenciaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(reader, ct);
    }

    public async Task<IReadOnlyList<CargaListaRow>> ListarPorUsuarioAsync(
        int usuarioId,
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_ListarPorUsuario", ct))
            return await ListarPorUsuarioDesdeSpAsync(con, usuarioId, dependenciaIdFiltro, ct);

        var sql = """
SELECT c.Id, c.DependenciaId, d.Nombre, c.Estado, c.FechaInicio, c.FechaFin,
       a.NombreOriginal, u.NombreUsuario,
       (SELECT COUNT(1) FROM dbo.ErroresValidacion e WHERE e.CargaArchivoId = c.Id) AS TotalErrores
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
WHERE c.UsuarioId = @UserId
""";
        if (dependenciaIdFiltro.HasValue)
            sql += " AND c.DependenciaId = @DepId";
        sql += " ORDER BY COALESCE(c.FechaFin, c.FechaInicio) DESC, c.Id DESC;";

        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", usuarioId);
        if (dependenciaIdFiltro.HasValue)
            cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(reader, ct);
    }

    public async Task<IReadOnlyList<ErrorValidacionRow>> ListarErroresAsync(int cargaId, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_Errores_Listar", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_Errores_Listar", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            await using var rSp = await cmdSp.ExecuteReaderAsync(ct);
            return await LeerErroresAsync(rSp, ct);
        }

        const string sql = """
SELECT Id, NumeroFila, NombreColumna, Mensaje, TipoError
FROM dbo.ErroresValidacion
WHERE CargaArchivoId = @Id
ORDER BY COALESCE(NumeroFila, 0), Id;
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerErroresAsync(r, ct);
    }

    public async Task<IReadOnlyList<HistorialRow>> ListarHistorialAsync(
        int? cargaId,
        int? dependenciaIdFiltro,
        CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_Historial_Listar", ct))
            return await ListarHistorialDesdeSpAsync(con, cargaId, dependenciaIdFiltro, ct);

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

        await using var cmd = new SqlCommand(sql, con);
        if (cargaId.HasValue) cmd.Parameters.AddWithValue("@CargaId", cargaId.Value);
        if (dependenciaIdFiltro.HasValue) cmd.Parameters.AddWithValue("@DepId", dependenciaIdFiltro.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<HistorialRow>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HistorialRow(
                reader.GetInt64(0), reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDateTime(6)));
        }
        return list;
    }

    public async Task LimpiarResultadosValidacionAsync(int cargaId, CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Carga_LimpiarResultadosValidacion", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Carga_LimpiarResultadosValidacion", con)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdSp.Parameters.AddWithValue("@CargaId", cargaId);
            await cmdSp.ExecuteNonQueryAsync(ct);
            return;
        }

        const string sql = """
DELETE FROM dbo.DatosCargados WHERE CargaArchivoId = @Id;
DELETE FROM dbo.ErroresValidacion WHERE CargaArchivoId = @Id;
DELETE FROM cd FROM dbo.CamposDiccionario cd
INNER JOIN dbo.DiccionarioArchivo da ON da.Id = cd.DiccionarioArchivoId
WHERE da.CargaArchivoId = @Id;
DELETE FROM dbo.DiccionarioArchivo WHERE CargaArchivoId = @Id;
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static CargaDetalleRow LeerCargaDetalle(SqlDataReader r) =>
        new(
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3),
            r.GetInt32(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.GetDateTime(8), r.IsDBNull(9) ? null : r.GetDateTime(9),
            r.GetString(10), r.GetString(11));

    private static async Task<IReadOnlyList<CargaListaRow>> ListarDesdeSpAsync(
        SqlConnection con,
        int? dependenciaIdFiltro,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("dbo.usp_Carga_Listar", con)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(reader, ct);
    }

    private static async Task<IReadOnlyList<CargaListaRow>> ListarPorUsuarioDesdeSpAsync(
        SqlConnection con,
        int usuarioId,
        int? dependenciaIdFiltro,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("dbo.usp_Carga_ListarPorUsuario", con)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(reader, ct);
    }

    private static async Task<IReadOnlyList<HistorialRow>> ListarHistorialDesdeSpAsync(
        SqlConnection con,
        int? cargaId,
        int? dependenciaIdFiltro,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("dbo.usp_Carga_Historial_Listar", con)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@CargaId", (object?)cargaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<HistorialRow>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HistorialRow(
                reader.GetInt64(0), reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDateTime(6)));
        }
        return list;
    }

    private static async Task<IReadOnlyList<CargaListaRow>> LeerCargasListaAsync(
        SqlDataReader r,
        CancellationToken ct)
    {
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

    private static async Task<IReadOnlyList<ErrorValidacionRow>> LeerErroresAsync(
        SqlDataReader r,
        CancellationToken ct)
    {
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
