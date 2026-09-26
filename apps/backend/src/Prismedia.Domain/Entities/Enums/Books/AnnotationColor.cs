namespace Prismedia.Domain.Entities;

/// <summary>Closed palette of reader highlight colours.</summary>
public enum AnnotationColor {
    /// <summary>Yellow highlight (default).</summary>
    [Code("yellow")]
    Yellow,

    /// <summary>Blue highlight.</summary>
    [Code("blue")]
    Blue,

    /// <summary>Green highlight.</summary>
    [Code("green")]
    Green,

    /// <summary>Red highlight.</summary>
    [Code("red")]
    Red,

    /// <summary>Purple highlight.</summary>
    [Code("purple")]
    Purple
}
