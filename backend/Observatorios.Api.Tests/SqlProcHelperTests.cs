using Observatorios.Api.Data;

namespace Observatorios.Api.Tests;

public sealed class SqlProcHelperTests
{
    [Fact]
    public void RolesToCsv_omite_vacios_y_duplicados()
    {
        var csv = SqlProcHelper.RolesToCsv(["ADMIN", " ", "admin", "VALIDADOR"]);
        Assert.Equal("ADMIN,VALIDADOR", csv);
    }

    [Fact]
    public void RolesFromCsv_redondea_ida_y_vuelta()
    {
        var roles = SqlProcHelper.RolesFromCsv("ADMIN, VALIDADOR ,ADMIN");
        Assert.Equal(2, roles.Count);
        Assert.Contains("ADMIN", roles);
        Assert.Contains("VALIDADOR", roles);
    }

    [Fact]
    public void AreaIdsFromCsv_parsea_enteros()
    {
        var ids = SqlProcHelper.AreaIdsFromCsv("1, 2, x, 3");
        Assert.Equal([1, 2, 3], ids);
    }
}
