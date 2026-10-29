using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Friends;

internal static class FriendsDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor List { get; } = new(
        ApiIds.FriendsList,
        "Friends",
        "Reads a filtered page from the current immutable friend-list snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(FriendsListRequest),
        typeof(FriendListPage),
        ListParameters(200),
        stateEffects: [new(GameRequirement.FriendsLoaded, ApiStateChange.Reads)],
        messages: ListMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.FriendsRefresh,
        "Refresh friends",
        "Loads the active session's complete friend list and returns a filtered page.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(FriendsRefreshRequest),
        typeof(FriendListPage),
        [.. ListParameters(200), TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.FriendsLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Friends.InitializeRequest, MessageDirection.Out, ApiMessageRole.Send),
            .. ListMessages()
        ],
        new(true, false, true, true));

    public static ApiDescriptor Search { get; } = new(
        ApiIds.FriendsSearch,
        "Search users",
        "Searches the active hotel for users and separates existing friends from other results.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(FriendsSearchRequest),
        typeof(FriendsSearchResult),
        [RequiredText("query", "User name or fragment to search for."), TimeoutParameter()],
        [GameRequirement.HotelConnected],
        messages:
        [
            new(MessageKeys.Friends.SearchRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Friends.SearchResult, MessageDirection.In, ApiMessageRole.Observe)
        ],
        toolHints: new(true, false, false, true));

    public static ApiDescriptor MessageHistory { get; } = new(
        ApiIds.FriendMessageHistory,
        "Friend message history",
        "Reads the bounded private-message journal with stable cursor paging.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(FriendMessageHistoryRequest),
        typeof(FriendMessageHistoryPage),
        CursorParameters(),
        messages:
        [
            new(MessageKeys.Friends.PrivateMessageReceived, MessageDirection.In, ApiMessageRole.Observe)
        ],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor MessageSend { get; } = Operation<FriendMessageSendRequest>(
        ApiIds.FriendMessageSend,
        "Send private message",
        "Sends a private messenger message with the active Flash sequence layout.",
        [
            RequiredId("recipient_id", "Friend identifier."),
            RequiredText("message", "Private message text.")
        ],
        MessageKeys.Friends.PrivateMessageSend,
        destructive: true);

    public static ApiDescriptor RequestSend { get; } = Operation<FriendRequestSendRequest>(
        ApiIds.FriendRequestSend,
        "Send friend request",
        "Sends a friend request by hotel user name.",
        [RequiredText("name", "Hotel user name.")],
        MessageKeys.Friends.FriendRequestSend,
        destructive: true);

    public static ApiDescriptor RequestAccept { get; } = Operation<FriendRequestIdsRequest>(
        ApiIds.FriendRequestAccept,
        "Accept friend requests",
        "Accepts one or more pending friend requests.",
        [RequiredIds("request_ids", "Pending friend-request identifiers.")],
        MessageKeys.Friends.FriendRequestAccept,
        destructive: true,
        changes_friends: true);

    public static ApiDescriptor RequestDecline { get; } = Operation<FriendRequestDeclineRequest>(
        ApiIds.FriendRequestDecline,
        "Decline friend requests",
        "Declines one or more selected pending friend requests.",
        [RequiredIds("request_ids", "Pending friend-request identifiers.")],
        MessageKeys.Friends.FriendRequestDecline,
        destructive: true);

    public static ApiDescriptor RequestsDeclineAll { get; } = Operation<FriendRequestsDeclineAllRequest>(
        ApiIds.FriendRequestsDeclineAll,
        "Decline all friend requests",
        "Declines every pending friend request.",
        [],
        MessageKeys.Friends.FriendRequestDecline,
        destructive: true);

    public static ApiDescriptor RequestsList { get; } = new(
        ApiIds.FriendRequestsList,
        "Pending friend requests",
        "Loads the active session's current pending friend requests.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(FriendRequestsListRequest),
        typeof(PendingFriendRequests),
        [TimeoutParameter()],
        [GameRequirement.HotelConnected],
        messages:
        [
            new(MessageKeys.Friends.FriendRequestsRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Friends.FriendRequestsSnapshot, MessageDirection.In, ApiMessageRole.Observe)
        ],
        toolHints: new(true, false, true, true));

    public static ApiDescriptor Remove { get; } = Operation<FriendsRemoveRequest>(
        ApiIds.FriendsRemove,
        "Remove friends",
        "Removes one or more users from the friend list.",
        [RequiredIds("friend_ids", "Friend identifiers to remove.")],
        MessageKeys.Friends.Remove,
        destructive: true,
        changes_friends: true);

    public static ApiDescriptor Follow { get; } = Operation<FriendFollowRequest>(
        ApiIds.FriendFollow,
        "Follow friend",
        "Follows a friend to their current room.",
        [RequiredId("friend_id", "Friend identifier to follow.")],
        MessageKeys.Friends.Follow);

    public static ApiDescriptor RelationshipSet { get; } = Operation<FriendRelationshipSetRequest>(
        ApiIds.FriendRelationshipSet,
        "Set friend relationship",
        "Changes the relationship marker shown for a friend.",
        [
            RequiredId("friend_id", "Friend identifier."),
            new("relationship", typeof(RelationshipType), true, null, "Relationship marker.")
        ],
        MessageKeys.Friends.RelationshipSet,
        destructive: true,
        changes_friends: true);

    public static ApiDescriptor Changed { get; } = Event<FriendChanged>(
        ApiIds.FriendsChanged,
        "Friend list changed",
        "Publishes immutable friend-list load, add, update, remove and reset changes.",
        ListMessages(),
        [new(GameRequirement.FriendsLoaded, ApiStateChange.Changes)]);

    public static ApiDescriptor MessageReceived { get; } = Event<FriendMessageEntry>(
        ApiIds.FriendMessageReceived,
        "Friend message received",
        "Publishes immutable private messenger entries.",
        [new(MessageKeys.Friends.PrivateMessageReceived, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor MessageFailed { get; } = Event<InstantMessageError>(
        ApiIds.FriendMessageFailed,
        "Friend message failed",
        "Publishes private-message delivery failures.",
        [new(MessageKeys.Friends.PrivateMessageFailed, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor OperationFailed { get; } = Event<MessengerError>(
        ApiIds.FriendOperationFailed,
        "Friend operation failed",
        "Publishes hotel rejections for messenger operations.",
        [new(MessageKeys.Friends.OperationFailed, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor RequestReceived { get; } = Event<NewFriendRequest>(
        ApiIds.FriendRequestReceived,
        "Friend request received",
        "Publishes immutable incoming friend requests.",
        [new(MessageKeys.Friends.FriendRequestReceived, MessageDirection.In, ApiMessageRole.Observe)]);

    private static ApiDescriptor Operation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey key,
        bool read_only = false,
        bool destructive = false,
        bool idempotent = false,
        bool changes_friends = false) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(FriendOperationResult),
            parameters,
            [GameRequirement.HotelConnected],
            changes_friends
                ? [new(GameRequirement.FriendsLoaded, ApiStateChange.Changes)]
                : [],
            [new(key, MessageDirection.Out, ApiMessageRole.Send)],
            new(read_only, destructive, idempotent, true));

    private static ApiDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiMessageRequirement> messages,
        IReadOnlyList<ApiStateEffect>? state_effects = null) => new(
            id,
            title,
            description,
            ApiMemberKind.Event,
            event_exposure,
            null,
            typeof(TEvent),
            stateEffects: state_effects,
            messages: messages);

    private static ApiParameter[] ListParameters(int limit) =>
    [
        new(
            "query",
            typeof(string),
            false,
            string.Empty,
            "Optional local name, real-name or motto filter.",
            new(MaxUtf8Bytes: ushort.MaxValue)),
        new("online_only", typeof(bool), false, false, "Return only online friends."),
        new("offset", typeof(int), false, 0, "Zero-based result offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, limit, "Maximum number of friends to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApiParameter[] CursorParameters() =>
    [
        new(
            "after_sequence",
            typeof(long),
            false,
            0L,
            "Return entries after this sequence.",
            new(Pattern: "^[0-9]+$")),
        new("limit", typeof(int), false, 100, "Maximum number of entries to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApiParameter RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        new(Pattern: "^-?[0-9]+$"));

    private static ApiParameter RequiredIds(string name, string description) => new(
        name,
        typeof(IReadOnlyList<Id>),
        true,
        null,
        description,
        new(MinItems: 1, MaxItems: ushort.MaxValue));

    private static ApiMessageRequirement[] ListMessages() =>
    [
        new(MessageKeys.Friends.Initialized, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Friends.ListFragment, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Friends.ListUpdated, MessageDirection.In, ApiMessageRole.Observe)
    ];
}
