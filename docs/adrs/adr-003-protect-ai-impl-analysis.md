# ProtectAI's LLM Guard Implementation Analysis

After examining the LLM Guard repository, here's how their PromptInjection scanner actually works:
The scanner's architecture consists of three main components working together.

## Scanner Architecture

### Model Loading

They use the Hugging Face transformers library to load a fine-tuned DeBERTa-v3-base model specifically trained for prompt injection detection. The model is hosted at protectai/deberta-v3-base-prompt-injection-v2.

### Preprocessing Pipeline

```
# Simplified version of their approach
1. Text truncation to 512 tokens maximum
2. Tokenization using AutoTokenizer
3. Conversion to model input tensors
4. Padding/truncation to fixed length
```

### Inference and Scoring

The scanner performs binary classification where:

Class 0: Normal text (safe)
Class 1: Prompt injection detected (malicious)

They apply a softmax function to the model outputs and use the probability of class 1 as the injection score. If this score exceeds the configured threshold (default 0.5), the text is flagged as containing a prompt injection.

### Conclusion

The key insight is that the scanner's main role is indeed preprocessing - it handles text normalization, tokenization, and tensor preparation so the model receives properly formatted input. The actual detection logic is embedded in the model's weights from their specialized training process.

## Fine-tuned Model Approach

https://huggingface.co/protectai/deberta-v3-base-prompt-injection-v2

The ProtectAI team trained fine-tuned the microsoft deberta base model. It has been specifically optimized for prompt injection detection.

Attempting to replicate this training would be resource-intensive and likely produce inferior results. Instead, we should focus on efficiently deploying their pre-trained model in the .NET ecosystem.

### Limitations

deberta-v3-base-prompt-injection-v2 is highly accurate in identifying prompt injections in English. It does not detect jailbreak attacks or handle non-English prompts, which may limit its applicability in diverse linguistic environments or against advanced adversarial techniques.

Additionally, we do not recommend using this scanner for system prompts, as it produces false-positives.