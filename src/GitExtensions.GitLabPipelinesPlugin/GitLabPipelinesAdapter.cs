using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitCommands.Utils;
using GitLabApiClient;
using GitLabApiClient.Models.Pipelines;
using GitLabApiClient.Models.Pipelines.Requests;
using GitLabApiClient.Models.Pipelines.Responses;
using GitLabApiClient.Models.Projects.Responses;
using GitUI;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft.VisualStudio.Threading;

namespace GitExtensions.GitLabPipelinesPlugin
{
    public class GitLabIntegrationMetadataAttribute : BuildServerAdapterMetadataAttribute
    {
        public GitLabIntegrationMetadataAttribute(string buildServerType) : base(buildServerType)
        {
        }

        public override string CanBeLoaded
        {
            get
            {
                if (EnvUtils.IsNet4FullOrHigher())
                {
                    return null;
                }

                return ".Net 4 full framework required";
            }
        }
    }

    [Export(typeof(IBuildServerAdapter))]
    [GitLabIntegrationMetadata(PluginName)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitLabPipelinesAdapter : IBuildServerAdapter
    {
        public const string PluginName = "GitLab Pipelines";
        private IBuildServerWatcher _buildServerWatcher;

        private GitLabClient _gitLabClient;

        private string HostName { get; set; }

        private string ProjectName { get; set; }

        private JoinableTask<IList<Pipeline>> _buildDefinitionsTask;
        private IList<Pipeline> _buildDefinitions;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void Initialize(IBuildServerWatcher buildServerWatcher, ISettingsSource config, Func<ObjectId, bool> isCommitInRevisionGrid = null)
        {
            if (_buildServerWatcher != null)
            {
                throw new InvalidOperationException("Already initialized");
            }

            _buildServerWatcher = buildServerWatcher;

            ProjectName = config.GetString("GitLabProjectName", string.Empty);
            HostName = config.GetString("GitLabBuildServerUrl", string.Empty);
            var token = config.GetString("GitLabToken", string.Empty);

            InitializeGitLabClient(HostName, token);

            _buildDefinitionsTask = ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                _gitLabClient.Pipelines.GetAsync(ProjectName, _ => _.Scope = PipelineScope.All));
        }

        public string UniqueKey => _gitLabClient.HostUrl;

        public IObservable<BuildInfo> GetRunningBuilds(IScheduler scheduler)
        {
            return GetBuilds(scheduler, null, true);
        }

        public IObservable<BuildInfo> GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
        {
            return GetBuilds(scheduler, sinceDate, false);
        }

        private IObservable<BuildInfo> GetBuilds(IScheduler scheduler, DateTime? sinceDate = null, bool? running = null)
        {
            if (_gitLabClient == null)
            {
                return Observable.Empty<BuildInfo>(scheduler);
            }

            return Observable.Create<BuildInfo>((observer, cancellationToken) =>
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    return scheduler.Schedule(() => ObserveBuilds(sinceDate, running, observer, cancellationToken));
                }).Task);
        }

        private async Task ObserveBuilds(DateTime? sinceDate, bool? running, IObserver<BuildInfo> observer, CancellationToken cancellationToken)
        {
            try
            {
                if (_buildDefinitionsTask == null)
                {
                    _buildDefinitionsTask = ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                        _gitLabClient.Pipelines.GetAsync(ProjectName, _ => _.Scope = PipelineScope.All));
                }


                if (_buildDefinitions == null)
                {
                    _buildDefinitions = await _buildDefinitionsTask.JoinAsync();

                    if (_buildDefinitions == null)
                    {
                        observer.OnCompleted();
                        return;
                    }
                }

                if (_buildDefinitions == null)
                {
                    observer.OnCompleted();
                    return;
                }

                Func<Pipeline, bool> predicate = pipeline =>
                    running.HasValue && running.Value
                        ? pipeline.Status == PipelineStatus.Running
                        : pipeline.Status != PipelineStatus.Running;

                if (sinceDate.HasValue)
                {
                    predicate += pipeline => pipeline.CreatedAt >= sinceDate;
                }

                var builds = _buildDefinitions.Where(predicate);

                foreach (var pipeline in builds)
                {
                    var status = pipeline.Status == PipelineStatus.Running
                        ? BuildInfo.BuildStatus.InProgress
                        : ParseBuildStatus(pipeline.Status);
                    var buildInfo = new BuildInfo
                    {
                        Id = pipeline.Id.ToString(),
                        StartDate = pipeline.CreatedAt.Value,
                        Status = status,
                        CommitHashList = new List<ObjectId>
                                    {
                                        ObjectId.Parse(pipeline.Sha)
                                    },
                        Url = pipeline.WebUrl.ToString(),
                        Description = $"#{pipeline.Id} {status:G}"
                    };

                    observer.OnNext(buildInfo);
                }

                _buildDefinitionsTask = null;
                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                // Do nothing, the observer is already stopped
            }
            catch (Exception e)
            {
                observer.OnError(e);
            }
        }

        private BuildInfo.BuildStatus ParseBuildStatus(PipelineStatus pipelineStatus)
        {
            switch (pipelineStatus)
            {
                case PipelineStatus.Created:
                case PipelineStatus.Scheduled:
                case PipelineStatus.Pending:
                case PipelineStatus.Preparing:
                case PipelineStatus.Running:
                    return BuildInfo.BuildStatus.InProgress;
                case PipelineStatus.Success:
                    return BuildInfo.BuildStatus.Success;
                case PipelineStatus.Failed:
                    return BuildInfo.BuildStatus.Failure;
                case PipelineStatus.Canceled:
                    return BuildInfo.BuildStatus.Stopped;
                case PipelineStatus.Skipped:
                    return BuildInfo.BuildStatus.Stopped;
                case PipelineStatus.Manual:
                    return BuildInfo.BuildStatus.Unknown;
                default:
                    return BuildInfo.BuildStatus.Unknown;
            }
        }

        public void InitializeGitLabClient(string serverUrl, string token)
        {
            if (!string.IsNullOrWhiteSpace(serverUrl) && !string.IsNullOrWhiteSpace(token))
            {
                _gitLabClient = new GitLabClient(serverUrl, token);
            }
        }

        public IList<Project> GetProjectsTree()
        {
            IList<Project> projects = new List<Project>();

            var task = Task.Run(async () =>
            {
                projects = await _gitLabClient.Projects.GetAsync();
            });

            task.Wait();

            return projects;
        }
    }
}
