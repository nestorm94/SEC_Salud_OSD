using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Services;

namespace Observatorios.Api.Data;

public sealed class CargasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<int> CrearCargaAsync(
        int archivoId, int dependenciaId, int usuarioId, string estadoInicial, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_Crear");
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@DependenciaId", dependenciaId);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@Estado", estadoInicial);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarEstadoAsync(int cargaId, string estado, string? observaciones, CancellationToken ct)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_ActualizarEstado");
        cmd.Parameters.AddWithValue("@Id", cargaId);
        cmd.Parameters.AddWithValue("@Estado", estado);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)observaciones ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GuardarDiccionarioAsync(int cargaId, IReadOnlyList<CampoDiccionarioDto> campos, CancellationToken ct)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_GuardarDiccionario");
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.Add(SqlTvpHelper.TvpParameter("@Campos", "dbo.Tvp_CampoDiccionario", SqlTvpHelper.CampoDiccionario(campos)));
        var outId = new SqlParameter("@DiccionarioId", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(outId);
        await cmd.ExecuteNonQueryAsync(ct);
        return outId.Value is int i ? i : Convert.ToInt32(outId.Value);
    }

    public async Task GuardarDatosAsync(int cargaId, IReadOnlyList<DatosFilaDto> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return;
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_GuardarDatosBulk", 300);
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.Add(SqlTvpHelper.TvpParameter("@Filas", "dbo.Tvp_DatosCargados", SqlTvpHelper.DatosCargados(filas)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task GuardarErroresAsync(int cargaId, IReadOnlyList<ValidationErrorDto> errores, CancellationToken ct)
    {
        if (errores.Count == 0) return;
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_GuardarErroresBulk", 120);
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.Add(SqlTvpHelper.TvpParameter("@Errores", "dbo.Tvp_ErrorValidacion", SqlTvpHelper.ErroresValidacion(errores)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RegistrarHistorialAsync(int cargaId, int? usuarioId, string accion, string? detalle, CancellationToken ct)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_RegistrarHistorial");
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        cmd.Parameters.AddWithValue("@UsuarioId", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CargaDetalleRow?> GetCargaAsync(int cargaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_Obtener");
        cmd.Parameters.AddWithValue("@Id", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await r.ReadAsync(ct)) return null;
        return LeerCargaDetalle(r);
    }

    public async Task<IReadOnlyList<CargaListaRow>> ListarAsync(int? dependenciaIdFiltro, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_Listar");
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(r, ct);
    }

    public async Task<IReadOnlyList<CargaListaRow>> ListarPorUsuarioAsync(
        int usuarioId, int? dependenciaIdFiltro, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_ListarPorUsuario");
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerCargasListaAsync(r, ct);
    }

    public async Task<IReadOnlyList<ErrorValidacionRow>> ListarErroresAsync(int cargaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_Errores_Listar");
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerErroresAsync(r, ct);
    }

    public async Task<IReadOnlyList<HistorialRow>> ListarHistorialAsync(
        int? cargaId, int? dependenciaIdFiltro, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_Historial_Listar");
        cmd.Parameters.AddWithValue("@CargaId", (object?)cargaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)dependenciaIdFiltro ?? DBNull.Value);
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
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Carga_LimpiarResultadosValidacion");
        cmd.Parameters.AddWithValue("@CargaId", cargaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name, int timeout = 30) =>
        new(name, con) { CommandType = CommandType.StoredProcedure, CommandTimeout = timeout };

    private static CargaDetalleRow LeerCargaDetalle(SqlDataReader r) =>
        new(
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3),
            r.GetInt32(4), r.GetString(5), r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.GetDateTime(8), r.IsDBNull(9) ? null : r.GetDateTime(9),
            r.GetString(10), r.GetString(11));

    private static async Task<IReadOnlyList<CargaListaRow>> LeerCargasListaAsync(SqlDataReader r, CancellationToken ct)
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

    private static async Task<IReadOnlyList<ErrorValidacionRow>> LeerErroresAsync(SqlDataReader r, CancellationToken ct)
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
