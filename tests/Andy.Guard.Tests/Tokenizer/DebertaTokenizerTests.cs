using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Andy.Guard.Tokenizers.Deberta;
using FluentAssertions;
using Microsoft.ML.Tokenizers;

namespace Andy.Guard.Tests.Tokenizer;

// Parity-focused tests adapted from Hugging Face's DeBERTa v2 tokenizer tests:
// https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
//
// Covered:
// - build_inputs_with_special_tokens (single and pair) + padding/masks
// - truncation behavior (longest_first vs only_first)
// - num_special_tokens_to_add (single=2, pair=3)
// - do_lower_case=True token targets check (token list equality)
//
// Notes:
// - We emulate HF's do_lower_case=True by lowercasing text before running SentencePiece,
//   since the model itself is cased (do_lower_case=False by default in config).
// - Token-to-id mapping for the token-target test is derived from tokenizer.json's Unigram
//   vocab (the array index is the id), ensuring we validate against the exact shipped vocab.
public class DebertaTokenizerTests : IClassFixture<DebertaTokenizerFixture>
{
    private readonly DebertaTokenizerFixture _fx;

    public DebertaTokenizerTests(DebertaTokenizerFixture fx)
    {
        _fx = fx;
    }

    /// Parity: mirrors HF's build_inputs_with_special_tokens and padding tests
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void Single_Encode_StructureAndMask()
    {
        using var tok = _fx.CreateTokenizer(maxLen: 16);

        var enc = tok.Encode("Hello world!");

        enc.InputIds.Length.Should().Be(16);
        enc.AttentionMask.Length.Should().Be(16);

        var realLen = enc.AttentionMask.Sum();
        realLen.Should().BeGreaterThan(2); // [CLS] x [SEP]

        enc.InputIds[0].Should().Be(_fx.CLS);
        enc.InputIds[realLen - 1].Should().Be(_fx.SEP);

        if (realLen < enc.InputIds.Length)
        {
            enc.InputIds[realLen].Should().Be(_fx.PAD);
        }

        enc.AttentionMask.Take(realLen).All(x => x == 1).Should().BeTrue();
        enc.AttentionMask.Skip(realLen).All(x => x == 0).Should().BeTrue();
    }

    /// Parity: mirrors HF's build_inputs_with_special_tokens for pair sequences
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void Pair_Encode_StructureAndMask()
    {
        using var tok = _fx.CreateTokenizer(maxLen: 20);

        var enc = tok.Encode("Hello there", "General Kenobi!");

        enc.InputIds.Length.Should().Be(20);
        enc.AttentionMask.Length.Should().Be(20);

        var realLen = enc.AttentionMask.Sum();
        realLen.Should().BeGreaterThan(4); // [CLS] A [SEP] B [SEP]

        enc.InputIds[0].Should().Be(_fx.CLS);

        // Locate the two [SEP]s within the non-padded region
        var nonPad = enc.InputIds.Take(realLen).ToArray();
        var sepPositions = nonPad
            .Select((v, i) => (v, i))
            .Where(t => t.v == _fx.SEP)
            .Select(t => t.i)
            .ToArray();

        sepPositions.Length.Should().Be(2);
        sepPositions[0].Should().BeGreaterThan(0);
        sepPositions[1].Should().BeGreaterThan(sepPositions[0]);

        // Attention mask correctness
        enc.AttentionMask.Take(realLen).All(x => x == 1).Should().BeTrue();
        enc.AttentionMask.Skip(realLen).All(x => x == 0).Should().BeTrue();
    }

    /// Parity: mirrors HF's truncation_strategy="longest_first" vs "only_first"
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void Pair_Truncation_LongestFirst_vs_OnlyFirst()
    {
        // Make A much longer than B to exercise the strategy difference
        var a = string.Join(' ', Enumerable.Repeat("longsegment", 40));
        var b = "short";

        int maxLen = 32; // small to force truncation

        using var longestFirst = _fx.CreateTokenizer(maxLen: maxLen, trunc: TruncationStrategy.LongestFirst);
        using var onlyFirst = _fx.CreateTokenizer(maxLen: maxLen, trunc: TruncationStrategy.OnlyFirst);

        var encLF = longestFirst.Encode(a, b);
        var encOF = onlyFirst.Encode(a, b);

        // Helper to extract A/B token lengths from encoded sequence
        static (int aLen, int bLen, int sep1, int sep2, int real) ABLens(DebertaEncoding e, int sepId, int padId)
        {
            var real = e.AttentionMask.Sum();
            var nonPad = e.InputIds.Take(real).ToArray();
            var sepIdx = nonPad
                .Select((v, i) => (v, i))
                .Where(t => t.v == sepId)
                .Select(t => t.i)
                .ToArray();

            var sep1 = sepIdx[0];
            var sep2 = sepIdx[1];
            var aLen = sep1 - 1; // minus [CLS]
            var bLen = sep2 - (sep1 + 1);
            return (aLen, bLen, sep1, sep2, real);
        }

        var lf = ABLens(encLF, _fx.SEP, _fx.PAD);
        var of = ABLens(encOF, _fx.SEP, _fx.PAD);

        // Content budget equals maxLen - 3 specials
        (lf.aLen + lf.bLen).Should().Be(maxLen - 3);
        (of.aLen + of.bLen).Should().Be(maxLen - 3);

        // OnlyFirst should preserve B more than (or equal to) LongestFirst when A is longer
        of.bLen.Should().BeGreaterOrEqualTo(lf.bLen);
        of.aLen.Should().BeLessOrEqualTo(lf.aLen);
    }

    /// Parity: mirrors HF's num_special_tokens_to_add for single and pair
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void NumSpecialTokens_SingleAndPair()
    {
        using var tok = _fx.CreateTokenizer(maxLen: 8);

        // Empty single → just [CLS] [SEP]
        var single = tok.Encode("");
        var singleReal = single.AttentionMask.Sum();
        singleReal.Should().Be(2);
        single.InputIds[0].Should().Be(_fx.CLS);
        single.InputIds[1].Should().Be(_fx.SEP);

        // Empty pair → [CLS] [SEP] [SEP]
        var pair = tok.Encode("", "");
        var pairReal = pair.AttentionMask.Sum();
        pairReal.Should().Be(3);
        var nonPad = pair.InputIds.Take(pairReal).ToArray();
        nonPad[0].Should().Be(_fx.CLS);
        nonPad.Count(x => x == _fx.SEP).Should().Be(2);
    }

    /// Parity: mirrors HF's do_lower_case=False behavior (case is preserved)
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void Casing_Is_Preserved()
    {
        using var tok = _fx.CreateTokenizer(maxLen: 16);

        var upper = tok.Encode("Hello World");
        var lower = tok.Encode("hello world");

        // Compare non-padded, non-special content regions
        var upperReal = upper.AttentionMask.Sum();
        var lowerReal = lower.AttentionMask.Sum();
        upperReal.Should().Be(lowerReal);

        // strip [CLS] and final [SEP]
        var uCore = upper.InputIds.Skip(1).Take(upperReal - 2).ToArray();
        var lCore = lower.InputIds.Skip(1).Take(lowerReal - 2).ToArray();

        // With a cased model, these should differ for typical inputs
        uCore.SequenceEqual(lCore).Should().BeFalse();
    }

    /// Parity: basic vocab and config expectations from the reference HF config
    /// Ref: https://huggingface.co/microsoft/deberta-v3-base and
    ///      https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py
    [Fact]
    public void Config_VocabSize_And_SpecialTokenIds_Match_Reference()
    {
        // Read the model config shipped with the repo to assert core expectations
        var root = FindRepoRoot();
        var cfgPath = Path.Combine(root, "src", "Andy.Guard", "Tokenizers", "Deberta", "onnx", "config.json");
        File.Exists(cfgPath).Should().BeTrue(because: "config.json must be present for parity checks");

        using var fs = File.OpenRead(cfgPath);
        using var doc = JsonDocument.Parse(fs);
        var vocabSize = doc.RootElement.GetProperty("vocab_size").GetInt32();
        var padIdCfg = doc.RootElement.GetProperty("pad_token_id").GetInt32();
        var maxPositions = doc.RootElement.GetProperty("max_position_embeddings").GetInt32();

        // Known values from the protected DeBERTa v3-base config
        vocabSize.Should().Be(128100);
        padIdCfg.Should().Be(_fx.PAD);
        maxPositions.Should().Be(512);

        // And our fixture IDs should match the canonical ones used by HF
        _fx.CLS.Should().Be(1);
        _fx.SEP.Should().Be(2);
        _fx.UNK.Should().Be(3);
        _fx.MASK.Should().Be(128000);
    }

    /// Parity: token targets when lowercasing is applied (HF do_lower_case=True)
    /// Ref: https://github.com/huggingface/transformers/blob/v4.56.1/tests/models/deberta_v2/test_tokenization_deberta_v2.py#L56
    [Fact]
    public void DoLowerCase_TokenTargets_Match()
    {
        // fmt: off
        var sequence = " \tHeLLo!how  \n Are yoU?  ";
        var tokensTarget = new[] { "▁hello", "!", "how", "▁are", "▁you", "?" };
        // fmt: on

        var spmPath = Path.Combine(AppContext.BaseDirectory, "onnx", "spm.model");
        File.Exists(spmPath).Should().BeTrue();

        using var stream = File.OpenRead(spmPath);
        var sp = SentencePieceTokenizer.Create(
            stream,
            addBeginOfSentence: false,
            addEndOfSentence: false,
            specialTokens: new Dictionary<string, int>
            {
                ["[PAD]"] = _fx.PAD,
                ["[CLS]"] = _fx.CLS,
                ["[SEP]"] = _fx.SEP,
                ["[MASK]"] = _fx.MASK,
                ["[UNK]"] = _fx.UNK
            });

        // Emulate HF do_lower_case=True by lowercasing before SP piece segmentation
        var lowered = sequence.ToLowerInvariant();
        var ids = sp.EncodeToIds(lowered).ToArray();

        // Convert expected tokens to ids using tokenizer.json's vocab (index is id)
        var vocabMap = LoadTokenizerVocabMap();
        var expectedIds = tokensTarget.Select(t => vocabMap[t]).ToArray();
        ids.Should().Equal(expectedIds);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        DirectoryInfo? di = new DirectoryInfo(dir);
        while (di != null && !File.Exists(Path.Combine(di.FullName, "andy-guard.sln")))
        {
            di = di.Parent;
        }

        return di?.FullName ?? throw new DirectoryNotFoundException("Could not locate solution root");
    }

    private static Dictionary<string, int> LoadTokenizerVocabMap()
    {
        var root = FindRepoRoot();
        var tokPath = Path.Combine(root, "src", "Andy.Guard", "Tokenizers", "Deberta", "onnx", "tokenizer.json");
        using var fs = File.OpenRead(tokPath);
        using var doc = JsonDocument.Parse(fs);
        var vocab = doc.RootElement.GetProperty("model").GetProperty("vocab");
        var map = new Dictionary<string, int>(capacity: 128100);
        int id = 0;
        foreach (var entry in vocab.EnumerateArray())
        {
            var token = entry[0].GetString()!;
            map[token] = id++;
        }

        return map;
    }
}

public sealed class DebertaTokenizerFixture : IDisposable
{
    // Common special token IDs for DeBERTa v2/v3 checkpoints
    public int PAD { get; } = 0;
    public int CLS { get; } = 1;
    public int SEP { get; } = 2;
    public int UNK { get; } = 3;
    public int MASK { get; } = 128000;

    public DebertaTokenizer CreateTokenizer(int maxLen, TruncationStrategy trunc = TruncationStrategy.LongestFirst)
        => DebertaTokenizer.FromFile(
            spModelPath: null, // default to ./onnx/spm.model (copied to output by the main project)
            padId: PAD,
            clsId: CLS,
            sepId: SEP,
            unkId: UNK,
            maskId: MASK,
            maxLen: maxLen,
            truncation: trunc);

    public void Dispose()
    {
        // no shared unmanaged resources
    }
}
