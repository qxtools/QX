using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Profiles;

internal static class ProfileDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = Query<ProfileStateRequest, ProfileStateView>(
        ApiIds.ProfileState,
        "Profile state",
        "Reads the active local account snapshot and its loaded-state summary.",
        [],
        [new(GameRequirement.ProfileLoaded, ApiStateChange.Reads)]);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.ProfileRefresh,
        "Refresh profile",
        "Loads the active session's local account profile.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(ProfileRefreshRequest),
        typeof(ProfileStateView),
        [TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.ProfileLoaded, ApiStateChange.Changes)],
        [
            Send(MessageKeys.Users.ProfileRequest),
            Observe(MessageKeys.Users.ProfileSnapshot)
        ],
        new(true, false, true, true));

    public static ApiDescriptor BlocksList { get; } = Query<ProfileIdPageRequest, ProfileIdPage>(
        ApiIds.ProfileBlocksList,
        "Blocked users",
        "Reads a bounded page from the local account's block-list snapshot.",
        PagingParameters(200),
        [new(GameRequirement.ProfileBlockListLoaded, ApiStateChange.Reads)]);

    public static ApiDescriptor BlocksRefresh { get; } = RefreshIds(
        ApiIds.ProfileBlocksRefresh,
        "Refresh blocked users",
        "Loads the active session's complete block list and returns a bounded page.",
        MessageKeys.Users.Block.ListRequest,
        MessageKeys.Users.Block.ListSnapshot,
        GameRequirement.ProfileBlockListLoaded);

    public static ApiDescriptor BlockAdd { get; } = UserOperation(
        ApiIds.ProfileBlockAdd,
        "Block user",
        "Adds a user to the local account's block list.",
        MessageKeys.Users.Block.Add,
        GameRequirement.ProfileBlockListLoaded);

    public static ApiDescriptor BlockRemove { get; } = UserOperation(
        ApiIds.ProfileBlockRemove,
        "Unblock user",
        "Removes a user from the local account's block list.",
        MessageKeys.Users.Block.Remove,
        GameRequirement.ProfileBlockListLoaded);

    public static ApiDescriptor IgnoresList { get; } = Query<ProfileIdPageRequest, ProfileIdPage>(
        ApiIds.ProfileIgnoresList,
        "Ignored users",
        "Reads a bounded page from the local account's ignore-list snapshot.",
        PagingParameters(200),
        [new(GameRequirement.ProfileIgnoreListLoaded, ApiStateChange.Reads)]);

    public static ApiDescriptor IgnoresRefresh { get; } = RefreshIds(
        ApiIds.ProfileIgnoresRefresh,
        "Refresh ignored users",
        "Loads the active session's complete ignore list and returns a bounded page.",
        MessageKeys.Users.Ignore.ListRequest,
        MessageKeys.Users.Ignore.ListSnapshot,
        GameRequirement.ProfileIgnoreListLoaded);

    public static ApiDescriptor IgnoreAddById { get; } = UserOperation(
        ApiIds.ProfileIgnoreAddById,
        "Ignore user by id",
        "Adds a user identifier to the local account's ignore list.",
        MessageKeys.Users.Ignore.AddByIdRequest,
        GameRequirement.ProfileIgnoreListLoaded);

    public static ApiDescriptor IgnoreRemove { get; } = new(
        ApiIds.ProfileIgnoreRemove,
        "Unignore user",
        "Removes exactly one user identifier or user name through a verified dialect route.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(ProfileIgnoreRemoveRequest),
        typeof(ProfileDispatchResult),
        [
            new("kind", typeof(ProfileIdentityKind), true, null, "Identity representation carried by the request."),
            RequiredText("identity", "Positive decimal user identifier or hotel user name selected by kind.")
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.ProfileIgnoreListLoaded, ApiStateChange.Changes)],
        [Send(MessageKeys.Users.Ignore.Remove)],
        new(false, true, false, true));

    public static ApiDescriptor FigureSetsList { get; } = Query<ProfileFigureSetsRequest, ProfileFigureSetsPage>(
        ApiIds.ProfileFigureSetsList,
        "Owned figure sets",
        "Reads bounded pages of owned figure sets and Flash-bound furniture names.",
        PagingParameters(200),
        [new(GameRequirement.ProfileFigureSetsLoaded, ApiStateChange.Reads)]);

    public static ApiDescriptor SanctionsList { get; } = Query<ProfileSanctionsRequest, ProfileSanctionsPage>(
        ApiIds.ProfileSanctionsList,
        "Account sanctions",
        "Reads a bounded sanction page.",
        PagingParameters(100),
        [new(GameRequirement.ProfileSanctionsLoaded, ApiStateChange.Reads)]);

    public static ApiDescriptor SanctionsRefresh { get; } = new(
        ApiIds.ProfileSanctionsRefresh,
        "Refresh account sanctions",
        "Loads the active dialect's account-sanction status and returns a bounded view.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(ProfileSanctionsRefreshRequest),
        typeof(ProfileSanctionsPage),
        [.. PagingParameters(100), TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.ProfileSanctionsLoaded, ApiStateChange.Changes)],
        [
            Send(MessageKeys.Users.Sanctions.Request),
            Observe(MessageKeys.Users.Sanctions.Snapshot)
        ],
        new(true, false, true, true));

    public static ApiDescriptor WardrobeGet { get; } = new(
        ApiIds.ProfileWardrobeGet,
        "Wardrobe snapshot",
        "Creates one immutable session-bound wardrobe snapshot at offset zero, then returns bounded pages from that snapshot revision.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(ProfileWardrobeRequest),
        typeof(ProfileWardrobePage),
        [
            .. PagingParameters(100),
            TimeoutParameter(),
            new(
                "snapshot_revision",
                typeof(long?),
                false,
                null,
                "Snapshot revision returned by the first page; required for every continuation page.",
                new(Minimum: 1))
        ],
        [GameRequirement.HotelConnected],
        messages:
        [
            Send(MessageKeys.Wardrobe.Request),
            Observe(MessageKeys.Wardrobe.Snapshot)
        ],
        toolHints: new(true, false, true, true));

    public static ApiDescriptor MottoSet { get; } = Dispatch<ProfileMottoSetRequest>(
        ApiIds.ProfileMottoSet,
        "Set motto",
        "Changes the local account's motto.",
        [new("motto", typeof(string), true, null, "New motto.", TextConstraints(true))],
        MessageKeys.Users.MottoUpdate,
        GameRequirement.ProfileLoaded);

    public static ApiDescriptor FigureSet { get; } = Dispatch<ProfileFigureSetRequest>(
        ApiIds.ProfileFigureSet,
        "Set figure",
        "Changes the local account's figure and gender through the active dialect projection.",
        [
            RequiredText("gender", "Gender code."),
            RequiredText("figure", "Figure string.")
        ],
        MessageKeys.Wardrobe.FigureUpdate,
        GameRequirement.ProfileLoaded);

    public static ApiDescriptor OutfitSave { get; } = Dispatch<ProfileOutfitSaveRequest>(
        ApiIds.ProfileWardrobeOutfitSave,
        "Save wardrobe outfit",
        "Saves a figure and gender into a wardrobe slot.",
        [
            new("slot_id", typeof(int), true, null, "Wardrobe slot identifier.", new(Minimum: 0)),
            RequiredText("figure", "Figure string."),
            RequiredText("gender", "Gender code.")
        ],
        MessageKeys.Wardrobe.OutfitSave);

    public static ApiDescriptor FavoriteGroupSelect { get; } = Dispatch<ProfileFavoriteGroupRequest>(
        ApiIds.ProfileFavoriteGroupSelect,
        "Select favorite group",
        "Selects the local account's favorite group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Users.FavoriteGroup.Select);

    public static ApiDescriptor FavoriteGroupDeselect { get; } = Dispatch<ProfileFavoriteGroupRequest>(
        ApiIds.ProfileFavoriteGroupDeselect,
        "Deselect favorite group",
        "Deselects the local account's favorite group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Users.FavoriteGroup.Deselect);

    public static ApiDescriptor Changed { get; } = Event<ProfileChanged>(
        ApiIds.ProfileChanged,
        "Profile changed",
        "Publishes immutable local profile, list, figure-set, sanction and reset summaries.",
        [
            Observe(MessageKeys.Users.ProfileSnapshot, false),
            Observe(MessageKeys.Users.Block.ListSnapshot, false),
            Observe(MessageKeys.Users.Block.Updated, false),
            Observe(MessageKeys.Users.Ignore.ListSnapshot, false),
            Observe(MessageKeys.Users.Ignore.Updated, false),
            Observe(MessageKeys.Users.FigureSets.Added, false),
            Observe(MessageKeys.Users.FigureSets.Removed, false),
            Observe(MessageKeys.Users.FigureSets.Snapshot, false),
            Observe(MessageKeys.Users.Sanctions.Snapshot, false),
            Observe(MessageKeys.Users.FigureUpdated, false),
            Observe(MessageKeys.Users.NameChangeResult, false),
            Observe(MessageKeys.Users.SafetyLockChanged, false),
            Observe(MessageKeys.Room.Occupants.Identity.Appearance, false),
            Observe(MessageKeys.Room.Occupants.Identity.Name, false)
        ]);

    public static ApiDescriptor BlockUpdated { get; } = Event<ProfileBlockUpdated>(
        ApiIds.ProfileBlockUpdated,
        "Block result",
        "Publishes hotel block and unblock results with the committed profile revision.",
        [Observe(MessageKeys.Users.Block.Updated)]);

    public static ApiDescriptor IgnoreUpdated { get; } = Event<ProfileIgnoreUpdated>(
        ApiIds.ProfileIgnoreUpdated,
        "Ignore result",
        "Publishes hotel ignore results with the committed profile revision.",
        [Observe(MessageKeys.Users.Ignore.Updated)]);

    private static ApiDescriptor Query<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        IReadOnlyList<ApiStateEffect> effects) => new(
            id,
            title,
            description,
            ApiMemberKind.Query,
            ApiExposure.All,
            typeof(TRequest),
            typeof(TResult),
            parameters,
            stateEffects: effects,
            toolHints: new(true, false, true, false),
            invocationScope: ApiLifetime.Persistent);

    private static ApiDescriptor RefreshIds(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key,
        GameRequirement state) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(ProfileIdRefreshRequest),
            typeof(ProfileIdPage),
            [.. PagingParameters(200), TimeoutParameter()],
            [GameRequirement.HotelConnected],
            [new(state, ApiStateChange.Changes)],
            [Send(request_key), Observe(snapshot_key)],
            new(true, false, true, true));

    private static ApiDescriptor UserOperation(
        string id,
        string title,
        string description,
        MessageKey key,
        GameRequirement state) => Dispatch<ProfileUserRequest>(
            id,
            title,
            description,
            [RequiredId("user_id", "User identifier.")],
            key,
            state);

    private static ApiDescriptor Dispatch<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey key,
        GameRequirement? state = null) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(ProfileDispatchResult),
            parameters,
            [GameRequirement.HotelConnected],
            state is GameRequirement changed
                ? [new(changed, ApiStateChange.Changes)]
                : [],
            [Send(key)],
            new(false, true, false, true));

    private static ApiDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiMessageRequirement> messages) => new(
            id,
            title,
            description,
            ApiMemberKind.Event,
            event_exposure,
            null,
            typeof(TEvent),
            messages: messages);

    private static ApiParameter[] PagingParameters(int limit) =>
    [
        new("offset", typeof(int), false, 0, "Zero-based result offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, limit, "Maximum number of entries to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        IdConstraints());

    private static ApiConstraints IdConstraints() => new(
        Pattern: "^[1-9][0-9]*$");

    private static ApiParameter RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        TextConstraints(false));

    private static ApiConstraints TextConstraints(bool allow_empty) => new(
        MinLength: allow_empty ? 0 : 1,
        MaxUtf8Bytes: ushort.MaxValue,
        Pattern: allow_empty ? null : @".*\S.*");

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);
}

internal static class GroupMembershipDescriptors
{
    public static ApiDescriptor Join { get; } = Operation<GroupJoinRequest>(
        ApiIds.GroupMembershipJoin,
        "Join group",
        "Requests membership in a group.",
        [RequiredId("group_id", "Group identifier.")],
        MessageKeys.Groups.Membership.Join);

    public static ApiDescriptor Kick { get; } = Operation<GroupMemberKickRequest>(
        ApiIds.GroupMembershipKick,
        "Kick group member",
        "Removes a user from a group and optionally blocks rejoining.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier."),
            new("block_rejoin", typeof(bool), false, false, "Prevent the user from rejoining.")
        ],
        MessageKeys.Groups.Membership.Kick);

    public static ApiDescriptor Approve { get; } = Operation<GroupMemberRequest>(
        ApiIds.GroupMembershipApprove,
        "Approve group member",
        "Approves a pending group membership request.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier.")
        ],
        MessageKeys.Groups.Membership.Approve);

    public static ApiDescriptor Reject { get; } = Operation<GroupMemberRequest>(
        ApiIds.GroupMembershipReject,
        "Reject group member",
        "Rejects a pending group membership request.",
        [
            RequiredId("group_id", "Group identifier."),
            RequiredId("user_id", "User identifier.")
        ],
        MessageKeys.Groups.Membership.Reject);

    private static ApiDescriptor Operation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey key) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(GroupMembershipDispatchResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages: [new(key, MessageDirection.Out, ApiMessageRole.Send)],
            toolHints: new(false, true, false, true));

    private static ApiParameter RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        new(Pattern: "^[1-9][0-9]*$"));
}

internal static class RemotePeopleDescriptors
{
    public static ApiDescriptor ProfileGet { get; } = Read<RemoteProfileGetRequest, RemoteProfileResult>(
        ApiIds.PeopleProfileGet,
        "Remote profile",
        "Loads an immutable remote-user profile for the active hotel session.",
        MessageKeys.Users.ExtendedProfileRequest,
        MessageKeys.Users.ExtendedProfileSnapshot);

    public static ApiDescriptor RelationshipGet { get; } = Read<RemoteRelationshipGetRequest, RemoteRelationshipResult>(
        ApiIds.PeopleRelationshipGet,
        "Remote relationship status",
        "Loads the relationship summary for a remote user in the active hotel session.",
        MessageKeys.Users.Relationship.Request,
        MessageKeys.Users.Relationship.Snapshot);

    public static ApiDescriptor BadgesGet { get; } = Read<RemoteBadgesGetRequest, RemoteBadgesResult>(
        ApiIds.PeopleBadgesGet,
        "Remote selected badges",
        "Loads the selected badge set for a remote user in the active hotel session.",
        MessageKeys.Badges.SelectedRequest,
        MessageKeys.Badges.Selected);

    public static ApiDescriptor ProfileOpen { get; } = new(
        ApiIds.PeopleProfileOpen,
        "Open remote profile",
        "Opens a remote user's profile in the active game client.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RemoteProfileOpenRequest),
        typeof(RemoteProfileOpenReceipt),
        [UserId(), SessionGeneration()],
        [GameRequirement.HotelConnected],
        messages: [Send(MessageKeys.Users.ExtendedProfileRequest)],
        toolHints: new(false, false, true, true));

    private static ApiDescriptor Read<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(TResult),
            [UserId(), Timeout(), SessionGeneration()],
            [GameRequirement.HotelConnected],
            messages: [Send(request_key), Observe(snapshot_key)],
            toolHints: new(true, false, true, true));

    private static ApiParameter UserId() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "Positive hotel user identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for locking, dispatch, retries and the matching hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);
}

internal static class GroupReadsDescriptors
{
    public static ApiDescriptor DetailsGet { get; } = Read<GroupDetailsGetRequest, GroupDetailsResult>(
        ApiIds.GroupsDetailsGet,
        "Group details",
        "Loads immutable details for a group in the active hotel session.",
        [GroupId(), Timeout(), SessionGeneration()],
        MessageKeys.Groups.Details.Request,
        MessageKeys.Groups.Details.Snapshot);

    public static ApiDescriptor MembersPage { get; } = Read<GroupMembersPageRequest, GroupMembersPage>(
        ApiIds.GroupsMembersPage,
        "Group members page",
        "Loads one authoritative hotel page of group members.",
        [
            GroupId(),
            new("page_index", typeof(int), false, 0, "Zero-based hotel page index.", new(Minimum: 0)),
            new(
                "user_name_filter",
                typeof(string),
                false,
                string.Empty,
                "Exact hotel user-name filter carried by the request.",
                new(MinLength: 0, MaxUtf8Bytes: ushort.MaxValue)),
            new(
                "search_type",
                typeof(GuildMemberSearchType),
                false,
                GuildMemberSearchType.All,
                "Requested group-member category."),
            Timeout(),
            SessionGeneration()
        ],
        MessageKeys.Groups.Members.Request,
        MessageKeys.Groups.Members.Snapshot);

    public static ApiDescriptor MembershipsGet { get; } = Read<GroupMembershipsGetRequest, GroupMembershipsPage>(
        ApiIds.GroupsMembershipsGet,
        "Group memberships",
        "Loads or continues a bounded page from a session-bound immutable membership snapshot.",
        [
            new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
            new("limit", typeof(int), false, 500, "Maximum memberships to return.", new(Minimum: 1, Maximum: 500)),
            Timeout(),
            new(
                "snapshot_revision",
                typeof(long?),
                false,
                null,
                "Positive revision required for every continuation page.",
                new(Minimum: 1)),
            SessionGeneration()
        ],
        MessageKeys.Groups.Memberships.Request,
        MessageKeys.Groups.Memberships.Snapshot);

    private static ApiDescriptor Read<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey request_key,
        MessageKey snapshot_key) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(TResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages: [Send(request_key), Observe(snapshot_key)],
            toolHints: new(true, false, true, true));

    private static ApiParameter GroupId() => new(
        "group_id",
        typeof(Id),
        true,
        null,
        "Positive group identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for locking, dispatch, retries and the matching hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);
}
