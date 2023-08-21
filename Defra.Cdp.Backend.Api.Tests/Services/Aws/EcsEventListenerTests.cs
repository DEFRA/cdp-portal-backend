using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws;

public class EcsEventListenerTests
{
    [Fact]
    public void TestSplitImage()
    {
        var resp = EcsEventListener.SplitImage(
            "000000000.dkr.ecr.eu-west-2.amazonaws.com/cdp-portal-deployables-backend:v0.1.0");

        Assert.Equal("cdp-portal-deployables-backend", resp.Item1);
        Assert.Equal("v0.1.0", resp.Item2);
    }

    [Fact]
    public void TestJsonParsing()
    {
        var exampleMessage =
            @"{""version"":""0"",""id"":""db60b9dc-dc30-5d83-0d2a-3dc14c4abdc5"",""detail-type"":""ECS Task State Change"",""source"":""aws.ecs"",""account"":""163841473800"",""time"":""2023-05-22T10:33:27Z"",""region"":""eu-west-2"",""resources"":[""arn:aws:ecs:eu-west-2:163841473800:task/ecs-snd-cluster-backend/9ba462c0d4204acab93c9799f0014079""],""detail"":{""attachments"":[{""id"":""57f9670d-decd-4977-bc96-26dea1efc9e5"",""type"":""sdi"",""status"":""ATTACHED"",""details"":[]},{""id"":""5c2717a5-be05-4813-9cb8-f25e9f2a222b"",""type"":""elb"",""status"":""ATTACHED"",""details"":[]},{""id"":""e8b8dc67-df88-4e79-900b-8964feee33c3"",""type"":""eni"",""status"":""ATTACHED"",""details"":[{""name"":""subnetId"",""value"":""subnet-06a92ede025f9978b""},{""name"":""networkInterfaceId"",""value"":""eni-015f8990e226b755a""},{""name"":""macAddress"",""value"":""0a:9e:cc:03:c8:96""},{""name"":""privateDnsName"",""value"":""ip-10-212-109-73.eu-west-2.compute.internal""},{""name"":""privateIPv4Address"",""value"":""10.212.109.73""}]}],""attributes"":[{""name"":""ecs.cpu-architecture"",""value"":""x86_64""}],""availabilityZone"":""eu-west-2b"",""clusterArn"":""arn:aws:ecs:eu-west-2:163841473800:cluster/ecs-snd-cluster-backend"",""connectivity"":""CONNECTED"",""connectivityAt"":""2023-05-22T10:33:06.028Z"",""containers"":[{""containerArn"":""arn:aws:ecs:eu-west-2:163841473800:container/ecs-snd-cluster-backend/9ba462c0d4204acab93c9799f0014079/91abca72-8c0c-4b91-9127-65a8a446e197"",""lastStatus"":""RUNNING"",""name"":""cdp-portal-deployables-backend"",""image"":""163841473800.dkr.ecr.eu-west-2.amazonaws.com/cdp-portal-deployables-backend:0.29.0"",""imageDigest"":""sha256:ea139c4efc1e5d22982ba509bb3c39affc2f1e4b3e457d71e0c68d8f3f0ca8d5"",""runtimeId"":""9ba462c0d4204acab93c9799f0014079-3462610887"",""taskArn"":""arn:aws:ecs:eu-west-2:163841473800:task/ecs-snd-cluster-backend/9ba462c0d4204acab93c9799f0014079"",""networkInterfaces"":[{""attachmentId"":""e8b8dc67-df88-4e79-900b-8964feee33c3"",""privateIpv4Address"":""10.212.109.73""}],""cpu"":""256"",""memory"":""256"",""managedAgents"":[{""name"":""ExecuteCommandAgent"",""status"":""RUNNING"",""lastStartedAt"":""2023-05-22T10:33:24.278Z""}]}],""cpu"":""1024"",""createdAt"":""2023-05-22T10:33:02.807Z"",""desiredStatus"":""RUNNING"",""enableExecuteCommand"":true,""ephemeralStorage"":{""sizeInGiB"":20},""group"":""service:cdp-portal-deployables-backend"",""launchType"":""FARGATE"",""lastStatus"":""RUNNING"",""memory"":""8192"",""overrides"":{""containerOverrides"":[{""name"":""cdp-portal-deployables-backend""}]},""platformVersion"":""1.4.0"",""pullStartedAt"":""2023-05-22T10:33:14.133Z"",""pullStoppedAt"":""2023-05-22T10:33:18.193Z"",""startedAt"":""2023-05-22T10:33:27.789Z"",""startedBy"":""ecs-svc/5993655974023057049"",""taskArn"":""arn:aws:ecs:eu-west-2:163841473800:task/ecs-snd-cluster-backend/9ba462c0d4204acab93c9799f0014079"",""taskDefinitionArn"":""arn:aws:ecs:eu-west-2:163841473800:task-definition/cdp-portal-deployables-backend:40"",""updatedAt"":""2023-05-22T10:33:27.789Z"",""version"":4}}";

        var parsed = JsonSerializer.Deserialize<EcsEvent>(exampleMessage);

        Assert.NotNull(parsed);
        Assert.Single(parsed.Detail.Containers);
    }
}