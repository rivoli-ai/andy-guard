# Andy.Guard's Tokenizers 

## DeBERTa

Andy.Guard relies on DeBERTa v3. This [Microsoft's BERT-family model](https://huggingface.co/microsoft/deberta-v3-base) is one of the SOTA models for NLU tasks, especially useful for detecting prompt injection attacks.

DeBERTa v3 builds on v2 with key improvements to the model architecture and training objectives, while keeping the same tokenizer. 

v3 introduces architectural refinements for better parameter sharing and efficiency, leading to stronger performance on standard NLP benchmarks with the same compute budget. **Since the input format and vocabulary did not change**, Hugging Face continues to use the DebertaV2Tokenizer for v3 models—the innovations are inside the model, not in the tokenizer.

## Main Files in ./ONNX

- Based on Protect AI's [repo](https://huggingface.co/protectai/deberta-v3-base-prompt-injection-v2)
- Commit: e6535ca4ce3ba852083e75ec585d7c8aeb4be4c5
- Commited on May 28, 2024

### spm.model

SentencePiece is a library developed by Google for text tokenization. The spm.model file is a SentencePiece model, stored as a serialized Protocol Buffer (protobuf). It combines both a vocabulary and the tokenization rules. Unlike a plain vocab file (just a list of tokens), it encodes the entire tokenization logic — including normalization, segmentation (BPE or Unigram), and the mapping from tokens to integer IDs — in a binary protobuf format. This makes it portable and ensures that any framework (PyTorch, TensorFlow, ONNX, etc.) can reproduce exactly the same tokenization. In short, spm.model is not just a vocab file, but a self-contained, protobuf-based tokenizer definition.

The .model file is a binary protobuf file that contains:
- The vocabulary (list of tokens → IDs).
- The rules for how to split or merge characters into tokens (BPE or Unigram).
- Definitions of special tokens like <unk>, <s>, </s>, [CLS], [SEP], etc.

### model.onnx

This is the model exported in the ONNX (Open Neural Network Exchange) format—an open standard for representing machine learning models. It encodes the model’s entire computational graph and weights, enabling framework-agnostic inference and optimization via ONNX Runtime

### tokenizer.json

This file describes the tokenizer configuration in a structured JSON format suitable for Hugging Face’s “Fast” tokenizers (Rust-backed). It includes token-to-ID mappings, special tokens, pre- and post-processing rules, and other metadata. Andy.Guard uses this file to retrieve the the special token IDs.

## Reference Docs:

- [Official GitHub Repo](https://github.com/microsoft/DeBERTa)
- [Microsoft's deployment on HF](https://huggingface.co/microsoft/deberta-v3-base)
- [HuggingFace's integration in Transformers python lib](https://huggingface.co/docs/transformers/en/model_doc/deberta-v2#transformers.DebertaV2Model_)
- [Protect AI's Prompt Injection Scanner](https://github.com/protectai/llm-guard/blob/main/llm_guard/input_scanners/prompt_injection.py)