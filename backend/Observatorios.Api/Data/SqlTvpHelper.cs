using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Services;

namespace Observatorios.Api.Data;

/// <summary>Tablas valor para TVP de SQL Server (fase 5).</summary>
internal static class SqlTvpHelper
{
    public static DataTable CampoDiccionario(IReadOnlyList<CampoDiccionarioDto> campos)
    {
        var dt = new DataTable();
        dt.Columns.Add("NombreCampo", typeof(string));
        dt.Columns.Add("TipoDato", typeof(string));
        dt.Columns.Add("Obligatorio", typeof(bool));
        dt.Columns.Add("Descripcion", typeof(string));
        dt.Columns.Add("Longitud", typeof(int));
        dt.Columns.Add("Formato", typeof(string));
        dt.Columns.Add("ValoresPermitidos", typeof(string));
        dt.Columns.Add("Orden", typeof(int));

        foreach (var c in campos)
        {
            dt.Rows.Add(
                c.NombreCampo,
                c.TipoDato,
                c.Obligatorio,
                (object?)c.Descripcion ?? DBNull.Value,
                c.Longitud.HasValue ? c.Longitud.Value : DBNull.Value,
                (object?)c.Formato ?? DBNull.Value,
                (object?)c.ValoresPermitidos ?? DBNull.Value,
                c.Orden);
        }
        return dt;
    }

    public static DataTable DatosCargados(IReadOnlyList<DatosFilaDto> filas)
    {
        var dt = new DataTable();
        dt.Columns.Add("NumeroFila", typeof(int));
        dt.Columns.Add("DatosJson", typeof(string));
        foreach (var f in filas)
            dt.Rows.Add(f.NumeroFila, f.DatosJson);
        return dt;
    }

    public static DataTable ErroresValidacion(IReadOnlyList<ValidationErrorDto> errores)
    {
        var dt = new DataTable();
        dt.Columns.Add("NumeroFila", typeof(int));
        dt.Columns.Add("NombreColumna", typeof(string));
        dt.Columns.Add("Mensaje", typeof(string));
        dt.Columns.Add("TipoError", typeof(string));
        foreach (var e in errores)
        {
            dt.Rows.Add(
                e.NumeroFila.HasValue ? e.NumeroFila.Value : DBNull.Value,
                (object?)e.NombreColumna ?? DBNull.Value,
                e.Mensaje,
                (object?)e.TipoError ?? DBNull.Value);
        }
        return dt;
    }

    public static SqlParameter TvpParameter(string name, string typeName, DataTable table) =>
        new(name, SqlDbType.Structured)
        {
            TypeName = typeName,
            Value = table
        };
}
