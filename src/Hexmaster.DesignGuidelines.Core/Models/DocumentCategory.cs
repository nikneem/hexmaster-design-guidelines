namespace Hexmaster.DesignGuidelines.Core.Models;

/// <summary>
/// Categories of guideline documents.
/// </summary>
public enum DocumentCategory
{
    /// <summary>
    /// Architecture Decision Record.
    /// </summary>
    Adr,

    /// <summary>
    /// Design document providing detailed architectural exploration.
    /// </summary>
    Design,

    /// <summary>
    /// Recommendation document with prescriptive guidance.
    /// </summary>
    Recommendation,

    /// <summary>
    /// Structure document providing project templates and scaffolds.
    /// </summary>
    Structure
}
