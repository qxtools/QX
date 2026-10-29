using Qx.Game.Api;
using Qx.Game.Messages;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Forums;

internal sealed partial class ForumService : IGameFeature
{
    private readonly IConnection connection;
    private readonly ForumManager forums;
    private readonly RequestBroker requests;
    private readonly GameMessageSender message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ForumEventSource changed;
    private int disposed;

    public ForumService(
        IConnection connection,
        GameState game,
        GameMessageSender message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        forums = game.Forums;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ForumEventSource(observer_error);
        Bindings = Array.AsReadOnly<IApiBinding>(
        [
            new ApiCall<ForumStateRequest, ForumStateView>(
                ForumDescriptors.State,
                (request, token) => ValueTask.FromResult(ReadState(request, token))),
            new ApiCall<ForumListRefreshRequest, ForumListRefreshResult>(
                ForumDescriptors.ListRefresh,
                RefreshList),
            new ApiCall<ForumDetailsRequest, ForumDispatchResult>(
                ForumDescriptors.DetailsRequest,
                RequestDetails),
            new ApiCall<ForumListRequest, ForumDispatchResult>(
                ForumDescriptors.ListRequest,
                RequestList),
            new ApiCall<ForumThreadsRequest, ForumDispatchResult>(
                ForumDescriptors.ThreadsRequest,
                RequestThreads),
            new ApiCall<ForumMessagesRequest, ForumDispatchResult>(
                ForumDescriptors.MessagesRequest,
                RequestMessages),
            new ApiCall<ForumThreadRequest, ForumDispatchResult>(
                ForumDescriptors.ThreadRequest,
                RequestThread),
            new ApiCall<ForumUnreadRequest, ForumDispatchResult>(
                ForumDescriptors.UnreadRequest,
                RequestUnread),
            new ApiCall<ForumThreadsRefreshRequest, ForumThreadsRefreshResult>(
                ForumDescriptors.ThreadsRefresh,
                RefreshThreads),
            new ApiCall<ForumMessagesRefreshRequest, ForumMessagesRefreshResult>(
                ForumDescriptors.MessagesRefresh,
                RefreshMessages),
            new ApiCall<ForumDetailsRefreshRequest, ForumDetailsRefreshResult>(
                ForumDescriptors.DetailsRefresh,
                RefreshDetails),
            new ApiCall<ForumThreadRefreshRequest, ForumThreadRefreshResult>(
                ForumDescriptors.ThreadRefresh,
                RefreshThread),
            new ApiCall<ForumUnreadRefreshRequest, ForumUnreadRefreshResult>(
                ForumDescriptors.UnreadRefresh,
                RefreshUnread),
            new ApiCall<ForumPostActionRequest, ForumDispatchResult>(
                ForumDescriptors.Post,
                Post),
            new ApiCall<ForumThreadModerationRequest, ForumDispatchResult>(
                ForumDescriptors.ThreadModerate,
                ModerateThread),
            new ApiCall<ForumMessageModerationRequest, ForumDispatchResult>(
                ForumDescriptors.MessageModerate,
                ModerateMessage),
            new ApiCall<ForumSettingsUpdateRequest, ForumDispatchResult>(
                ForumDescriptors.SettingsUpdate,
                UpdateSettings),
            new ApiCall<ForumReadMarkersUpdateRequest, ForumDispatchResult>(
                ForumDescriptors.ReadMarkersUpdate,
                UpdateReadMarkers),
            new ApiCall<ForumThreadUpdateRequest, ForumDispatchResult>(
                ForumDescriptors.ThreadUpdate,
                UpdateThread),
            new ApiCall<ForumThreadReportRequest, ForumDispatchResult>(
                ForumDescriptors.ThreadReport,
                ReportThread),
            new ApiCall<ForumMessageReportRequest, ForumDispatchResult>(
                ForumDescriptors.MessageReport,
                ReportMessage),
            new ApiEvent<ForumChanged>(
                ForumDescriptors.Changed,
                changed.Subscribe)
        ]);
        forums.SnapshotChanged += PublishChanged;
    }

    public IReadOnlyList<IApiBinding> Bindings { get; }

    private ForumStateView ReadState(
        ForumStateRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        ValidateSnapshotRevision(request.SnapshotRevision);
        ForumSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        Session? session = lease.Session;
        var result = new ForumStateView(
            session is not null,
            lease.SessionGeneration,
            lease.Revision,
            lease.Snapshot);
        RequireLeaseActive(lease);
        return result;
    }

    private ValueTask<ForumListRefreshResult> RefreshList(
        ForumListRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidatePage(request.StartIndex, request.MaxCount);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            ForumsList page = await requests.RequestAsync(
                MessageContracts.Forums.ListRequest,
                new GetForumsList(request.ListCode, request.StartIndex, request.MaxCount),
                MessageContracts.Forums.List,
                scope.Session,
                match: value =>
                    value.ListCode == request.ListCode &&
                    value.StartIndex == request.StartIndex &&
                    ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumListRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                page);
        });

    private ValueTask<ForumDispatchResult> RequestDetails(
        ForumDetailsRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.StatsRequest,
            new GetForumStats(request.GroupId),
            cancellation_token,
            request.GroupId);

    private ValueTask<ForumDispatchResult> RequestList(
        ForumListRequest request,
        CancellationToken cancellation_token)
    {
        ValidatePage(request.StartIndex, request.MaxCount);
        return Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ListRequest,
            new GetForumsList(request.ListCode, request.StartIndex, request.MaxCount),
            cancellation_token);
    }

    private ValueTask<ForumDispatchResult> RequestThreads(
        ForumThreadsRequest request,
        CancellationToken cancellation_token)
    {
        ValidatePage(request.StartIndex, request.MaxCount);
        return Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ThreadsRequest,
            new GetForumThreads(request.GroupId, request.StartIndex, request.MaxCount),
            cancellation_token,
            request.GroupId);
    }

    private ValueTask<ForumDispatchResult> RequestMessages(
        ForumMessagesRequest request,
        CancellationToken cancellation_token)
    {
        ValidatePage(request.StartIndex, request.MaxCount);
        return Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.MessagesRequest,
            new GetForumThreadMessages(
                request.GroupId,
                request.ThreadId,
                request.StartIndex,
                request.MaxCount),
            cancellation_token,
            request.GroupId,
            request.ThreadId);
    }

    private ValueTask<ForumDispatchResult> RequestThread(
        ForumThreadRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ThreadRequest,
            new GetForumThread(request.GroupId, request.ThreadId),
            cancellation_token,
            request.GroupId,
            request.ThreadId);

    private ValueTask<ForumDispatchResult> RequestUnread(
        ForumUnreadRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.UnreadCountRequest,
            new GetUnreadForumsCount(),
            cancellation_token);

    private ValueTask<ForumThreadsRefreshResult> RefreshThreads(
        ForumThreadsRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidatePage(request.StartIndex, request.MaxCount);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            ValidateIds(request.GroupId);
            ForumThreads page = await requests.RequestAsync(
                MessageContracts.Forums.ThreadsRequest,
                new GetForumThreads(request.GroupId, request.StartIndex, request.MaxCount),
                MessageContracts.Forums.Threads,
                scope.Session,
                match: value =>
                    value.GroupId == request.GroupId &&
                    value.StartIndex == request.StartIndex &&
                    ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumThreadsRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                page);
        });

    private ValueTask<ForumMessagesRefreshResult> RefreshMessages(
        ForumMessagesRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidatePage(request.StartIndex, request.MaxCount);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            ValidateIds(request.GroupId, request.ThreadId);
            ThreadMessages page = await requests.RequestAsync(
                MessageContracts.Forums.MessagesRequest,
                new GetForumThreadMessages(
                    request.GroupId,
                    request.ThreadId,
                    request.StartIndex,
                    request.MaxCount),
                MessageContracts.Forums.Messages,
                scope.Session,
                match: value =>
                    value.GroupId == request.GroupId &&
                    value.ThreadId == request.ThreadId &&
                    value.StartIndex == request.StartIndex &&
                    ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumMessagesRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                page);
        });

    private ValueTask<ForumDetailsRefreshResult> RefreshDetails(
        ForumDetailsRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            ValidateIds(request.GroupId);
            ForumData response = await requests.RequestAsync(
                MessageContracts.Forums.StatsRequest,
                new GetForumStats(request.GroupId),
                MessageContracts.Forums.Stats,
                scope.Session,
                match: value => value.Data.GroupId == request.GroupId && ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumDetailsRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                response.Data);
        });

    private ValueTask<ForumThreadRefreshResult> RefreshThread(
        ForumThreadRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            ValidateIds(request.GroupId, request.ThreadId);
            UpdateThread response = await requests.RequestAsync(
                MessageContracts.Forums.ThreadRequest,
                new GetForumThread(request.GroupId, request.ThreadId),
                MessageContracts.Forums.ThreadUpdated,
                scope.Session,
                match: value =>
                    value.GroupId == request.GroupId &&
                    value.Thread?.ThreadId == request.ThreadId &&
                    ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumThreadRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                response.Thread ?? throw new InvalidDataException("Forum thread response is empty."));
        });

    private ValueTask<ForumUnreadRefreshResult> RefreshUnread(
        ForumUnreadRefreshRequest request,
        CancellationToken cancellation_token) =>
        Invoke(cancellation_token, async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateTimeout(request.TimeoutMilliseconds);
            ForumScope scope = CaptureScope(request.ExpectedSessionGeneration, token);
            UnreadForumsCount response = await requests.RequestAsync(
                MessageContracts.Forums.UnreadCountRequest,
                new GetUnreadForumsCount(),
                MessageContracts.Forums.UnreadCount,
                scope.Session,
                match: _ => ScopeActive(scope),
                timeout_ms: request.TimeoutMilliseconds,
                block: false,
                cancellation_token: token,
                max_attempts: 1,
                dispatch_guard: () => RequireScope(scope)).ConfigureAwait(false);
            RequireScope(scope);
            return new ForumUnreadRefreshResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                response.Count);
        });

    private ValueTask<ForumDispatchResult> Post(
        ForumPostActionRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.Post,
            new PostMessage(request.GroupId, request.ThreadId, request.Subject, request.MessageText),
            cancellation_token,
            request.GroupId,
            request.ThreadId);

    private ValueTask<ForumDispatchResult> ModerateThread(
        ForumThreadModerationRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ThreadModerate,
            new ModerateForumThread(request.GroupId, request.ThreadId, request.State),
            cancellation_token,
            request.GroupId,
            request.ThreadId);

    private ValueTask<ForumDispatchResult> ModerateMessage(
        ForumMessageModerationRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.MessageModerate,
            new ModerateForumMessage(
                request.GroupId,
                request.ThreadId,
                request.MessageId,
                request.State),
            cancellation_token,
            request.GroupId,
            request.ThreadId,
            request.MessageId);

    private ValueTask<ForumDispatchResult> UpdateSettings(
        ForumSettingsUpdateRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.SettingsUpdate,
            new UpdateForumSettings(
                request.GroupId,
                request.ReadLevel,
                request.PostMessageLevel,
                request.PostThreadLevel,
                request.ModerateLevel),
            cancellation_token,
            request.GroupId);

    private ValueTask<ForumDispatchResult> UpdateReadMarkers(
        ForumReadMarkersUpdateRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ReadMarkersUpdate,
            new UpdateForumReadMarkers(request.Markers),
            cancellation_token);

    private ValueTask<ForumDispatchResult> UpdateThread(
        ForumThreadUpdateRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ThreadUpdate,
            new UpdateThread(
                request.GroupId,
                request.ThreadId,
                request.IsSticky,
                request.IsLocked),
            cancellation_token,
            request.GroupId,
            request.ThreadId);

    private ValueTask<ForumDispatchResult> ReportThread(
        ForumThreadReportRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.ThreadReport,
            new CallForHelpFromForumThread(
                request.GroupId,
                request.ThreadId,
                request.CategoryId,
                request.Report,
                request.FirstContext,
                request.SecondContext),
            cancellation_token,
            request.GroupId,
            request.ThreadId);

    private ValueTask<ForumDispatchResult> ReportMessage(
        ForumMessageReportRequest request,
        CancellationToken cancellation_token) =>
        Dispatch(
            request,
            request.ExpectedSessionGeneration,
            MessageContracts.Forums.MessageReport,
            new CallForHelpFromForumMessage(
                request.GroupId,
                request.ThreadId,
                request.MessageId,
                request.CategoryId,
                request.Report,
                request.FirstContext,
                request.SecondContext),
            cancellation_token,
            request.GroupId,
            request.ThreadId,
            request.MessageId);

    private ValueTask<ForumDispatchResult> Dispatch<TRequest, TMessage>(
        TRequest request,
        long? expected_generation,
        MessageContract<TMessage> contract,
        TMessage message,
        CancellationToken cancellation_token,
        params Id[] ids)
        where TRequest : class
        where TMessage : IParserComposer<TMessage> =>
        Invoke(cancellation_token, token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ForumScope scope = CaptureScope(expected_generation, token);
            ValidateIds(ids);
            message_dispatcher.Dispatch(
                contract,
                message,
                scope.Session,
                token,
                () => RequireScope(scope));
            RequireScope(scope);
            return ValueTask.FromResult(new ForumDispatchResult(
                scope.Generation,
                time_provider.GetUtcNow(),
                1));
        });

    private async ValueTask<TResult> Invoke<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            lifetime.Token);
        try
        {
            TResult result = await invocation(linked.Token).ConfigureAwait(false);
            cancellation_token.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            return result;
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(ForumService));
        }
    }

    private ForumScope CaptureScope(
        long? expected_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(forums.Session, session))
            throw new InvalidOperationException("The forum state is not bound to the active session.");
        long generation = forums.SessionGeneration;
        if (expected_generation is long expected && expected != generation)
            throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
        return new ForumScope(session, generation);
    }

    private bool ScopeActive(ForumScope scope) =>
        Volatile.Read(ref disposed) == 0 &&
        ReferenceEquals(connection.Session, scope.Session) &&
        ReferenceEquals(forums.Session, scope.Session) &&
        forums.SessionGeneration == scope.Generation;

    private void RequireScope(ForumScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the forum operation.");
    }

    private void PublishChanged(ForumSnapshot snapshot)
    {
        if (Volatile.Read(ref disposed) != 0)
            return;
        Session? session = connection.Session;
        changed.Publish(new ForumChanged(
            time_provider.GetUtcNow(),
            session is not null,
            forums.SessionGeneration,
            snapshot));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        forums.SnapshotChanged -= PublishChanged;
        lifetime.Cancel();
        ClearLeases();
        changed.Dispose();
        lifetime.Dispose();
    }

    private static void ValidatePage(int start_index, int max_count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start_index);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max_count);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateSnapshotRevision(long? snapshot_revision)
    {
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
    }

    private static void ValidateIds(params Id[] ids)
    {
        for (int index = 0; index < ids.Length; index++)
        {
            long value = ids[index];
            if (value is < int.MinValue or > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ids));
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct ForumScope(Session Session, long Generation);

    private sealed class ForumEventSource(Action<Exception>? observer_error) : IDisposable
    {
        private readonly object sync = new();
        private Action<ForumChanged>? listeners;
        private bool disposed;

        public IDisposable Subscribe(Action<ForumChanged> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                listeners += listener;
            }
            return new ForumSubscription(this, listener);
        }

        public void Publish(ForumChanged value)
        {
            Action<ForumChanged>? snapshot;
            lock (sync)
                snapshot = disposed ? null : listeners;
            if (snapshot is null)
                return;
            foreach (Action<ForumChanged> listener in snapshot.GetInvocationList().Cast<Action<ForumChanged>>())
            {
                try
                {
                    listener(value);
                }
                catch (Exception error)
                {
                    observer_error?.Invoke(error);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                listeners = null;
            }
        }

        private void Unsubscribe(Action<ForumChanged> listener)
        {
            lock (sync)
                listeners -= listener;
        }

        private sealed class ForumSubscription(
            ForumEventSource source,
            Action<ForumChanged> listener) : IDisposable
        {
            private ForumEventSource? current_source = source;
            private Action<ForumChanged>? current_listener = listener;

            public void Dispose()
            {
                ForumEventSource? source_value = Interlocked.Exchange(ref current_source, null);
                Action<ForumChanged>? listener_value = Interlocked.Exchange(ref current_listener, null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
