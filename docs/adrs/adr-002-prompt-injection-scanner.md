# Building a .NET LLM Guard PromptInjection Scanner Proxy for vLLM

LLM Guard's PromptInjection scanner represents a sophisticated security solution that can be effectively implemented in .NET as a high-performance proxy for vLLM. The implementation combines state-of-the-art machine learning models with enterprise-grade .NET infrastructure to create a production-ready security layer for LLM applications.

## LLM Guard PromptInjection scanner architecture and implementation

LLM Guard's PromptInjection scanner uses a **DeBERTa-v3-base transformer model** fine-tuned on 300k+ training samples across 22 datasets, achieving **95.25% accuracy** with **99.74% recall** for prompt injection detection. The core architecture follows a modular pipeline: input reception → tokenization → feature extraction → binary classification → threshold-based filtering.

The scanner implements several sophisticated technical components. **DeBERTa's disentangled attention mechanism** separates content and position representations, providing superior understanding of injection patterns compared to traditional BERT models. The model processes inputs with a **maximum 512-token length** using subword tokenization, then applies classification to generate probability-based risk scores.

Configuration flexibility centers around two key parameters: **threshold** (default 0.5) for detection sensitivity, and **match_type** for handling longer prompts. The scanner supports **ONNX optimization** for 20-40% faster inference and 30% memory reduction, making it suitable for real-time applications with **50-200ms CPU latency** and **10-50ms GPU latency**.

The underlying ML model uses comprehensive training data covering direct and indirect prompt injections while explicitly excluding jailbreak attacks. Training employed **multi-dataset fine-tuning** with 20+ hyperparameter configurations tested, resulting in **99.93% training accuracy** and strong generalization to production scenarios.

## vLLM integration patterns and performance optimization

vLLM provides an **OpenAI-compatible API** through its FastAPI-based architecture, making integration straightforward for .NET proxy implementations. The core endpoints include `/v1/chat/completions`, `/v1/completions`, and `/v1/models`, with comprehensive request/response schemas supporting both OpenAI parameters and vLLM-specific extensions like `best_of` and `use_beam_search`.

**Authentication** uses Bearer token middleware checking the Authorization header, with support for environment variable configuration (`VLLM_API_KEY`) and command-line setup. Production deployments should implement additional security layers through reverse proxy authentication, API gateways with Lambda authorizers, or service mesh integration.

Performance optimization leverages vLLM's **PagedAttention memory management** and **continuous batching** capabilities. Recent benchmarks show **2.7x throughput improvement** on Llama 8B models and **5x faster time-per-output-token** compared to previous versions. Key optimization strategies include:

```bash
# Optimized vLLM configuration
vllm serve model \
    --gpu-memory-utilization 0.95 \
    --max-num-batched-tokens 4096 \
    --enable-chunked-prefill \
    --tensor-parallel-size 4
```

**Rate limiting** implements request-level controls through concurrent request limiting (`--max-num-seqs 256`) and scheduler-based priority management. External rate limiting can be added using Redis-based middleware with configurable limits per client or API key.

The proxy architecture supports multiple deployment patterns: NGINX reverse proxy for load balancing, HAProxy for health-checked routing, and Kubernetes with horizontal pod autoscaling. **Monitoring integration** provides comprehensive Prometheus metrics including request latency, token throughput, GPU cache utilization, and error rates.

## .NET ML and AI implementation strategy

**.NET ML capabilities** center on **ML.NET 2.0+** with NAS-BERT integration through TorchSharp, providing native text classification with fine-tuning support. The TextClassification API handles up to 512 tokens with automatic truncation and supports multi-class classification scenarios essential for security applications.

**ONNX Runtime integration** offers the most promising approach for deploying LLM Guard's DeBERTa model in .NET environments. Key implementation components include:

```csharp
// ONNX Runtime setup for transformer inference
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

var sessionOptions = new SessionOptions();
sessionOptions.AppendExecutionProvider_DML(deviceId); // DirectML for Windows GPU
var session = new InferenceSession("deberta-prompt-injection.onnx", sessionOptions);

// Optimized inference pipeline
using var inputTensor = OrtValue.CreateTensorWithEmptyStrings(
    OrtAllocator.DefaultInstance, new long[] { 1, 1 });
inputTensor.StringTensorSetElementAt(inputText, 0);

var inputs = new Dictionary<string, OrtValue> { { "input", inputTensor } };
using var outputs = session.Run(runOptions, inputs, session.OutputNames);
var probability = outputs["probability"].GetTensorDataAsSpan<float>()[0];
```

**HuggingFace model deployment** requires model conversion from the original DeBERTa checkpoint to ONNX format using HuggingFace Optimum. The conversion workflow uses `transformers.onnx` for export, followed by ONNX Runtime transformer optimization for production deployment.

Critical implementation challenges include **manual tokenization** since .NET lacks direct sentence-transformers equivalent, requiring custom BPE/WordPiece implementations or pre-tokenized input handling. **Performance optimization** focuses on object pooling, `Span<T>` usage for zero-copy operations, and proper resource disposal patterns.

## .NET proxy architecture patterns with ASP.NET Core and YARP

**ASP.NET Core middleware** provides the foundation for implementing LLM Guard as a security proxy using the Chain of Responsibility pattern. The recommended middleware order places security components early: exception handling → HTTPS redirection → authentication → authorization → custom security middleware → reverse proxy routing.

**YARP (Yet Another Reverse Proxy)** offers production-ready reverse proxy functionality with native .NET integration:

```csharp
// YARP configuration for vLLM proxy
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Security middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PromptInjectionMiddleware>(); // Custom LLM Guard integration
app.MapReverseProxy();
```

**Authentication integration** supports JWT Bearer tokens with comprehensive validation including signature verification, issuer validation, and expiration checks. Production patterns include API key management through environment variables, OAuth 2.0 flows for user authentication, and service account authentication for downstream services.

**Monitoring and observability** integrate Serilog with Application Insights for structured logging, Prometheus metrics collection for performance monitoring, and health checks for service availability. Key metrics include request throughput, prompt injection detection rates, false positive rates, and downstream service latency.

The architecture supports **horizontal scaling** through stateless design, proper session management, and container orchestration. Kubernetes deployment patterns include horizontal pod autoscaling based on CPU/memory utilization, health probes for liveness and readiness checks, and service mesh integration for advanced traffic management.

## Complete technical implementation architecture

The optimal implementation combines all components into a layered security architecture:

**Layer 1: Security Middleware**
```csharp
public class PromptInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InferenceSession _onnxSession;
    private readonly ILogger<PromptInjectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/v1/chat/completions"))
        {
            var request = await DeserializeChatRequest(context.Request);
            
            // Extract prompts from messages
            var prompts = request.Messages.Select(m => m.Content);
            
            foreach (var prompt in prompts)
            {
                var injectionScore = await DetectInjectionAsync(prompt);
                if (injectionScore > 0.7f)
                {
                    _logger.LogWarning("Prompt injection detected: {Score}", injectionScore);
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Request blocked: potential injection detected");
                    return;
                }
            }
        }

        await _next(context);
    }

    private async Task<float> DetectInjectionAsync(string prompt)
    {
        // Tokenize input (simplified - production needs proper tokenization)
        var tokenized = TokenizeText(prompt);
        
        // Create ONNX input tensor
        using var inputTensor = OrtValue.CreateTensorValueFromMemory(tokenized);
        var inputs = new Dictionary<string, OrtValue> { { "input_ids", inputTensor } };
        
        // Run inference
        using var outputs = await Task.Run(() => _onnxSession.Run(RunOptions.Default, inputs, _onnxSession.OutputNames));
        
        // Extract probability score
        var logits = outputs["logits"].GetTensorDataAsSpan<float>();
        return Softmax(logits)[1]; // Index 1 for injection class
    }
}
```

**Layer 2: YARP Proxy Configuration**
```json
{
  "ReverseProxy": {
    "Routes": {
      "llm-route": {
        "ClusterId": "vllm-cluster",
        "Match": { "Path": "/v1/{**catch-all}" },
        "Transforms": [
          { "RequestHeader": "Authorization", "Set": "Bearer {env:VLLM_API_KEY}" },
          { "RequestHeader": "X-Forwarded-For", "Append": "{RemoteIpAddress}" }
        ]
      }
    },
    "Clusters": {
      "vllm-cluster": {
        "Destinations": {
          "vllm-primary": { "Address": "http://vllm-service:8000/" },
          "vllm-secondary": { "Address": "http://vllm-service-2:8000/" }
        },
        "LoadBalancingPolicy": "RoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      }
    }
  }
}
```

**Layer 3: Performance Optimization**
```csharp
public class OptimizedLLMGuardService : IDisposable
{
    private readonly ObjectPool<InferenceSession> _sessionPool;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _semaphore;

    public async Task<float> ClassifyAsync(string text)
    {
        // Check cache first
        var cacheKey = ComputeHash(text);
        if (_cache.TryGetValue(cacheKey, out float cachedScore))
            return cachedScore;

        // Acquire semaphore for rate limiting
        await _semaphore.WaitAsync();
        try
        {
            var session = _sessionPool.Get();
            try
            {
                var score = await PerformInference(session, text);
                _cache.Set(cacheKey, score, TimeSpan.FromMinutes(10));
                return score;
            }
            finally
            {
                _sessionPool.Return(session);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

## Security considerations and deployment best practices

**Production security** requires defense-in-depth implementation with multiple validation layers. Input sanitization handles malicious content before ML model processing, while output validation ensures responses don't leak sensitive information. Rate limiting prevents abuse with per-user and per-endpoint controls.

**Model security** involves encrypting ONNX model files using Azure Key Vault integration, implementing runtime isolation through containerization, and establishing RBAC controls for model access. Regular model updates address new attack patterns and maintain detection effectiveness.

**Monitoring and alerting** track security metrics including prompt injection detection rates, false positive/negative analysis, and unusual request patterns. Integration with Application Insights provides real-time dashboards and automated alerting for security incidents.

**Docker deployment** uses multi-stage builds for security hardening:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LLMGuardProxy.dll"]
```

**Kubernetes deployment** supports horizontal pod autoscaling, health probes, and service mesh integration for production-grade availability and security. Container security scanning in CI/CD pipelines ensures vulnerability-free deployments.

## Performance benchmarks and optimization strategies

Expected performance characteristics include **10-50ms inference latency** for prompt injection detection using ONNX Runtime on CPU, with **2-3x improvement** using GPU acceleration. **Memory requirements** total 600MB for model weights plus 1-2GB runtime memory, with 30% reduction using ONNX optimization.

**Throughput optimization** implements batching strategies with optimal batch sizes of 16-32 requests, connection pooling for HTTP clients, and distributed caching using Redis. Multi-level caching reduces repeated inference costs while maintaining security effectiveness.

**CI/CD integration** includes automated model quality gates checking accuracy thresholds, security scanning for container vulnerabilities, and performance benchmarking to prevent regression. MLOps pipelines support model versioning, A/B testing for model updates, and automated rollback capabilities.

This comprehensive architecture delivers enterprise-grade security for LLM applications while maintaining high performance and scalability. The combination of proven ML models, robust .NET infrastructure, and production-ready deployment patterns creates a practical solution for organizations requiring sophisticated prompt injection protection in their LLM deployments.

## DeBERTa-v3-base Model and H100 GPU Compatibility

Absolutely, the **DeBERTa-v3-base model fits comfortably on H100 GPUs** with significant room to spare. This is actually a case of using high-end hardware for a relatively small model, which provides excellent performance benefits.

### DeBERTa-v3-base Technical Specifications

The **DeBERTa-v3-base model** is surprisingly compact for its performance:

- **Parameters**: 184M total (86M backbone + 98M embedding layer)
- **Architecture**: 12 layers with 768 hidden dimensions
- **Memory Requirements**:
  - **Inference (FP16/BF16)**: ~350MB
  - **Largest Layer**: 187.65MB
  - **Training with Adam**: 1.37GB
  - **Vocabulary**: 128K tokens (much larger than original BERT's 30K)

### H100 GPU Memory Capacity

H100 GPUs come in several memory configurations:
- **Standard H100**: 80GB HBM3 memory
- **H100 NVL**: 94-96GB HBM3 memory  
- **Memory Bandwidth**: 3TB/s (2x faster than A100)
- **Native FP8 Support**: Hardware-optimized for transformer inference

### Memory Utilization Analysis

The DeBERTa-v3-base model uses **less than 0.5% of H100's memory capacity**:

```
Model Memory Usage on H100:
├── Inference (FP16): 350MB / 80GB = 0.44%
├── Training (Adam): 1.37GB / 80GB = 1.71%
└── Available Headroom: ~78GB remaining
```

### Performance Benefits on H100

Using H100 for DeBERTa-v3-base provides several advantages:

**1. Ultra-Low Latency**
- **Expected Inference**: 1-5ms per batch
- **Transformer Engine**: Native acceleration for attention mechanisms
- **Memory Bandwidth**: 3TB/s eliminates memory bottlenecks

**2. Massive Batch Processing**
```python
# Theoretical batch sizes on H100
batch_size_estimates = {
    "Conservative": 1000,  # ~500MB total
    "Aggressive": 5000,    # ~2.5GB total  
    "Maximum": 10000+      # Still <10GB total
}
```

**3. Multi-Model Deployment**
You could run **dozens of DeBERTa-v3-base models simultaneously**:
- **200+ models** for different domains/tasks
- **Multi-tenant** security scanning
- **A/B testing** different model versions

### Practical Implementation Considerations

**Optimal Configuration for LLM Guard Proxy:**
```python
# ONNX Runtime configuration for H100
session_options = SessionOptions()
session_options.providers = ['CUDAExecutionProvider']
session_options.cuda_device_id = 0
session_options.enable_gpu_mem_arena = False  # Not needed with 80GB
session_options.gpu_mem_limit = 1024 * 1024 * 1024  # 1GB limit (overkill)
```

**Multi-Instance Deployment:**
- Use **NVIDIA MIG** to partition H100 into 7 instances
- Each instance gets ~11GB memory
- Run independent DeBERTa models per instance
- Perfect for multi-tenant environments

### Cost-Performance Analysis

While H100 provides excellent performance, consider the economics:

**Overprovisioned Scenario:**
- H100: $25,000-40,000
- Model needs: <1GB memory
- **Utilization**: <1% of GPU capacity

**Alternative Approaches:**
1. **Shared Deployment**: Run 100+ small models on single H100
2. **RTX 4090**: 24GB memory, 1/10th the cost, still 50x oversized
3. **Cloud Inference**: Pay-per-use for smaller workloads

### Real-World Deployment Recommendations

**For Production LLM Guard:**
```yaml
# Kubernetes deployment example
resources:
  requests:
    nvidia.com/gpu: "1"     # Full H100
    memory: "8Gi"           # System memory
  limits:
    memory: "16Gi"
    
# Or using MIG for cost efficiency
mig:
  profile: "1g.10gb"        # 1/7th of H100
  instances: 7              # Multiple DeBERTa deployments
```

**Performance Expectations:**
- **Throughput**: 10,000+ requests/second
- **Latency**: <2ms per inference
- **Concurrent Models**: 50+ different variants
- **Memory Efficiency**: 99% unused capacity available

### Bottom Line

The DeBERTa-v3-base model **dramatically underutilizes** an H100 GPU's capabilities. While it absolutely fits and runs with exceptional performance, you're using a Formula 1 car for a grocery run. The combination works brilliantly for high-throughput production environments where you need maximum performance, but consider whether the cost justifies the performance gain for your specific use case.

For most LLM Guard deployments, this setup provides **massive scaling headroom** and **future-proofing** for larger models, making it ideal for enterprise environments expecting growth or requiring ultra-low latency guarantees.