namespace Observatorios.Api.Models;

/// <summary>Estados del flujo de carga por archivo (validar → enviar).</summary>
public static class ArchivoEstados
{
    public const string PendienteValidacion = "PendienteValidacion";
    public const string Validado = "Validado";
    public const string Rechazado = "Rechazado";
    public const string Enviado = "Enviado";

    public static string Etiqueta(string estado) => estado switch
    {
        PendienteValidacion => "Pendiente de validación",
        Validado => "Validado",
        Rechazado => "Rechazado",
        Enviado => "Enviado",
        _ => estado
    };

    public static bool EsValido(string estado) =>
        string.Equals(estado, Validado, StringComparison.OrdinalIgnoreCase);

    public static bool PuedeEnviar(string estado) =>
        string.Equals(estado, Validado, StringComparison.OrdinalIgnoreCase);
}
