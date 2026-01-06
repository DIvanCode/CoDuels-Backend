using Duely.Infrastructure.Gateway.Exesh.Abstracts;

namespace Duely.Application.Services;

public static class ExeshStepsBuilder
{
    private const int DefaultTimeLimitMs = 2000;
    private const int DefaultMemoryLimitMb = 256;

    public static ExeshStep[] BuildRunSteps(string code, string language, string input)
    {
        var codeSource = new InlineSource(code + "\n");


        var inputSource = new InlineSource(input + "\n");

        var lang = NormalizeLanguage(language);

        return lang switch
        {
            KnownLanguage.Python =>
            [
                new RunPyStep(
                    Code: codeSource,
                    RunInput: inputSource,
                    TimeLimitMs: DefaultTimeLimitMs,
                    MemoryLimitMb: DefaultMemoryLimitMb,
                    ShowOutput: true
                )
            ],

            KnownLanguage.Golang =>
            [
                new RunGoStep(
                    Code: codeSource,
                    RunInput: inputSource,
                    TimeLimitMs: DefaultTimeLimitMs,
                    MemoryLimitMb: DefaultMemoryLimitMb,
                    ShowOutput: true
                )
            ],

            KnownLanguage.Cpp =>
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

            _ => throw new NotSupportedException($"Language '{language}' is not supported for runs.")
        };

    }


    private static KnownLanguage NormalizeLanguage(string language)
    {
        var l = language.Trim().ToLowerInvariant();
        return l switch
        {
            "python" or "py" => KnownLanguage.Python,
            "golang" or "go" => KnownLanguage.Golang,
            "cpp" or "c++" => KnownLanguage.Cpp,
            _ => KnownLanguage.Unknown
        };
    }

    private enum KnownLanguage
    {
        Unknown = 0,
        Python = 1,
        Golang = 2,
        Cpp = 3
    }
}