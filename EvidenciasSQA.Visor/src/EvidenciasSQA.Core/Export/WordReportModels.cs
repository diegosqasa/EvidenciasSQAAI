namespace EvidenciasSQA.Core.Export;

/// <summary>Datos de la Historia de Usuario que encabezan el documento.</summary>
public sealed record WordHuInfo(string Id, string Nombre, string Usuario);

/// <summary>Una evidencia dentro del documento Word.</summary>
public sealed record WordEvidenceItem(
    string FilePath,
    string EvidenceCode,
    string FormattedDate,
    string OriginSite,
    string Title);

/// <summary>Un caso de prueba / módulo del informe (exportación por módulos).</summary>
public sealed record WordModule(string Name, IReadOnlyList<WordEvidenceItem> Items);
