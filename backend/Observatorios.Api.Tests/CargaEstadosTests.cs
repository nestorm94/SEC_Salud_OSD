using Observatorios.Api.Models;

namespace Observatorios.Api.Tests;

public sealed class CargaEstadosTests
{
    [Theory]
    [InlineData("subido", CargaEstados.Recibido)]
    [InlineData("VALIDANDO", CargaEstados.EnValidacion)]
    [InlineData("validado_ok", CargaEstados.ValidadoExitoso)]
    [InlineData("APROBADO", CargaEstados.Aprobado)]
    public void Normalizar_mapea_alias_y_mayusculas(string input, string expected) =>
        Assert.Equal(expected, CargaEstados.Normalizar(input));

    [Fact]
    public void Normalizar_vacio_devuelve_recibido() =>
        Assert.Equal(CargaEstados.Recibido, CargaEstados.Normalizar(null));

    [Theory]
    [InlineData(CargaEstados.ValidadoExitoso, true)]
    [InlineData("validado_ok", true)]
    [InlineData(CargaEstados.ValidadoConErrores, false)]
    [InlineData(CargaEstados.Aprobado, false)]
    public void EsPendienteAprobacion_solo_validado_exitoso(string estado, bool esperado) =>
        Assert.Equal(esperado, CargaEstados.EsPendienteAprobacion(estado));
}
