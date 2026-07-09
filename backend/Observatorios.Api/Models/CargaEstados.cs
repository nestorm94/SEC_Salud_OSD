namespace Observatorios.Api.Models;

/// <summary>
/// Estados del ciclo de vida de una carga Excel en el OSD: recepción, validación,
/// aprobación y persistencia en base de datos.
/// </summary>
public static class CargaEstados
{
    /// <summary>Archivo recibido y registrado, pendiente de validación.</summary>
    public const string Recibido = "RECIBIDO";
    /// <summary>Validación en curso sobre el contenido del Excel.</summary>
    public const string EnValidacion = "EN_VALIDACION";
    /// <summary>Validación finalizada con errores de negocio o formato.</summary>
    public const string ValidadoConErrores = "VALIDADO_CON_ERRORES";
    /// <summary>Validación exitosa; puede pasar a aprobación del validador.</summary>
    public const string ValidadoExitoso = "VALIDADO_EXITOSO";
    /// <summary>Aprobado por validador o administrador.</summary>
    public const string Aprobado = "APROBADO";
    /// <summary>Rechazado en el flujo de aprobación.</summary>
    public const string Rechazado = "RECHAZADO";
    /// <summary>Datos persistidos en tablas operativas del observatorio.</summary>
    public const string CargadoBd = "CARGADO_BD";

    // Alias compatibilidad v1
    /// <summary>Alias legacy de <see cref="Recibido"/>.</summary>
    public const string Subido = "SUBIDO";
    /// <summary>Alias legacy de <see cref="EnValidacion"/>.</summary>
    public const string Validando = "VALIDANDO";
    /// <summary>Alias legacy de <see cref="ValidadoExitoso"/>.</summary>
    public const string ValidadoOk = "VALIDADO_OK";

    /// <summary>
    /// Unifica estados legacy y actuales a la nomenclatura v2.
    /// </summary>
    /// <param name="estado">Estado almacenado en BD o recibido del cliente.</param>
    /// <returns>Constante normalizada; <see cref="Recibido"/> si es nulo o vacío.</returns>
    public static string Normalizar(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return Recibido;
        var e = estado.Trim().ToUpperInvariant();
        return e switch
        {
            "SUBIDO" => Recibido,
            "VALIDANDO" => EnValidacion,
            "VALIDADO_OK" => ValidadoExitoso,
            _ => e
        };
    }

    /// <summary>Indica si la carga está lista para revisión y aprobación por un validador.</summary>
    public static bool EsPendienteAprobacion(string? estado) =>
        Normalizar(estado) == ValidadoExitoso;
}
