using System.Text.Json;
using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static System.Text.Json.JsonSerializer;

namespace Defra.Cdp.Backend.Api.Tests.Services.TenantArtifacts;

public class DockerClientTests
{
    private readonly ArtifactScanAndStore _artifactScanAndStore;
    private readonly IDeployableArtifactsService _deployableServiceMock = Substitute.For<IDeployableArtifactsService>();
    private readonly IDockerClient _dockerClientMock = Substitute.For<IDockerClient>();

    private readonly ILayerService _layerServiceMock = Substitute.For<ILayerService>();
    private readonly IRepositoryService _repositoryService = Substitute.For<IRepositoryService>();

    public DockerClientTests()
    {
        _artifactScanAndStore = new ArtifactScanAndStore(_deployableServiceMock, _layerServiceMock, _dockerClientMock,
            _repositoryService, ConsoleLogger.CreateLogger<ArtifactScanAndStore>());
    }

    [Fact]
    public void DockerServiceShouldExtractFilesFromTgzStream()
    {
        var filesToExtract = new List<Regex>
        {
            new(".+/.+\\.deps\\.json$"), // TODO: find out what the WORKSPACE is in the c# base image
            new("home/node.*/package-lock\\.json$"), // TODO: exclude anything in node_modules etc
            new(".*/pom\\.xml$") // TODO: find out what our jvm image is going to look like and what the build system of choice is
        };

        // A list of paths we dont want to scan (stuff in the base image basically, avoids false positives
        var pathsToIgnore = new List<Regex> { new("^/?usr/.*") };
        var mockHttp = Substitute.For<HttpClient>();
        var opts = new DockerServiceOptions();
        var client = new DockerClient(mockHttp, Options.Create(opts), new EmptyDockerCredentialProvider(),
            NullLoggerFactory.Instance.CreateLogger<DockerClient>());

        using (var fs = File.OpenRead("Resources/testlayer.tgz"))
        {
            var files = client.ExtractFilesFromStream(fs, "test:1.0.0", filesToExtract, pathsToIgnore);
            Assert.Single(files);
            Assert.Equal("home/node/package-lock.json", files[0].FileName);
            Assert.Equal("{\n\t\"should_be_found\": true\n}\n", files[0].Content);
        }
    }


    [Fact]
    public void FlattenFilesShouldHandleDupes()
    {
        var layers = new Layer[]
        {
            new("aaa", new List<LayerFile> { new("foo", "111") }),
            new("bbb", new List<LayerFile> { new("foo", "222") })
        };

        var results = ArtifactScanAndStore.FlattenFiles(layers);

        Assert.Single(results);
        Assert.Equal("bbb", results["foo"].LayerSha256);
    }


    [Fact]
    public async Task ScanImageShouldSaveAnArtifact()
    {
        // mock manifest
        var cfg = new Blob("", "digest-cfg");
        var files = new Blob("", "digest-files");

        _dockerClientMock
            .LoadManifest("foo", "1.0.0")!
            .Returns(
                Task.FromResult(new Manifest
                {
                    name = "foo",
                    tag = "1.0.0",
                    digest = "sha256:b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c",
                    config = cfg,
                    layers = new List<Blob> { files }
                })
            );

        // mock reading cfg layer
        _dockerClientMock.SearchLayer("foo", cfg, Arg.Any<List<Regex>>(), Arg.Any<List<Regex>>())
            .Returns(Task.FromResult(new Layer(cfg.digest, [])));

        // mock file layer
        _dockerClientMock.SearchLayer("foo", files, Arg.Any<List<Regex>>(), Arg.Any<List<Regex>>())
            .Returns(Task.FromResult(new Layer(files.digest,
                [new LayerFile("package-lock.json", "{\"name\": \"foo\"}")])));

        var labels = new Dictionary<string, string>
        {
            ["defra.cdp.git.repo.url"] = "https://github.com/foo/foo",
            ["defra.cdp.service.name"] = "foo"
        };

        _dockerClientMock
            .LoadManifestImage("foo", cfg)!
            .Returns(
                Task.FromResult(new ManifestImage(new ManifestImageConfig(labels, ""), new DateTime()))
            );


        var res = await _artifactScanAndStore.ScanImage("foo", "1.0.0", CancellationToken.None);
        Assert.True(res.Success);
        Assert.NotNull(res.Artifact);
        var artifact = res.Artifact;

        Assert.Equal(1, artifact?.ScannerVersion);
        Assert.Equal("foo", artifact?.Repo);
        Assert.Equal("1.0.0", artifact?.Tag);
        Assert.Equal(4294967296, artifact?.SemVer);
        Assert.Equal("https://github.com/foo/foo", artifact?.GithubUrl);
        Assert.Equal("foo", artifact?.ServiceName);
        Assert.Empty(artifact!.Files);
        Assert.Equal("sha256:b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c", artifact.Sha256);
    }

    [Fact]
    public Task TestParsingCreatedDateTimeForManifestImage()
    {
        const string imageAsJson = """
                                   {
                                     "architecture": "amd64",
                                     "config": {
                                       "Hostname": "",
                                       "Domainname": "",
                                       "User": "",
                                       "AttachStdin": false,
                                       "AttachStdout": false,
                                       "AttachStderr": false,
                                       "ExposedPorts": {
                                         "443/tcp": {},
                                         "80/tcp": {}
                                       },
                                       "Tty": false,
                                       "OpenStdin": false,
                                       "StdinOnce": false,
                                       "Env": [],
                                       "Cmd": null,
                                       "Image": "sha256:2a3b4d813d03ff4c6333f44e431469235ac3372b5923600438514e7c55ffcd99",
                                       "Volumes": null,
                                       "WorkingDir": "/app",
                                       "Entrypoint": ["dotnet", "Defra.Cdp.Deployments.dll"],
                                       "OnBuild": null,
                                       "Labels": {
                                         "defra.cdp.git.repo.url": "https://github.com/${org}/${service}",
                                         "defra.cdp.service.name": "${service}",
                                         "defra.cdp.run_mode": "${runMode}"
                                       }
                                     },
                                     "container": "d8ceb8cc445b3e9292a17b41693b14c7c7e8a9b7dbfe12b7661f63b772539c6f",
                                     "container_config": {
                                       "Hostname": "d8ceb8cc445b",
                                       "Domainname": "",
                                       "User": "",
                                       "AttachStdin": false,
                                       "AttachStdout": false,
                                       "AttachStderr": false,
                                       "ExposedPorts": {
                                         "443/tcp": {},
                                         "80/tcp": {}
                                       },
                                       "Tty": false,
                                       "OpenStdin": false,
                                       "StdinOnce": false,
                                       "Env": [],
                                       "Cmd": [
                                         "/bin/sh",
                                         "-c",
                                         "#(nop) ",
                                         "LABEL defra.cdp.service.name=cdp-deployments"
                                       ],
                                       "Image": "sha256:2a3b4d813d03ff4c6333f44e431469235ac3372b5923600438514e7c55ffcd99",
                                       "Volumes": null,
                                       "WorkingDir": "/app",
                                       "Entrypoint": ["dotnet", "Defra.Cdp.Deployments.dll"],
                                       "OnBuild": null,
                                       "Labels": {
                                         "defra.cdp.git.repo.url": "https://github.com/${org}/${service}",
                                         "defra.cdp.service.name": "${service}",
                                         "defra.cdp.run_mode": "${runMode}"
                                       }
                                     },
                                     "created": "2023-04-11T13:32:11.321678253Z",
                                     "docker_version": "20.10.23",
                                     "history": [],
                                     "os": "linux",
                                     "rootfs": {
                                       "type": "layers",
                                       "diff_ids": []
                                     }
                                   }
                                   """;
        var manifestImage = Deserialize<ManifestImage>(imageAsJson);
        Assert.NotNull(manifestImage);
        Assert.True((new DateTime(2023, 4, 11, 13, 32, 11, 321) - manifestImage.created).Duration() <= TimeSpan.FromMilliseconds(1));
        return Task.CompletedTask;
    }
}