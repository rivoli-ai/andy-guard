namespace Andy.Guard.Tokenizers.CodeBert;

/// <summary>
/// Represents the output of CodeBERT tokenization.
/// Contains input IDs, attention mask, and sequence length information.
/// </summary>
public class CodeBertEncoding
{
    /// <summary>
    /// Token IDs for the input sequence
    /// </summary>
    public int[] InputIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Attention mask (1 for real tokens, 0 for padding)
    /// </summary>
    public int[] AttentionMask { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Length of the actual sequence (excluding padding)
    /// </summary>
    public int SequenceLength { get; set; }
}
