namespace Andy.Guard.Tokenizers.Deberta;

/// <summary>
/// Truncation strategy used when encoded sequences exceed the maximum length.
/// </summary>
public enum TruncationStrategy
{
        /// <summary>
        /// For pairs, iteratively remove tokens from the longer sequence until it fits (Hugging Face default).
        /// For singles, behaves like head truncation at the configured max length.
        /// </summary>
        LongestFirst,

        /// <summary>
        /// Truncate only the first (left) sequence. If still over the limit, the second may be trimmed as a safety fallback.
        /// </summary>
        OnlyFirst
}
