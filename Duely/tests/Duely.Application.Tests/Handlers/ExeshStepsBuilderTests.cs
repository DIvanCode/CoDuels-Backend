using Duely.Application.UseCases.Features.UserCodeRuns;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests;

public class ExeshStepsBuilderTests
{
    [Fact]
    public void BuildRunSteps_Python_ReturnsRunPyStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print('hello')", "python", "input");

        steps.Should().HaveCount(1);
        steps[0].Should().BeOfType<RunPyStep>();
        var step = (RunPyStep)steps[0];
        step.Code.Should().BeOfType<InlineSource>();
        step.RunInput.Should().BeOfType<InlineSource>();
        step.TimeLimitMs.Should().Be(2000);
        step.MemoryLimitMb.Should().Be(256);
        step.ShowOutput.Should().BeTrue();
    }

    [Fact]
    public void BuildRunSteps_Py_ReturnsRunPyStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print('hello')", "py", "input");

        steps.Should().HaveCount(1);
        steps[0].Should().BeOfType<RunPyStep>();
    }

    [Fact]
    public void BuildRunSteps_Golang_ReturnsRunGoStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("package main\nfunc main() {}", "golang", "input");

        steps.Should().HaveCount(1);
        steps[0].Should().BeOfType<RunGoStep>();
        var step = (RunGoStep)steps[0];
        step.Code.Should().BeOfType<InlineSource>();
        step.RunInput.Should().BeOfType<InlineSource>();
        step.TimeLimitMs.Should().Be(2000);
        step.MemoryLimitMb.Should().Be(256);
        step.ShowOutput.Should().BeTrue();
    }

    [Fact]
    public void BuildRunSteps_Go_ReturnsRunGoStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("package main\nfunc main() {}", "go", "input");

        steps.Should().HaveCount(1);
        steps[0].Should().BeOfType<RunGoStep>();
    }

    [Fact]
    public void BuildRunSteps_Cpp_ReturnsCompileAndRunSteps()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("#include <iostream>", "cpp", "input");

        steps.Should().HaveCount(2);
        steps[0].Should().BeOfType<CompileCppStep>();
        steps[1].Should().BeOfType<RunCppStep>();
        
        var compileStep = (CompileCppStep)steps[0];
        compileStep.Code.Should().BeOfType<InlineSource>();
        
        var runStep = (RunCppStep)steps[1];
        runStep.CompiledCode.Should().BeOfType<OtherStepSource>();
        runStep.RunInput.Should().BeOfType<InlineSource>();
        runStep.TimeLimitMs.Should().Be(2000);
        runStep.MemoryLimitMb.Should().Be(256);
        runStep.ShowOutput.Should().BeTrue();
    }

    [Fact]
    public void BuildRunSteps_CPlusPlus_ReturnsCompileAndRunSteps()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("#include <iostream>", "c++", "input");

        steps.Should().HaveCount(2);
        steps[0].Should().BeOfType<CompileCppStep>();
        steps[1].Should().BeOfType<RunCppStep>();
    }

    [Fact]
    public void BuildRunSteps_UnknownLanguage_ThrowsNotSupportedException()
    {
        var act = () => ExeshStepsBuilder.BuildRunSteps("code", "unknown", "input");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Language 'unknown' is not supported for runs.*");
    }

    [Fact]
    public void BuildRunSteps_AddsNewlineToCode()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print(1)", "python", "input");

        var step = (RunPyStep)steps[0];
        var codeSource = (InlineSource)step.Code;
        codeSource.Content.Should().Be("print(1)\n");
    }

    [Fact]
    public void BuildRunSteps_AddsNewlineToInput()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print(1)", "python", "test input");

        var step = (RunPyStep)steps[0];
        var inputSource = (InlineSource)step.RunInput;
        inputSource.Content.Should().Be("test input\n");
    }

    [Fact]
    public void BuildRunSteps_CaseInsensitiveLanguage()
    {
        var steps1 = ExeshStepsBuilder.BuildRunSteps("print(1)", "PYTHON", "input");
        var steps2 = ExeshStepsBuilder.BuildRunSteps("print(1)", "Python", "input");
        var steps3 = ExeshStepsBuilder.BuildRunSteps("print(1)", "python", "input");

        steps1[0].Should().BeOfType<RunPyStep>();
        steps2[0].Should().BeOfType<RunPyStep>();
        steps3[0].Should().BeOfType<RunPyStep>();
    }

    [Fact]
    public void BuildRunSteps_TrimsLanguageWhitespace()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print(1)", "  python  ", "input");

        steps[0].Should().BeOfType<RunPyStep>();
    }
}

