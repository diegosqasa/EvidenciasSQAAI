namespace EvidenciasSQA.Core.Interfaces;

/// <summary>
/// Resultado de una exportación a un destino (análogo a EvidenciasSQA.Base.Interfaces.ExportInformation).
/// Transporta la información necesaria para notificar al usuario si la exportación fue exitosa,
/// dónde quedó el archivo, el URI generado o el error ocurrido.
/// </summary>
public sealed class ExportInformation
{
    public ExportInformation(string destinationDesignation, string destinationDescription)
    {
        DestinationDesignation = destinationDesignation;
        DestinationDescription = destinationDescription;
    }

    /// <summary>Identificador estable del destino ("File", "Clipboard", ...).</summary>
    public string DestinationDesignation { get; }

    /// <summary>Descripción legible para la UI.</summary>
    public string DestinationDescription { get; set; }

    /// <summary>true si el destino realmente exportó la captura.</summary>
    public bool ExportMade { get; set; }

    /// <summary>Ruta completa del archivo generado (si aplica).</summary>
    public string? Filepath { get; set; }

    /// <summary>URI de destino remoto (si aplica, p. ej. Imgur en EvidenciasSQA).</summary>
    public string? Uri { get; set; }

    /// <summary>Mensaje de error si la exportación falló.</summary>
    public string? ErrorMessage { get; set; }
}
