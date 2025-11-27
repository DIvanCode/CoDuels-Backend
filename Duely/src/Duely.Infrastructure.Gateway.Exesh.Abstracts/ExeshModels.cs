using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Exesh.Abstracts;

public abstract record ExeshSource(
    [property: JsonPropertyName("type")] string Type
);

public sealed record InlineSource(
    [property: JsonPropertyName("content")] string Content
) : ExeshSource("inline");

public sealed record OtherStepSource(
    [property: JsonPropertyName("step_name")] string StepName
) : ExeshSource("other_step");

public abstract record ExeshStep(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type
);

public sealed record RunPyStep(
    [property: JsonPropertyName("code")] InlineSource Code,
    [property: JsonPropertyName("run_input")] InlineSource RunInput,
    [property: JsonPropertyName("time_limit")] int TimeLimitMs,
    [property: JsonPropertyName("memory_limit")] int MemoryLimitMb,
    [property: JsonPropertyName("show_output")] bool ShowOutput
) : ExeshStep("run code", "run_py");

public sealed record RunGoStep(
    [property: JsonPropertyName("code")] InlineSource Code,
    [property: JsonPropertyName("run_input")] InlineSource RunInput,
    [property: JsonPropertyName("time_limit")] int TimeLimitMs,
    [property: JsonPropertyName("memory_limit")] int MemoryLimitMb,
    [property: JsonPropertyName("show_output")] bool ShowOutput
) : ExeshStep("run code", "run_go");

public sealed record CompileCppStep(
    [property: JsonPropertyName("code")] InlineSource Code
) : ExeshStep("compile code", "compile_cpp");

public sealed record RunCppStep(
    [property: JsonPropertyName("compiled_code")] OtherStepSource CompiledCode,
    [property: JsonPropertyName("run_input")] InlineSource RunInput,
    [property: JsonPropertyName("time_limit")] int TimeLimitMs,
    [property: JsonPropertyName("memory_limit")] int MemoryLimitMb,
    [property: JsonPropertyName("show_output")] bool ShowOutput
) : ExeshStep("run code", "run_cpp");
