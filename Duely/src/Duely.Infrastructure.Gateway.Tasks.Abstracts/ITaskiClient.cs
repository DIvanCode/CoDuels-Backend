using System.Xml.XPath;
using FluentResults;
namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public interface ITaskiClient
{
    Task <Result> SendSubmission(
        int taskId,
        int submissionId,
        string solution,
        string language);
}
