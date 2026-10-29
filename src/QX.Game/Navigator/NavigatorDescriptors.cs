using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Navigator;

internal static class NavigatorDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.NavigatorState,
        "Navigator state",
        "Reads the active session's immutable navigator metadata and personalization snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(NavigatorStateRequest),
        typeof(NavigatorState),
        stateEffects:
        [
            new(GameRequirement.NavigatorMetadataLoaded, ApiStateChange.Reads),
            new(GameRequirement.NavigatorFlatCategoriesLoaded, ApiStateChange.Reads)
        ],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor MetadataRefresh { get; } = new(
        ApiIds.NavigatorMetadataRefresh,
        "Refresh navigator metadata",
        "Loads the navigator categories published for the active hotel session.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(NavigatorRefreshRequest),
        typeof(NavigatorState),
        [TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.NavigatorMetadataLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Navigator.WindowSettings, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Navigator.State.MetadataRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Navigator.State.Metadata, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor FlatCategoriesRefresh { get; } = new(
        ApiIds.NavigatorFlatCategoriesRefresh,
        "Refresh room categories",
        "Loads the room categories published for the active hotel session.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(NavigatorRefreshRequest),
        typeof(NavigatorState),
        [TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.NavigatorFlatCategoriesLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Navigator.WindowSettings, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Navigator.State.FlatCategoriesRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Navigator.State.FlatCategories, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor SearchView { get; } = Search<NavigatorViewSearchInput>(
        ApiIds.NavigatorSearchView,
        "Search navigator view",
        "Searches a hotel-published navigator view with an explicit filter.",
        MessageKeys.Navigator.Search.View,
        [RequiredText("search_code", "Hotel-published navigator view code."), OptionalText("filter", "Navigator filter text."), TimeoutParameter()]);

    public static ApiDescriptor SearchText { get; } = Search<NavigatorTextSearchInput>(
        ApiIds.NavigatorSearchText,
        "Search rooms by text",
        "Searches rooms by the selected navigator text field.",
        MessageKeys.Navigator.Search.Text,
        [
            new("field", typeof(RoomSearchField), true, null, "Navigator text field."),
            OptionalText("text", "Text to search for."),
            TimeoutParameter()
        ],
        MessageKeys.Navigator.Search.LegacyResult);

    public static ApiDescriptor SearchMyRooms { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyRooms,
        "Search my rooms",
        "Loads rooms owned by the active account.",
        MessageKeys.Navigator.Search.MyRooms);

    public static ApiDescriptor SearchMyFavourites { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyFavourites,
        "Search favourite rooms",
        "Loads rooms favourited by the active account.",
        MessageKeys.Navigator.Search.MyFavouriteRooms);

    public static ApiDescriptor SearchMyRoomRights { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyRoomRights,
        "Search rooms with rights",
        "Loads rooms where the active account has room rights.",
        MessageKeys.Navigator.Search.MyRoomRights);

    public static ApiDescriptor SearchMyHistory { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyHistory,
        "Search room history",
        "Loads rooms recently visited by the active account.",
        MessageKeys.Navigator.Search.MyRoomHistory);

    public static ApiDescriptor SearchMyFrequentHistory { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyFrequentHistory,
        "Search frequent rooms",
        "Loads rooms most frequently visited by the active account.",
        MessageKeys.Navigator.Search.MyFrequentRoomHistory);

    public static ApiDescriptor SearchMyFriendsRooms { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyFriendsRooms,
        "Search friends' rooms",
        "Loads rooms owned by friends of the active account.",
        MessageKeys.Navigator.Search.MyFriendsRooms);

    public static ApiDescriptor SearchFriendsPresent { get; } = QuickSearch(
        ApiIds.NavigatorSearchFriendsPresent,
        "Search rooms with friends",
        "Loads rooms where friends of the active account are currently present.",
        MessageKeys.Navigator.Search.RoomsWhereFriendsAre);

    public static ApiDescriptor SearchMyGuildBases { get; } = QuickSearch(
        ApiIds.NavigatorSearchMyGuildBases,
        "Search my guild bases",
        "Loads bases of guilds joined by the active account.",
        MessageKeys.Navigator.Search.MyGuildBases);

    public static ApiDescriptor SearchPopular { get; } = Search<NavigatorPopularSearchInput>(
        ApiIds.NavigatorSearchPopular,
        "Search popular rooms",
        "Loads popular rooms, optionally filtered by tag.",
        MessageKeys.Navigator.Search.Popular,
        [OptionalText("tag", "Optional room tag."), AdIndexParameter(), TimeoutParameter()]);

    public static ApiDescriptor SearchHighestScore { get; } = Search<NavigatorAdSearchInput>(
        ApiIds.NavigatorSearchHighestScore,
        "Search highest-scoring rooms",
        "Loads the hotel's highest-scoring rooms.",
        MessageKeys.Navigator.Search.HighestScoring,
        [AdIndexParameter(), TimeoutParameter()]);

    public static ApiDescriptor SearchGuildBases { get; } = Search<NavigatorAdSearchInput>(
        ApiIds.NavigatorSearchGuildBases,
        "Search guild bases",
        "Loads public guild-base rooms.",
        MessageKeys.Navigator.Search.GuildBases,
        [AdIndexParameter(), TimeoutParameter()]);

    public static ApiDescriptor SavedSearchAdd { get; } = Personalization<NavigatorSavedSearchAddInput>(
        ApiIds.NavigatorSavedSearchAdd,
        "Add saved search",
        "Adds a navigator search to the active account's saved searches.",
        MessageKeys.Navigator.Personalization.SavedSearchAdd,
        [RequiredText("search_code", "Navigator view code."), OptionalText("filter", "Navigator filter text.")],
        new(false, false, false, true));

    public static ApiDescriptor SavedSearchDelete { get; } = Personalization<NavigatorSavedSearchDeleteInput>(
        ApiIds.NavigatorSavedSearchDelete,
        "Delete saved search",
        "Deletes a saved navigator search from the active account.",
        MessageKeys.Navigator.Personalization.SavedSearchDelete,
        [new("saved_search_id", typeof(int), true, null, "Saved-search identifier.", new(Minimum: 0))],
        new(false, true, false, true));

    public static ApiDescriptor CategoryCollapse { get; } = Personalization<NavigatorCategoryInput>(
        ApiIds.NavigatorCategoryCollapse,
        "Collapse navigator category",
        "Collapses a navigator category for the active account.",
        MessageKeys.Navigator.Personalization.CollapsedCategoryAdd,
        [RequiredText("search_code", "Navigator category code.")],
        new(false, false, true, true));

    public static ApiDescriptor CategoryExpand { get; } = Personalization<NavigatorCategoryInput>(
        ApiIds.NavigatorCategoryExpand,
        "Expand navigator category",
        "Expands a collapsed navigator category for the active account.",
        MessageKeys.Navigator.Personalization.CollapsedCategoryRemove,
        [RequiredText("search_code", "Navigator category code.")],
        new(false, false, true, true));

    public static ApiDescriptor RoomCreate { get; } = RoomOperation<NavigatorRoomCreateInput>(
        ApiIds.NavigatorRoomCreate,
        "Create room",
        "Creates a room owned by the active account.",
        MessageKeys.Navigator.RoomCreate,
        [
            RequiredText("name", "Room name."),
            new("description", typeof(string), true, null, "Room description, which may be empty.",
                new(MaxUtf8Bytes: ushort.MaxValue)),
            RequiredText("model", "Floor-plan model name."),
            new("category", typeof(int), true, null, "Navigator category identifier."),
            new("max_visitors", typeof(int), true, null, "Maximum visitor count."),
            new("trade_mode", typeof(int), false, 0, "Room trading mode.")
        ]);

    public static ApiDescriptor RoomDelete { get; } = RoomOperation<NavigatorRoomDeleteInput>(
        ApiIds.NavigatorRoomDelete,
        "Delete room",
        "Deletes a room owned by the active account.",
        MessageKeys.Navigator.RoomDelete,
        [new("room_id", typeof(Id), true, null, "Room identifier.")]);

    public static ApiDescriptor HomeRoomSet { get; } = RoomOperation<NavigatorHomeRoomSetInput>(
        ApiIds.NavigatorHomeRoomSet,
        "Set home room",
        "Sets or clears the active account's home room.",
        MessageKeys.Navigator.HomeRoomUpdate,
        [new("room_id", typeof(Id), true, null, "Room identifier, or zero to clear it.")]);

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.NavigatorChanged,
        "Navigator state changed",
        "Publishes immutable navigator metadata and personalization state changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(NavigatorChanged),
        stateEffects:
        [
            new(GameRequirement.NavigatorMetadataLoaded, ApiStateChange.Changes),
            new(GameRequirement.NavigatorFlatCategoriesLoaded, ApiStateChange.Changes)
        ],
        messages: StateMessages());

    public static ApiDescriptor SearchReceived { get; } = new(
        ApiIds.NavigatorSearchReceived,
        "Navigator search received",
        "Publishes immutable navigator search results observed for the active session.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(NavigatorSearchReceived),
        messages:
        [
            new(MessageKeys.Navigator.Search.Result, MessageDirection.In, ApiMessageRole.Observe),
            new(MessageKeys.Navigator.Search.LegacyResult, MessageDirection.In, ApiMessageRole.Observe)
        ]);

    private static ApiDescriptor QuickSearch(
        string id,
        string title,
        string description,
        MessageKey request_key) => Search<NavigatorSearchRequest>(
            id,
            title,
            description,
            request_key,
            [TimeoutParameter()],
            MessageKeys.Navigator.Search.LegacyResult);

    private static ApiDescriptor Search<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey? result_key = null) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(NavigatorSearchSnapshot),
            parameters,
            [GameRequirement.HotelConnected],
            messages:
            [
                new(MessageKeys.Navigator.WindowSettings, MessageDirection.Out, ApiMessageRole.Send),
                new(request_key, MessageDirection.Out, ApiMessageRole.Send),
                new(
                    result_key ?? MessageKeys.Navigator.Search.Result,
                    MessageDirection.In,
                    ApiMessageRole.Observe)
            ],
            toolHints: new(true, false, true, true));

    private static ApiDescriptor Personalization<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApiParameter> parameters,
        ApiToolHints hints) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(NavigatorOperationResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages:
            [
                new(MessageKeys.Navigator.WindowSettings, MessageDirection.Out, ApiMessageRole.Send),
                new(request_key, MessageDirection.Out, ApiMessageRole.Send)
            ],
            toolHints: hints);

    private static ApiDescriptor RoomOperation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApiParameter> parameters) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(NavigatorRoomOperationResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages:
            [
                new(MessageKeys.Navigator.WindowSettings, MessageDirection.Out, ApiMessageRole.Send),
                new(request_key, MessageDirection.Out, ApiMessageRole.Send)
            ],
            toolHints: new(false, true, false, true));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter AdIndexParameter() => new(
        "ad_index",
        typeof(int),
        false,
        -1,
        "Promoted-room slot requested alongside the search result.",
        new(Minimum: -1));

    private static ApiParameter RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApiParameter OptionalText(string name, string description) => new(
        name,
        typeof(string),
        false,
        string.Empty,
        description,
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApiMessageRequirement[] StateMessages() =>
    [
        new(MessageKeys.Navigator.State.Metadata, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.State.FlatCategories, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.State.LiftedRooms, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.State.Settings, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.State.Preferences, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.Personalization.SavedSearches, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Navigator.Personalization.CollapsedCategories, MessageDirection.In, ApiMessageRole.Observe)
    ];
}
