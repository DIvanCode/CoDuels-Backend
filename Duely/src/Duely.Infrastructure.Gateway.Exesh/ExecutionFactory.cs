using System.Text.Json.Serialization;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;

namespace Duely.Infrastructure.Gateway.Exesh;

internal static class ExecutionFactory
{
    private const string CodeSourceName = "code";
    private const string InputSourceName = "input";
    private const string CompileJobName = "compile code";
    private const string RunJobName = "run code";
    private const string StageName = "run";
    private const string OkStatus = "OK";
    private const int CompileTimeLimitMs = 5000;
    private const int CompileMemoryLimitMb = 256;

    public static ExeshExecuteRequest Build(ExecuteCodeRequest request)
    {
        var codeContent = request.Code + "\n";
        var inputContent = request.Input + "\n";

        var sources = new List<ExeshSourceDefinition>
        {
            new("inline", CodeSourceName, codeContent),
            new("inline", InputSourceName, inputContent)
        };

        var jobs = BuildJobs(request);
        var stages = new List<ExeshStageDefinition>
        {
            new(StageName, [], jobs)
        };

        return new ExeshExecuteRequest(sources, stages);
    }

    private static IReadOnlyCollection<ExeshJobDefinition> BuildJobs(ExecuteCodeRequest request)
    {
        return NormalizeLanguage(request.Language) switch
        {
            "python" =>
            [
                new ExeshJobDefinition(
                    Type: "run_py",
                    Name: RunJobName,
                    SuccessStatus: OkStatus,
                    CategoryName: BuildCategoryName(request, "run_py"),
                    Code: new InlineInputDefinition("inline", CodeSourceName),
                    Input: new InlineInputDefinition("inline", InputSourceName),
                    TimeLimit: request.TimeLimit,
                    MemoryLimit: request.MemoryLimit,
                    ShowOutput: true)
            ],

            "golang" =>
            [
                new ExeshJobDefinition(
                    Type: "compile_go",
                    Name: CompileJobName,
                    SuccessStatus: OkStatus,
                    CategoryName: BuildCategoryName(request, "compile_go"),
                    Code: new InlineInputDefinition("inline", CodeSourceName),
                    TimeLimit: CompileTimeLimitMs,
                    MemoryLimit: CompileMemoryLimitMb),
                new ExeshJobDefinition(
                    Type: "run_go",
                    Name: RunJobName,
                    SuccessStatus: OkStatus,
                    CategoryName: BuildCategoryName(request, "run_go"),
                    CompiledCode: new ArtifactInputDefinition("artifact", CompileJobName),
                    Input: new InlineInputDefinition("inline", InputSourceName),
                    TimeLimit: request.TimeLimit,
                    MemoryLimit: request.MemoryLimit,
                    ShowOutput: true)
            ],

            "cpp" =>
            [
                new ExeshJobDefinition(
                    Type: "compile_cpp",
                    Name: CompileJobName,
                    SuccessStatus: OkStatus,
                    CategoryName: BuildCategoryName(request, "compile_cpp"),
                    Code: new InlineInputDefinition("inline", CodeSourceName),
                    TimeLimit: CompileTimeLimitMs,
                    MemoryLimit: CompileMemoryLimitMb),
                new ExeshJobDefinition(
                    Type: "run_cpp",
                    Name: RunJobName,
                    SuccessStatus: OkStatus,
                    CategoryName: BuildCategoryName(request, "run_cpp"),
                    CompiledCode: new ArtifactInputDefinition("artifact", CompileJobName),
                    Input: new InlineInputDefinition("inline", InputSourceName),
                    TimeLimit: request.TimeLimit,
                    MemoryLimit: request.MemoryLimit,
                    ShowOutput: true)
            ],

            _ => throw new NotSupportedException($"Language '{request.Language}' is not supported for runs.")
        };
    }

    private static string BuildCategoryName(ExecuteCodeRequest request, string jobType)
        => $"{request.DuelId}, {request.UserId}, {request.TaskKey}, {jobType}";

    private static string NormalizeLanguage(string language)
        => language.Trim().ToLowerInvariant();
}

internal sealed record ExeshExecuteRequest(
    [property: JsonPropertyName("sources")] IReadOnlyCollection<ExeshSourceDefinition> Sources,
    [property: JsonPropertyName("stages")] IReadOnlyCollection<ExeshStageDefinition> Stages
);

internal sealed record ExeshSourceDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("content")] string Content
);

internal sealed record ExeshStageDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("deps")] IReadOnlyCollection<string> Deps,
    [property: JsonPropertyName("jobs")] IReadOnlyCollection<ExeshJobDefinition> Jobs
);

internal sealed record ExeshJobDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("success_status")] string SuccessStatus,
    [property: JsonPropertyName("category_name")] string CategoryName,
    [property: JsonPropertyName("code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InlineInputDefinition? Code = null,
    [property: JsonPropertyName("input"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    InlineInputDefinition? Input = null,
    [property: JsonPropertyName("compiled_code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ArtifactInputDefinition? CompiledCode = null,
    [property: JsonPropertyName("time_limit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? TimeLimit = null,
    [property: JsonPropertyName("memory_limit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MemoryLimit = null,
    [property: JsonPropertyName("show_output"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? ShowOutput = null
);

internal sealed record InlineInputDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("source")] string Source
);

internal sealed record ArtifactInputDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("job")] string Job
);
