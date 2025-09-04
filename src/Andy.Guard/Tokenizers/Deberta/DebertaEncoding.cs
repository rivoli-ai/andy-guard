namespace Andy.Guard.Tokenizers.Deberta;

/// <summary>
/// Encoded outputs for DeBERTa-style tokenization (IDs and attention mask).
/// </summary>
/// <remarks>
/// <para>InputIds include special tokens and padding:</para>
/// <list type="bullet">
///   <item><description>Single: <c>[CLS]</c> tokens <c>[SEP]</c></description></item>
///   <item><description>Pair: <c>[CLS]</c> A <c>[SEP]</c> B <c>[SEP]</c></description></item>
///   <item><description>Padded/truncated to the configured maximum length.</description></item>
/// </list>
/// <para><c>AttentionMask</c> marks real tokens as 1 and padding as 0. For DeBERTa v3, <c>token_type_ids</c> are not used.</para>
/// </remarks>
/// <seealso cref="DebertaTokenizer"/>
public sealed class DebertaEncoding
{
    /// <summary>
    /// Token IDs including <c>[CLS]</c>/<c>[SEP]</c> and any padding; fixed length.
    /// </summary>
    public int[] InputIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Attention mask aligned with <see cref="InputIds"/> (1 for tokens, 0 for padding).
    /// </summary>
    public int[] AttentionMask { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Total sequence length; equals <c>InputIds.Length</c>.
    /// </summary>
    public int SequenceLength => InputIds.Length;
}
