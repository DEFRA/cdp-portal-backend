using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.TenantArtifacts;

class MockClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return Substitute.For<HttpClient>();
    }
}

public class DockerClientTests
{
    private readonly ArtifactScanner _artifactScanner;
    private readonly IDeployablesService _deployableServiceMock = Substitute.For<IDeployablesService>();
    private readonly IDockerClient _dockerClientMock = Substitute.For<IDockerClient>();

    private readonly HttpClient _httpMock = Substitute.For<HttpClient>();
    private readonly ILayerService _layerServiceMock = Substitute.For<ILayerService>();
    private readonly IRepositoryService _repositoryService = Substitute.For<IRepositoryService>();

    public DockerClientTests()
    {
        _artifactScanner = new ArtifactScanner(_deployableServiceMock, _layerServiceMock, _dockerClientMock,
            _repositoryService, ConsoleLogger.CreateLogger<ArtifactScanner>());
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

        var results = ArtifactScanner.FlattenFiles(layers);

        Assert.Single(results);
        Assert.Equal("bbb", results["foo"].LayerSha256);
    }


    [Fact]
    public async void ScanImageShouldSaveAnArtifact()
    {
        // mock manifest
        var cfg = new Blob("", 0, "digest-cfg");
        var files = new Blob("", 0, "digest-files");

        _dockerClientMock
            .LoadManifest("foo", "1.0.0")!
            .Returns(
                Task.FromResult(new Manifest {
                    name = "foo",
                    tag = "1.0.0",
                    digest = "sha256:b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c",
                    config = cfg,
                    layers = new List<Blob> { files }
                })
            );

        // mock reading cfg layer
        _dockerClientMock.SearchLayer("foo", cfg, Arg.Any<List<Regex>>(), Arg.Any<List<Regex>>())
            .Returns(Task.FromResult(new Layer(cfg.digest, new List<LayerFile>())));

        // mock file file layer
        _dockerClientMock.SearchLayer("foo", files, Arg.Any<List<Regex>>(), Arg.Any<List<Regex>>())
            .Returns(Task.FromResult(new Layer(files.digest,
                new List<LayerFile> { new("package-lock.json", "{\"name\": \"foo\"}") })));

        var labels = new Dictionary<string, string>();
        labels["defra.cdp.git.repo.url"] = "https://github.com/foo/foo";
        labels["defra.cdp.service.name"] = "foo";

        _dockerClientMock
            .LoadManifestImage("foo", cfg)!
            .Returns(
                Task.FromResult(new ManifestImage(new ManifestImageConfig(labels, ""),
                    new DateTime().ToLongDateString(), ""))
            );


        var res = await _artifactScanner.ScanImage("foo", "1.0.0", CancellationToken.None);
        Assert.True(res.Success);
        Assert.NotNull(res.Artifact);
        var artifact = res.Artifact;

        Assert.Equal(1, artifact?.ScannerVersion);
        Assert.Equal("foo", artifact?.Repo);
        Assert.Equal("1.0.0", artifact?.Tag);
        Assert.Equal(4294967296, artifact?.SemVer);
        Assert.Equal("https://github.com/foo/foo", artifact?.GithubUrl);
        Assert.Equal("foo", artifact?.ServiceName);
        Assert.Single(artifact!.Files);
        Assert.Equal("sha256:b5bb9d8014a0f9b1d61e21e796d78dccdf1352f23cd32812f4850b878ae4944c", artifact.Sha256);
    }
}
