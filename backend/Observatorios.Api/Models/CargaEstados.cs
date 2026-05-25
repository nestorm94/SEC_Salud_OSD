namespace Observatorios.Api.Models;

public static class CargaEstados
{
    public const string Recibido = "RECIBIDO";
    public const string EnValidacion = "EN_VALIDACION";
    public const string ValidadoConErrores = "VALIDADO_CON_ERRORES";
    public const string ValidadoExitoso = "VALIDADO_EXITOSO";
    public const string Aprobado = "APROBADO";
    public const string Rechazado = "RECHAZADO";
    public const string CargadoBd = "CARGADO_BD";

    // Alias compatibilidad v1
    public const string Subido = "SUBIDO";
    public const string Validando = "VALIDANDO";
    public const string ValidadoOk = "VALIDADO_OK";

    public static string Normalizar(string? estado) => estado switch
    {
        "SUBIDO" => Recibido,
        "VALIDANDO" => EnValidacion,
        "VALIDADO_OK" => ValidadoExitoso,
        _ => estado ?? Recibido
    };
}
