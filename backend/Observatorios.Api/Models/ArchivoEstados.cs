namespace Observatorios.Api.Models;

/// <summary>
/// Estados del flujo de un archivo Excel OSC: prevalidación, envío y creación de carga.
/// </summary>
public static class ArchivoEstados
{
    /// <summary>Subido y pendiente de validación contra plantilla OSC.</summary>
    public const string PendienteValidacion = "PendienteValidacion";
    /// <summary>Pasó la validación y puede enviarse al flujo de cargas.</summary>
    public const string Validado = "Validado";
    /// <summary>Rechazado en validación por errores de plantilla o datos.</summary>
    public const string Rechazado = "Rechazado";
    /// <summary>Enviado al módulo de cargas (generó o vinculó una carga).</summary>
    public const string Enviado = "Enviado";

    /// <summary>Etiqueta legible en español para mostrar en la UI.</summary>
    public static string Etiqueta(string estado) => estado switch
    {
        PendienteValidacion => "Pendiente de validación",
        Validado => "Validado",
        Rechazado => "Rechazado",
        Enviado => "Enviado",
        _ => estado
    };

    /// <summary>Indica si el archivo superó la validación de plantilla.</summary>
    public static bool EsValido(string estado) =>
        string.Equals(estado, Validado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Indica si el archivo puede enviarse al flujo de cargas.</summary>
    public static bool PuedeEnviar(string estado) =>
        string.Equals(estado, Validado, StringComparison.OrdinalIgnoreCase);
}
