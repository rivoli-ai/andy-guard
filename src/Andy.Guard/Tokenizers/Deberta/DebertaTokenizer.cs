using Microsoft.ML.Tokenizers;

namespace Andy.Guard.Tokenizers.Deberta;

/// <summary>
/// DeBERTa tokenizer that mirrors Hugging Face preprocessing using SentencePiece.
/// </summary>
/// <remarks>
/// <para>What Hugging Face does:</para>
/// <list type="bullet">
///   <item><description>Uses SentencePiece (Unigram) to map text to IDs.</description></item>
///   <item><description>Prepends/appends special tokens.</description></item>
///   <item><description>Single: <c>[CLS]</c> tokens <c>[SEP]</c></description></item>
///   <item><description>Pair: <c>[CLS]</c> tokens_a <c>[SEP]</c> tokens_b <c>[SEP]</c></description></item>
///   <item><description>Applies truncation (often Longest-First for pairs).</description></item>
///   <item><description>Applies padding to <c>max_length</c>.</description></item>
///   <item><description>Builds <c>attention_mask</c> (1 → real token, 0 → padding).</description></item>
///   <item><description>For DeBERTa v3, no <c>token_type_ids</c> (segment IDs).</description></item>
/// </list>
///
/// <para>What this C# class does:</para>
/// <list type="bullet">
///   <item><description>Loads the same SentencePiece model and disables BOS/EOS (we add <c>[CLS]</c>/<c>[SEP]</c> ourselves).</description></item>
///   <item><description>Encodes via <c>_sp.EncodeToIds(text)</c> (matches the HF SentencePiece step).</description></item>
///   <item><description>Adds special tokens exactly like HF.</description></item>
///   <item><description>Implements Longest-First truncation for pairs and head-only truncation for singles.</description></item>
///   <item><description>Pads to <c>_maxLen</c> and creates the attention mask.</description></item>
/// </list>
///
/// <para>Note: Use the exact special-token IDs for your checkpoint (e.g., <c>microsoft/deberta-v3-base</c>). These IDs are not stored in the <c>spm.model</c>.</para>
/// <para>The special-token IDs must match the model’s embedding matrix indices. Retrieve them once using Hugging Face transformers:</para>
/// <code>
/// from transformers import AutoTokenizer
/// t = AutoTokenizer.from_pretrained("microsoft/deberta-v3-base")
/// print(t.cls_token_id, t.sep_token_id, t.pad_token_id, t.mask_token_id, t.unk_token_id)
/// </code>
/// </remarks>
/// <seealso cref="DebertaEncoding"/>
/// <seealso cref="TruncationStrategy"/>
public sealed class DebertaTokenizer : IDisposable
{
    private readonly SentencePieceTokenizer _sp;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _padId;
    private readonly int _maskId;
    private readonly int _unkId;

    private readonly int _maxLen;
    private readonly TruncationStrategy _truncStrategy;

    private DebertaTokenizer(
        SentencePieceTokenizer sp,
        int clsId, int sepId, int padId, int maskId, int unkId,
        int maxLen,
        TruncationStrategy truncation)
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        _clsId = clsId;
        _sepId = sepId;
        _padId = padId;
        _maskId = maskId;
        _unkId = unkId;
        _maxLen = maxLen;
        _truncStrategy = truncation;
    }

    /// <summary>
    /// Creates a tokenizer from a SentencePiece <c>spm.model</c> file path.
    /// </summary>
    /// <param name="spModelPath">Path to the SentencePiece <c>spm.model</c> file.</param>
    /// <param name="clsId">ID of <c>[CLS]</c>.</param>
    /// <param name="sepId">ID of <c>[SEP]</c>.</param>
    /// <param name="padId">ID of <c>[PAD]</c>.</param>
    /// <param name="maskId">ID of <c>[MASK]</c>.</param>
    /// <param name="unkId">ID of <c>[UNK]</c>.</param>
    /// <param name="maxLen">Maximum sequence length (includes specials and padding).</param>
    /// <param name="truncation">Truncation strategy for pairs and singles.</param>
    /// <returns>Configured <see cref="DebertaTokenizer"/> instance.</returns>
    /// <exception cref="FileNotFoundException">Thrown if <paramref name="spModelPath"/> does not exist.</exception>
    /// <remarks>
    /// Disables BOS/EOS in SentencePiece and adds <c>[CLS]</c>/<c>[SEP]</c> manually to match Hugging Face behavior.
    /// Special-token IDs are not stored inside the SentencePiece model; they must match the original checkpoint.
    /// </remarks>
    public static DebertaTokenizer FromFile(
        string spModelPath,
        int clsId, int sepId, int padId, int maskId, int unkId,
        int maxLen = 512,
        TruncationStrategy truncation = TruncationStrategy.LongestFirst)
    {
        if (!File.Exists(spModelPath))
            throw new FileNotFoundException($"SentencePiece model not found: {spModelPath}");

        using var fs = File.OpenRead(spModelPath);
        return FromStream(fs, clsId, sepId, padId, maskId, unkId, maxLen, truncation);
    }

    /// <summary>
    /// Creates a tokenizer from a SentencePiece <c>spm.model</c> stream.
    /// </summary>
    /// <param name="spModelStream">Open stream providing the <c>spm.model</c> contents.</param>
    /// <param name="clsId">ID of <c>[CLS]</c>.</param>
    /// <param name="sepId">ID of <c>[SEP]</c>.</param>
    /// <param name="padId">ID of <c>[PAD]</c>.</param>
    /// <param name="maskId">ID of <c>[MASK]</c>.</param>
    /// <param name="unkId">ID of <c>[UNK]</c>.</param>
    /// <param name="maxLen">Maximum sequence length (includes specials and padding).</param>
    /// <param name="truncation">Truncation strategy for pairs and singles.</param>
    /// <returns>Configured <see cref="DebertaTokenizer"/> instance.</returns>
    /// <remarks>
    /// Disables BOS/EOS in SentencePiece and registers special tokens so that <c>_sp.EncodeToIds</c> matches Hugging Face preprocessing.
    /// </remarks>
    public static DebertaTokenizer FromStream(
        Stream spModelStream,
        int clsId, int sepId, int padId, int maskId, int unkId,
        int maxLen = 512,
        TruncationStrategy truncation = TruncationStrategy.LongestFirst)
    {
        // Key point: disable BOS/EOS here; we add [CLS]/[SEP] ourselves to match HF behavior
        var sp = SentencePieceTokenizer.Create(
            spModelStream,
            addBeginOfSentence: false,
            addEndOfSentence: false,
            specialTokens: new Dictionary<string, int>
            {
                ["[PAD]"] = padId,
                ["[CLS]"] = clsId,
                ["[SEP]"] = sepId,
                ["[MASK]"] = maskId,
                ["[UNK]"] = unkId
            });

        return new DebertaTokenizer(sp, clsId, sepId, padId, maskId, unkId, maxLen, truncation);
    }

    /// <summary>
    /// Encodes a single sequence and returns input IDs and attention mask.
    /// </summary>
    /// <param name="text">Raw input text.</param>
    /// <returns>
    /// <see cref="DebertaEncoding"/> where <c>InputIds</c> is <c>[CLS]</c> tokens <c>[SEP]</c>,
    /// padded/truncated to the configured max length, and <c>AttentionMask</c> marks tokens as 1 and padding as 0.
    /// </returns>
    /// <remarks>
    /// Uses SentencePiece (Unigram) via <c>_sp.EncodeToIds</c>, adds specials, applies head-only truncation for singles,
    /// and pads to <c>maxLen</c>. For DeBERTa v3, no <c>token_type_ids</c> are produced.
    /// </remarks>
    public DebertaEncoding Encode(string text)
    {
        if (text == null) text = string.Empty;

        // 1) SentencePiece subword IDs for the raw text (no lowercasing; DeBERTa v3 is cased)
        var ids = _sp.EncodeToIds(text).ToList();

        // 2) Add specials: [CLS] + tokens + [SEP]
        var withSpecials = new List<int>(ids.Count + 2) { _clsId };
        withSpecials.AddRange(ids);
        withSpecials.Add(_sepId);

        // 3) Truncate (if needed)
        var truncated = TruncateSingle(withSpecials);

        // 4) Pad and attention mask
        return PadAndMask(truncated);
    }

    /// <summary>
    /// Encodes a pair of sequences with Hugging Face formatting: <c>[CLS]</c> A <c>[SEP]</c> B <c>[SEP]</c>.
    /// </summary>
    /// <param name="textA">Left (first) sequence.</param>
    /// <param name="textB">Right (second) sequence.</param>
    /// <returns>
    /// <see cref="DebertaEncoding"/> with <c>InputIds</c> formed as <c>[CLS]</c> A <c>[SEP]</c> B <c>[SEP]</c>,
    /// padded/truncated to the configured max length, and <c>AttentionMask</c> marking real tokens as 1 and padding as 0.
    /// </returns>
    /// <remarks>
    /// Applies the configured truncation strategy (default: Longest-First) to A/B before adding specials, then pads.
    /// For DeBERTa v3, <c>token_type_ids</c> are not used.
    /// </remarks>
    public DebertaEncoding Encode(string textA, string textB)
    {
        if (textA == null) textA = string.Empty;
        if (textB == null) textB = string.Empty;

        var a = _sp.EncodeToIds(textA).ToList();
        var b = _sp.EncodeToIds(textB).ToList();

        // Specials: [CLS] A [SEP] B [SEP]
        var composed = new List<int>(a.Count + b.Count + 3) { _clsId };
        composed.AddRange(a);
        composed.Add(_sepId);
        composed.AddRange(b);
        composed.Add(_sepId);

        // Truncate with strategy
        var truncated = TruncatePair(a, b);

        // Rebuild with specials after truncation
        var final = new List<int>(truncated.Item1.Count + truncated.Item2.Count + 3) { _clsId };
        final.AddRange(truncated.Item1);
        final.Add(_sepId);
        final.AddRange(truncated.Item2);
        final.Add(_sepId);

        return PadAndMask(final);
    }

    private List<int> TruncateSingle(List<int> tokens)
    {
        if (tokens.Count <= _maxLen) return tokens;
        // Keep the first _maxLen tokens (HF default behavior for single sequence is usually simple head truncation)
        return tokens.Take(_maxLen).ToList();
    }

    private (List<int>, List<int>) TruncatePair(List<int> a, List<int> b)
    {
        // We must account for 3 specials: [CLS], [SEP], [SEP]
        // Total length limit: len([CLS]) + len(a) + len([SEP]) + len(b) + len([SEP]) <= _maxLen
        int reserved = 3;
        int maxContent = _maxLen - reserved;
        if (maxContent < 0)
            throw new InvalidOperationException("maxLen too small to fit special tokens.");

        // If already fits, return as-is
        if (a.Count + b.Count <= maxContent)
            return (a, b);

        // Truncate according to strategy
        var aa = a.ToList();
        var bb = b.ToList();
        switch (_truncStrategy)
        {
            case TruncationStrategy.LongestFirst:
                while (aa.Count + bb.Count > maxContent)
                {
                    if (aa.Count >= bb.Count) aa.RemoveAt(aa.Count - 1);
                    else bb.RemoveAt(bb.Count - 1);
                }
                break;

            case TruncationStrategy.OnlyFirst:
                while (aa.Count + bb.Count > maxContent && aa.Count > 0)
                    aa.RemoveAt(aa.Count - 1);
                // if still exceeds, trim b as well (safety)
                while (aa.Count + bb.Count > maxContent && bb.Count > 0)
                    bb.RemoveAt(bb.Count - 1);
                break;

            default:
                throw new NotSupportedException($"Unknown truncation strategy: {_truncStrategy}");
        }

        return (aa, bb);
    }

    private DebertaEncoding PadAndMask(List<int> tokens)
    {
        var inputIds = tokens.ToList();
        var attention = Enumerable.Repeat(1, inputIds.Count).ToList();

        if (inputIds.Count > _maxLen)
        {
            inputIds = inputIds.Take(_maxLen).ToList();
            attention = attention.Take(_maxLen).ToList();
        }

        while (inputIds.Count < _maxLen)
        {
            inputIds.Add(_padId);
            attention.Add(0);
        }

        return new DebertaEncoding
        {
            InputIds = inputIds.ToArray(),
            AttentionMask = attention.ToArray()
        };
    }

    /// <summary>
    /// Disposes internal resources (no-op).
    /// </summary>
    public void Dispose()
    {
        // _sp currently doesn't require disposal; keep for future-proofing.
    }
}
