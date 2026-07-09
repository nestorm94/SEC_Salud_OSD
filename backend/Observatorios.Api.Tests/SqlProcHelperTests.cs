using Observatorios.Api.Data;

namespace Observatorios.Api.Tests;

/// <summary>
/// Prueba las utilidades de conversión CSV para roles y áreas temáticas en SqlProcHelper.
/// </summary>
public sealed class SqlProcHelperTests
{
    /// <summary>
    /// Verifica que RolesToCsv omita entradas vacías y duplicados (insensible a mayúsculas).
    /// </summary>
    [Fact]
    public void RolesToCsv_omite_vacios_y_duplicados()
    {
        var csv = SqlProcHelper.RolesToCsv(["ADMIN", " ", "admin", "VALIDADOR"]);
        Assert.Equal("ADMIN,VALIDADOR", csv);
    }

    /// <summary>
    /// Verifica que RolesFromCsv parsee correctamente y deduplique al convertir de vuelta.
    /// </summary>
    [Fact]
    public void RolesFromCsv_redondea_ida_y_vuelta()
    {
        var roles = SqlProcHelper.RolesFromCsv("ADMIN, VALIDADOR ,ADMIN");
        Assert.Equal(2, roles.Count);
        Assert.Contains("ADMIN", roles);
        Assert.Contains("VALIDADOR", roles);
    }

    /// <summary>
    /// Verifica que AreaIdsFromCsv parsee enteros e ignore valores no numéricos.
    /// </summary>
    [Fact]
    public void AreaIdsFromCsv_parsea_enteros()
    {
        var ids = SqlProcHelper.AreaIdsFromCsv("1, 2, x, 3");
        Assert.Equal([1, 2, 3], ids);
    }
}
