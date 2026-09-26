namespace Prismedia.Domain.Entities;

/// <summary>Closed set of learning-progress states for a saved vocabulary entry.</summary>
public enum VocabularyStatus {
    /// <summary>Just added; not yet reviewed (default).</summary>
    [Code("new")]
    New,

    /// <summary>Actively being learned.</summary>
    [Code("learning")]
    Learning,

    /// <summary>Recognized most of the time.</summary>
    [Code("familiar")]
    Familiar,

    /// <summary>Fully learned.</summary>
    [Code("known")]
    Known
}
