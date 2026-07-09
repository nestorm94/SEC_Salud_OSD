using Observatorios.Api.Models;

namespace Observatorios.Api.Tests;

/// <summary>
/// Prueba la normalización de estados de carga y la lógica de pendiente de aprobación.
/// </summary>
public sealed class CargaEstadosTests
{
    /// <summary>
    /// Verifica que alias y mayúsculas se mapeen al estado canónico.
    /// </summary>
    [Theory]
    [InlineData("subido", CargaEstados.Recibido)]
    [InlineData("VALIDANDO", CargaEstados.EnValidacion)]
    [InlineData("validado_ok", CargaEstados.ValidadoExitoso)]
    [InlineData("APROBADO", CargaEstados.Aprobado)]
    public void Normalizar_mapea_alias_y_mayusculas(string input, string expected) =>
        Assert.Equal(expected, CargaEstados.Normalizar(input));

    /// <summary>
    /// Verifica que valores nulos o vacíos se normalicen a Recibido.
    /// </summary>
    [Fact]
    public void Normalizar_vacio_devuelve_recibido() =>
        Assert.Equal(CargaEstados.Recibido, CargaEstados.Normalizar(null));

    /// <summary>
    /// Verifica que solo el estado validado exitoso se considere pendiente de aprobación.
    /// </summary>
    [Theory]
    [InlineData(CargaEstados.ValidadoExitoso, true)]
    [InlineData("validado_ok", true)]
    [InlineData(CargaEstados.ValidadoConErrores, false)]
    [InlineData(CargaEstados.Aprobado, false)]
    public void EsPendienteAprobacion_solo_validado_exitoso(string estado, bool esperado) =>
        Assert.Equal(esperado, CargaEstados.EsPendienteAprobacion(estado));
}
