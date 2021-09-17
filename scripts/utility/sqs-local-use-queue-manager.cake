using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda;
// using Amazon.Lambda.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

// Adapted from
// https://github.com/hudl/dotnet-microservice-cli-tools/blob/cdbb00c73c66f4cf7375f3b34f9eb8486355ed00/src/Hudl.Marvel.Tools.Shared/Utilities/LocalUseQueueManager/SqsLocalUseQueueManger.cs
public class SqsLocalUseQueueManager
{
    private const string CreateQueueFunction = "CreateJanusQueue";
    private const string DeleteQueueFunction = "DeleteJanusQueue";
    private readonly Lazy<IAmazonLambda> _lambdaClient;
    private readonly Lazy<IAmazonSQS> _sqsClient;
    
    public SqsLocalUseQueueManager()
    {       
        _lambdaClient = new Lazy<IAmazonLambda>(() => new AmazonLambdaClient(AwsCredentialsProvider.GetMarvelCredentials(), RegionEndpoint.USEast1));
        _sqsClient = new Lazy<IAmazonSQS>(() => new AmazonSQSClient(AwsCredentialsProvider.GetMarvelCredentials(), RegionEndpoint.USEast1));
    }

    public async System.Threading.Tasks.Task<bool> CreateLocalUseQueue(string environment, string service, string branch)
    {
        var request = new Amazon.Lambda.Model.InvokeRequest
        {
            FunctionName = CreateQueueFunction,
            Payload = CreatePayload(environment, service, branch)
        };
        var response = await _lambdaClient.Value.InvokeAsync(request);
        return response.HttpStatusCode == HttpStatusCode.OK;
    }
    
    public async System.Threading.Tasks.Task<bool> DoesQueueExist(string environment, string service, string branch)
    {
        var queueName = GenerateQueueName(environment, service, branch);
        return await DoesQueueExist(queueName);
    }

    private async System.Threading.Tasks.Task<bool> DoesQueueExist(string queueName)
    {
        try
        {
            var queueUrlResponse = await _sqsClient.Value.GetQueueUrlAsync(queueName);
            return !string.IsNullOrWhiteSpace(queueUrlResponse.QueueUrl);
        }
        catch (QueueDoesNotExistException)
        {
            return false;
        }
    }

    private static string CreatePayload(string environment, string service, string branch)
    {
        service = service.ToLower();
        branch = branch.ToLower();
        environment = environment.ToLower();
        return $"{{\"environment\": \"{environment}\", \"service\":\"{service}\", \"branch_name\":\"{branch}\"}}";
    }
    private string GenerateQueueName(string environment, string service, string branch)
    {
        service = service.ToLower();
        branch = branch.ToLower();
        environment = environment.ToLower();
        var envPrefix = GetEnvironmentPrefix(environment);

        var queueName = $"{envPrefix}-janus-{service}";
        if (!branch.Equals("master"))
        {
            queueName = $"{queueName}_{branch}";
        }
        return queueName;
    }
    private static string GetEnvironmentPrefix(string environment)
    {
        switch (environment)
        {
            case "thor":
                return "t";
            case "prod":
                return "p";
        }
        throw new Exception($"Unknown Enviroment {environment}");
    }
}
