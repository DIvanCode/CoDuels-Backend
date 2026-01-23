using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;

namespace Duely.Application.Services;

public static class ExeshStepsBuilder
{
    private const int DefaultTimeLimitMs = 2000;
    private const int DefaultMemoryLimitMb = 256;

    public static ExeshStep[] BuildRunSteps(string code, Language language, string input)
    {
        var codeSource = new InlineSource(code + "\n");
        var inputSource = new InlineSource(input + "\n");

        return language switch
        {
            Language.Python =>
            [
                new RunPyStep(
                    Code: codeSource,
                    RunInput: inputSource,
                    TimeLimitMs: DefaultTimeLimitMs,
                    MemoryLimitMb: DefaultMemoryLimitMb,
                    ShowOutput: true
                )
            ],
            Language.Cpp =>
            [
                new CompileCppStep(codeSource),

                new RunCppStep(
                    CompiledCode: new OtherStepSource("compile code"),
                    RunInput: inputSource,
                    TimeLimitMs: DefaultTimeLimitMs,
                    MemoryLimitMb: DefaultMemoryLimitMb,
                    ShowOutput: true
                )
            ],
            Language.Golang =>
            [
                new RunGoStep(
                    Code: codeSource,
                    RunInput: inputSource,
                    TimeLimitMs: DefaultTimeLimitMs,
                    MemoryLimitMb: DefaultMemoryLimitMb,
                    ShowOutput: true
                )
            ],
            _ => throw new NotSupportedException($"Language '{language}' is not supported for runs.")
        };
    }
}