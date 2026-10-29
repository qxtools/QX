using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Game.Messages;
using Qx.Model.Messages.Outgoing;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Forums;

internal static class ForumDescriptors
{
    private static readonly ApiExposure dispatch_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static readonly ApiDescriptor State = new(
        ApiIds.ForumsState,
        "Forum state",
        "Reads the immutable forum cache for the active or disconnected session.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(ForumStateRequest),
        typeof(ForumStateView),
        [SnapshotRevisionParameter()],
        stateEffects: [ReadEffect()],
        toolHints: QueryHints());

    public static readonly ApiDescriptor ListRefresh = Refresh(
        ApiIds.ForumsListRefresh,
        "Refresh forum list",
        "Requests a page of the forum directory and waits for it.",
        typeof(ForumListRefreshRequest),
        typeof(ForumListRefreshResult),
        MessageKeys.Forums.ListRequest,
        MessageKeys.Forums.List,
        ListParameters());

    public static readonly ApiDescriptor DetailsRequest = Request(
        ApiIds.ForumDetailsRequest,
        "Request forum details",
        typeof(ForumDetailsRequest),
        MessageKeys.Forums.StatsRequest,
        [GroupParameter(), GenerationParameter()]);

    public static readonly ApiDescriptor ListRequest = Request(
        ApiIds.ForumsListRequest,
        "Request forum list",
        typeof(ForumListRequest),
        MessageKeys.Forums.ListRequest,
        [.. ListParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApiDescriptor ThreadsRequest = Request(
        ApiIds.ForumThreadsRequest,
        "Request forum threads",
        typeof(ForumThreadsRequest),
        MessageKeys.Forums.ThreadsRequest,
        [.. ThreadPageParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApiDescriptor MessagesRequest = Request(
        ApiIds.ForumMessagesRequest,
        "Request forum messages",
        typeof(ForumMessagesRequest),
        MessageKeys.Forums.MessagesRequest,
        [.. MessagePageParameters().Where(parameter => parameter.Name != "timeout_milliseconds")]);

    public static readonly ApiDescriptor ThreadRequest = Request(
        ApiIds.ForumThreadRequest,
        "Request forum thread",
        typeof(ForumThreadRequest),
        MessageKeys.Forums.ThreadRequest,
        [GroupParameter(), ThreadParameter(), GenerationParameter()]);

    public static readonly ApiDescriptor UnreadRequest = Request(
        ApiIds.ForumsUnreadRequest,
        "Request unread forum count",
        typeof(ForumUnreadRequest),
        MessageKeys.Forums.UnreadCountRequest,
        [GenerationParameter()]);

    public static readonly ApiDescriptor ThreadsRefresh = Refresh(
        ApiIds.ForumThreadsRefresh,
        "Refresh forum threads",
        "Requests a page of forum threads and waits for it.",
        typeof(ForumThreadsRefreshRequest),
        typeof(ForumThreadsRefreshResult),
        MessageKeys.Forums.ThreadsRequest,
        MessageKeys.Forums.Threads,
        ThreadPageParameters());

    public static readonly ApiDescriptor MessagesRefresh = Refresh(
        ApiIds.ForumMessagesRefresh,
        "Refresh forum messages",
        "Requests a page of thread messages and waits for it.",
        typeof(ForumMessagesRefreshRequest),
        typeof(ForumMessagesRefreshResult),
        MessageKeys.Forums.MessagesRequest,
        MessageKeys.Forums.Messages,
        MessagePageParameters());

    public static readonly ApiDescriptor DetailsRefresh = Refresh(
        ApiIds.ForumDetailsRefresh,
        "Refresh forum details",
        "Requests the details of a group forum and waits for them.",
        typeof(ForumDetailsRefreshRequest),
        typeof(ForumDetailsRefreshResult),
        MessageKeys.Forums.StatsRequest,
        MessageKeys.Forums.Stats,
        [GroupParameter(), TimeoutParameter(), GenerationParameter()]);

    public static readonly ApiDescriptor ThreadRefresh = Refresh(
        ApiIds.ForumThreadRefresh,
        "Refresh forum thread",
        "Requests a single forum thread and waits for it.",
        typeof(ForumThreadRefreshRequest),
        typeof(ForumThreadRefreshResult),
        MessageKeys.Forums.ThreadRequest,
        MessageKeys.Forums.ThreadUpdated,
        [GroupParameter(), ThreadParameter(), TimeoutParameter(), GenerationParameter()]);

    public static readonly ApiDescriptor UnreadRefresh = Refresh(
        ApiIds.ForumsUnreadRefresh,
        "Refresh unread forum count",
        "Requests the number of forums with unread messages and waits for it.",
        typeof(ForumUnreadRefreshRequest),
        typeof(ForumUnreadRefreshResult),
        MessageKeys.Forums.UnreadCountRequest,
        MessageKeys.Forums.UnreadCount,
        [TimeoutParameter(), GenerationParameter()]);

    public static readonly ApiDescriptor Post = Action(
        ApiIds.ForumsPost,
        "Post forum message",
        "Posts a forum message or starts a new thread.",
        typeof(ForumPostActionRequest),
        MessageKeys.Forums.Post,
        [
            GroupParameter(),
            ThreadParameter(),
            StringParameter("subject", "Forum thread subject."),
            StringParameter("message_text", "Forum post body."),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor ThreadModerate = Action(
        ApiIds.ForumThreadModerate,
        "Moderate forum thread",
        "Hides or restores a forum thread.",
        typeof(ForumThreadModerationRequest),
        MessageKeys.Forums.ThreadModerate,
        [GroupParameter(), ThreadParameter(), StateParameter(), GenerationParameter()],
        true);

    public static readonly ApiDescriptor MessageModerate = Action(
        ApiIds.ForumMessageModerate,
        "Moderate forum message",
        "Hides or restores a forum message.",
        typeof(ForumMessageModerationRequest),
        MessageKeys.Forums.MessageModerate,
        [
            GroupParameter(),
            ThreadParameter(),
            MessageParameter(),
            StateParameter(),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor SettingsUpdate = Action(
        ApiIds.ForumSettingsUpdate,
        "Update forum settings",
        "Changes the permission levels of a group forum.",
        typeof(ForumSettingsUpdateRequest),
        MessageKeys.Forums.SettingsUpdate,
        [
            GroupParameter(),
            IntegerParameter("read_level", "Forum read permission level."),
            IntegerParameter("post_message_level", "Forum reply permission level."),
            IntegerParameter("post_thread_level", "Forum thread permission level."),
            IntegerParameter("moderate_level", "Forum moderation permission level."),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor ReadMarkersUpdate = Action(
        ApiIds.ForumReadMarkersUpdate,
        "Update forum read markers",
        "Updates the read markers of group forums.",
        typeof(ForumReadMarkersUpdateRequest),
        MessageKeys.Forums.ReadMarkersUpdate,
        [
            new("markers", typeof(IReadOnlyList<ForumReadMarker>), true, null,
                "Forum read markers.", new(MaxItems: ushort.MaxValue)),
            GenerationParameter()
        ],
        false);

    public static readonly ApiDescriptor ThreadUpdate = Action(
        ApiIds.ForumThreadUpdate,
        "Update forum thread",
        "Changes the sticky and locked flags of a forum thread.",
        typeof(ForumThreadUpdateRequest),
        MessageKeys.Forums.ThreadUpdate,
        [
            GroupParameter(),
            ThreadParameter(),
            new("is_sticky", typeof(bool), true, null, "Whether the thread is sticky."),
            new("is_locked", typeof(bool), true, null, "Whether the thread is locked."),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor ThreadReport = Action(
        ApiIds.ForumThreadReport,
        "Report forum thread",
        "Reports a forum thread to the moderators.",
        typeof(ForumThreadReportRequest),
        MessageKeys.Forums.ThreadReport,
        [
            GroupParameter(),
            ThreadParameter(),
            IntegerParameter("category_id", "Report category identifier."),
            StringParameter("report", "Report text."),
            OptionalStringParameter("first_context", "Flash report context."),
            OptionalStringParameter("second_context", "Flash report context."),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor MessageReport = Action(
        ApiIds.ForumMessageReport,
        "Report forum message",
        "Reports a forum message to the moderators.",
        typeof(ForumMessageReportRequest),
        MessageKeys.Forums.MessageReport,
        [
            GroupParameter(),
            ThreadParameter(),
            MessageParameter(),
            IntegerParameter("category_id", "Report category identifier."),
            StringParameter("report", "Report text."),
            OptionalStringParameter("first_context", "Flash report context."),
            OptionalStringParameter("second_context", "Flash report context."),
            GenerationParameter()
        ],
        true);

    public static readonly ApiDescriptor Changed = new(
        ApiIds.ForumsChanged,
        "Forum state changed",
        "Publishes immutable forum snapshots after accepted route messages and resets.",
        ApiMemberKind.Event,
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting,
        null,
        typeof(ForumChanged),
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints());

    private static ApiDescriptor Refresh(
        string id,
        string title,
        string summary,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        Type result_type,
        MessageKey request,
        MessageKey response,
        IReadOnlyList<ApiParameter> parameters) => new(
            id,
            title,
            summary + " Sends one session-pinned request and accepts only the matching forum response.",
            ApiMemberKind.Operation,
            ApiExposure.All,
            request_type,
            result_type,
            parameters,
            [GameRequirement.HotelConnected],
            [ChangeEffect()],
            [Send(request), Observe(response)],
            RefreshHints());

    private static ApiDescriptor Action(
        string id,
        string title,
        string summary,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        MessageKey request,
        IReadOnlyList<ApiParameter> parameters,
        bool destructive) => new(
            id,
            title,
            summary + " Dispatches exactly one forum action in the pinned active session.",
            ApiMemberKind.Operation,
            ApiExposure.All,
        request_type,
        typeof(ForumDispatchResult),
        parameters,
        requiredStates: [GameRequirement.HotelConnected],
            stateEffects: [ChangeEffect()],
            messages: [Send(request)],
            toolHints: new(false, destructive, false, true));

    private static ApiDescriptor Request(
        string id,
        string title,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        MessageKey request,
        IReadOnlyList<ApiParameter> parameters) => new(
            id,
            title,
            "Dispatches exactly one forum read request and returns immediately.",
            ApiMemberKind.Operation,
            dispatch_exposure,
        request_type,
        typeof(ForumDispatchResult),
        parameters,
        requiredStates: [GameRequirement.HotelConnected],
            stateEffects: [ReadEffect()],
            messages: [Send(request)],
            toolHints: new(true, false, false, true));

    private static IReadOnlyList<ApiParameter> ListParameters() =>
    [
        new("list_code", typeof(ForumListCode), true, null, "Hotel forum directory."),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static IReadOnlyList<ApiParameter> ThreadPageParameters() =>
    [
        GroupParameter(),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static IReadOnlyList<ApiParameter> MessagePageParameters() =>
    [
        GroupParameter(),
        ThreadParameter(),
        OffsetParameter("start_index"),
        CountParameter(),
        TimeoutParameter(),
        GenerationParameter()
    ];

    private static ApiParameter GroupParameter() =>
        new("group_id", typeof(Id), true, null, "Forum-owning group identifier.");

    private static ApiParameter ThreadParameter() =>
        new("thread_id", typeof(Id), true, null, "Forum thread identifier.");

    private static ApiParameter MessageParameter() =>
        new("message_id", typeof(Id), true, null, "Forum message identifier.");

    private static ApiParameter StateParameter() =>
        IntegerParameter("state", "Forum moderation state.");

    private static ApiParameter IntegerParameter(
        string name,
        string description) =>
        new(name, typeof(int), true, null, description);

    private static ApiParameter StringParameter(
        string name,
        string description) =>
        new(name, typeof(string), true, null, description,
            new(MinLength: 0, MaxLength: ushort.MaxValue));

    private static ApiParameter OptionalStringParameter(
        string name,
        string description) =>
        new(name, typeof(string), false, "", description,
            new(MinLength: 0, MaxLength: ushort.MaxValue));

    private static ApiParameter OffsetParameter(string name) =>
        new(name, typeof(int), false, 0, "Zero-based hotel page start.", new(Minimum: 0));

    private static ApiParameter CountParameter() =>
        new("max_count", typeof(int), false, 20, "Maximum hotel page entries.", new(Minimum: 1));

    private static ApiParameter TimeoutParameter() =>
        new("timeout_milliseconds", typeof(int), false, 10000, "Caller wait budget.",
            new(Minimum: 1, Maximum: 120000));

    private static ApiParameter GenerationParameter() =>
        new("expected_session_generation", typeof(long?), false, null,
            "Optional active hotel-session generation.", new(Minimum: 1));

    private static ApiParameter SnapshotRevisionParameter() =>
        new("snapshot_revision", typeof(long?), false, null,
            "Optional immutable forum snapshot revision.", new(Minimum: 1));

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Forums.Stats, false),
        Observe(MessageKeys.Forums.List, false),
        Observe(MessageKeys.Forums.Threads, false),
        Observe(MessageKeys.Forums.Messages, false),
        Observe(MessageKeys.Forums.ThreadCreated, false),
        Observe(MessageKeys.Forums.MessageCreated, false),
        Observe(MessageKeys.Forums.ThreadUpdated, false),
        Observe(MessageKeys.Forums.MessageUpdated, false),
        Observe(MessageKeys.Forums.UnreadCount, false)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.Forums, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.Forums, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);

    private static ApiToolHints RefreshHints() => new(true, false, true, true);
}
