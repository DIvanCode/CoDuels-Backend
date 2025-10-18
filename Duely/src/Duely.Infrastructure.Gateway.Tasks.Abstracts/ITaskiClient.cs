using System.Xml.XPath;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public interface ITaskiClient
{
    Task <Result> SendSubmission(
        string taskId,
        int submissionId,
        string solution,
        string language);

    Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken);

}
