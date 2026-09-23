using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class GitServiceTests
    {
        // Runner factice : enregistre la requête et simule git en créant le dossier cible.
        private sealed class RecordingRunner : IProcessRunner
        {
            public ProcessRequest? Last { get; private set; }
            public int ExitToReturn { get; set; } = 0;
            public bool CreateDestination { get; set; } = true;

            public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct = default)
                => throw new NotImplementedException();

            public Task<int> RunStreamingAsync(ProcessRequest request, Action<string>? onOutput, CancellationToken ct = default)
            {
                Last = request;
                if (CreateDestination && request.Arguments.Count >= 3)
                    Directory.CreateDirectory(request.Arguments[2]); // simule « git clone » réussi
                return Task.FromResult(ExitToReturn);
            }
        }

        private sealed class FakeLocator : IToolLocator
        {
            public string? GitPath = "git";
            public string? Locate(string toolName, string? configuredPath = null)
                => toolName == "git" ? GitPath : null;
        }

        private static SettingsService SettingsWithWorkspace(TempPlatformPaths paths, string workspace)
        {
            var s = new SettingsService(paths);
            s.Settings.WorkspaceFolder = workspace;
            return s;
        }

        [Fact]
        public async Task Clone_BuildsSeparatedArguments_AndReturnsDestination()
        {
            using var paths = new TempPlatformPaths();
            var ws = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(ws);
            var settings = SettingsWithWorkspace(paths, ws);
            var runner = new RecordingRunner();
            var git = new GitService(settings, runner, new FakeLocator());

            var dest = await git.CloneAsync("https://github.com/u/my-repo.git", null, null);

            Assert.Equal(Path.Combine(ws, "my-repo"), dest);
            Assert.True(Directory.Exists(dest));

            Assert.NotNull(runner.Last);
            Assert.Equal("git", runner.Last!.FileName);
            Assert.Equal(new[] { "clone", "https://github.com/u/my-repo.git", dest }, runner.Last.Arguments);
            Assert.Equal(ws, runner.Last.WorkingDirectory);
        }

        [Fact]
        public async Task Clone_PreservesSpacesInDestination()
        {
            using var paths = new TempPlatformPaths();
            var ws = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(ws);
            var git = new GitService(SettingsWithWorkspace(paths, ws), new RecordingRunner(), new FakeLocator());

            var dest = await git.CloneAsync("https://github.com/u/x", "mon dossier", null);

            Assert.Equal(Path.Combine(ws, "mon dossier"), dest); // espace conservé
        }

        [Fact]
        public async Task Clone_NoWorkspace_Throws()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            settings.Settings.WorkspaceFolder = string.Empty;
            var git = new GitService(settings, new RecordingRunner(), new FakeLocator());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => git.CloneAsync("https://github.com/u/x", null, null));
        }

        [Fact]
        public async Task Clone_DestinationExists_Throws()
        {
            using var paths = new TempPlatformPaths();
            var ws = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(Path.Combine(ws, "deja"));
            var git = new GitService(SettingsWithWorkspace(paths, ws), new RecordingRunner(), new FakeLocator());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => git.CloneAsync("https://github.com/u/deja", "deja", null));
        }

        [Fact]
        public async Task Clone_GitNotFound_Throws()
        {
            using var paths = new TempPlatformPaths();
            var ws = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(ws);
            var git = new GitService(SettingsWithWorkspace(paths, ws), new RecordingRunner(),
                new FakeLocator { GitPath = null });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => git.CloneAsync("https://github.com/u/x", null, null));
        }

        [Theory]
        [InlineData("https://github.com/user/my-repo", "my-repo")]
        [InlineData("https://github.com/user/my-repo.git", "my-repo")]
        [InlineData("https://github.com/user/my-repo/", "my-repo")]
        [InlineData("git@github.com:user/other.git", "other")]
        public void ExtractRepoName_Works(string url, string expected)
        {
            Assert.Equal(expected, GitService.ExtractRepoName(url));
        }
    }
}
