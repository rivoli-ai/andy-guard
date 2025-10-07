using Microsoft.ML.Tokenizers;

namespace Andy.Guard.Tokenizers.CodeBert;

/// <summary>
/// CodeBERT tokenizer for code understanding and security analysis.
/// Uses BERT-style tokenization optimized for programming languages.
/// </summary>
public sealed class CodeBertTokenizer : IDisposable
{
    private readonly BpeTokenizer _tokenizer;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _padId;
    private readonly int _maskId;
    private readonly int _unkId;
    private readonly int _maxLen;

    public CodeBertTokenizer(
        string vocabPath,
        string mergesPath,
        int clsId,
        int sepId,
        int padId,
        int maskId,
        int unkId,
        int maxLen = 512)
    {
        _clsId = clsId;
        _sepId = sepId;
        _padId = padId;
        _maskId = maskId;
        _unkId = unkId;
        _maxLen = maxLen;

        // Initialize BPE tokenizer for CodeBERT
        _tokenizer = BpeTokenizer.Create(vocabPath, mergesPath);
    }

    public static CodeBertTokenizer FromFiles(
        string vocabPath,
        string mergesPath,
        int clsId,
        int sepId,
        int padId,
        int maskId,
        int unkId,
        int maxLen = 512)
    {
        return new CodeBertTokenizer(vocabPath, mergesPath, clsId, sepId, padId, maskId, unkId, maxLen);
    }

    public CodeBertEncoding Encode(string code)
    {
        // Tokenize the code
        var tokens = _tokenizer.EncodeToIds(code);
        
        // Convert to IDs
        var tokenIds = new List<int> { _clsId }; // Start with CLS token
        
        foreach (var tokenId in tokens)
        {
            tokenIds.Add(tokenId);
        }
        
        tokenIds.Add(_sepId); // End with SEP token

        // Truncate if necessary
        if (tokenIds.Count > _maxLen)
        {
            tokenIds = tokenIds.Take(_maxLen - 1).ToList();
            tokenIds.Add(_sepId);
        }

        // Pad to max length
        var inputIds = new int[_maxLen];
        var attentionMask = new int[_maxLen];
        
        for (int i = 0; i < _maxLen; i++)
        {
            if (i < tokenIds.Count)
            {
                inputIds[i] = tokenIds[i];
                attentionMask[i] = 1;
            }
            else
            {
                inputIds[i] = _padId;
                attentionMask[i] = 0;
            }
        }

        return new CodeBertEncoding
        {
            InputIds = inputIds,
            AttentionMask = attentionMask,
            SequenceLength = tokenIds.Count
        };
    }

    public void Dispose()
    {
        // BpeTokenizer doesn't implement IDisposable
        // No cleanup needed
    }
}
