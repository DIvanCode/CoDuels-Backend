namespace Duely.Application.UseCases.Features.UserCodeRuns;

public class ExeshStepsBuilder
{
    private const int DefaultTimeLimitMs = 2000;
    private const int DefaultMemoryLimitMb = 256;

    public static object[] BuildRunSteps(string code, string language, string input)
    {
        var codeSource = new
        {
            type = "inline",
            content = code + "\n"
        };

        var inputSource = new
        {
            type = "inline",
            content = input + "\n"
        };

        var lang = NormalizeLanguage(language);

        return lang switch
        {
            KnownLanguage.Python =>
            [
                new
                {
                    name = "run code",
                    type = "run_py",
                    code = codeSource,
                    run_input = inputSource,
                    time_limit = DefaultTimeLimitMs,
                    memory_limit = DefaultMemoryLimitMb,
                    show_output = true
                }
            ],

            KnownLanguage.Golang =>
            [
                new
                {
                    name = "run code",
                    type = "run_go",
                    code = codeSource,
                    run_input = inputSource,
                    time_limit = DefaultTimeLimitMs,
                    memory_limit = DefaultMemoryLimitMb,
                    show_output = true
                }
            ],

            KnownLanguage.Cpp =>
            [
                new
                {
                    name = "compile code",
                    type = "compile_cpp",
                    code = codeSource
                },
                new
                {
                    name = "run code",
                    type = "run_cpp",
                    compiled_code = new
                    {
                        type = "other_step",
                        step_name = "compile code"
                    },
                    run_input = inputSource,
                    time_limit = DefaultTimeLimitMs,
                    memory_limit = DefaultMemoryLimitMb,
                    show_output = true
                }
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