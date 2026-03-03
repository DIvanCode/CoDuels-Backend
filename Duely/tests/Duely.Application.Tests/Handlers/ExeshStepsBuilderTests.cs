using Duely.Application.Services;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public class ExeshStepsBuilderTests
{
    [Fact]
    public void BuildRunSteps_Python_ReturnsRunPyStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print('hello')", Language.Python, "input");

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
    public void BuildRunSteps_Golang_ReturnsRunGoStep()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("package main\nfunc main() {}", Language.Golang, "input");

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
    public void BuildRunSteps_Cpp_ReturnsCompileAndRunSteps()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("#include <iostream>", Language.Cpp, "input");

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
    public void BuildRunSteps_UnknownLanguage_ThrowsNotSupportedException()
    {
        var act = () => ExeshStepsBuilder.BuildRunSteps("code", (Language)999, "input");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Language '999' is not supported for runs.*");
    }

    [Fact]
    public void BuildRunSteps_AddsNewlineToCode()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print(1)", Language.Python, "input");

        var step = (RunPyStep)steps[0];
        var codeSource = (InlineSource)step.Code;
        codeSource.Content.Should().Be("print(1)\n");
    }

    [Fact]
    public void BuildRunSteps_AddsNewlineToInput()
    {
        var steps = ExeshStepsBuilder.BuildRunSteps("print(1)", Language.Python, "test input");

        var step = (RunPyStep)steps[0];
        var inputSource = (InlineSource)step.RunInput;
        inputSource.Content.Should().Be("test input\n");
    }

    
}

