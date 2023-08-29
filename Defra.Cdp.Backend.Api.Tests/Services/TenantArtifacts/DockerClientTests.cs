using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.TenantArtifacts;

public class DockerClientTests
{
    private readonly ArtifactScanner _artifactScanner;
    private readonly IDeployablesService _deployableServiceMock = Substitute.For<IDeployablesService>();
    private readonly IDockerClient _dockerClientMock = Substitute.For<IDockerClient>();

    private readonly HttpClient _httpMock = Substitute.For<HttpClient>();
    private readonly ILayerService _layerServiceMock = Substitute.For<ILayerService>();

    public DockerClientTests()
    {
        _artifactScanner = new ArtifactScanner(_deployableServiceMock, _layerServiceMock, _dockerClientMock,
            ConsoleLogger.CreateLogger<ArtifactScanner>());
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
                Task.FromResult(new Manifest(
                    "foo",
                    "1.0.0",
                    cfg,
                    new List<Blob> { files }))
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


        var res = await _artifactScanner.ScanImage("foo", "1.0.0");
        Assert.Equal(1, res.ScannerVersion);
        Assert.Equal("foo", res.Repo);
        Assert.Equal("1.0.0", res.Tag);
        Assert.Equal(4294967296, res.SemVer);
        Assert.Equal("https://github.com/foo/foo", res.GithubUrl);
        Assert.Equal("foo", res.ServiceName);
        Assert.Single(res.Files);
    }
}