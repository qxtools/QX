using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;
using Qx.Model;

namespace Qx.Game.Wired.Simulation;

public enum SimulationEventKind
{
    Start, Chat, ClickFurniture, UseFurniture, Walk, Enter, Leave, Signal, ClickUser,
    UserAction, StateChanged, VariableChanged, GameStarts, GameEnds, ScoreChanged,
    BotDestinationReached, BotAvatarReached, AvatarCaught, TransactionCompleted, TransactionFailed, ApiRequest, ClockReached, Hover,
    OfferTrade, AcceptTrade, ConfirmTrade, LinkMessage, LinkDataframe, RoomInitialized, VariableObserved
}

public enum SimulationStatus { Idle, Running, Paused, Completed, Stopped, Faulted }
public enum SimulationSeverity { Information, Warning, Uncertain, Unsupported, Error }

public sealed record SimulationValue(long Value, long CreatedAt = 0, long UpdatedAt = 0, bool IsExact = true,
    bool CreationTimeKnown = true, bool UpdateTimeKnown = true)
{
    public static SimulationValue FromClientDisplay(int value, long? created_at = null, long? updated_at = null) =>
        new(value, created_at ?? 0, updated_at ?? 0, value is not (int.MinValue or int.MaxValue),
            created_at.HasValue, updated_at.HasValue);
}

public sealed record SimulationDerivedVariable(string Parent, long AddonId, int Index, int? TimeSource = null);

public sealed record SimulationTimeUtilitiesProfile(string Timezone, DayOfWeek FirstDayOfWeek, int MinimumDaysInFirstWeek = 1);

public sealed record SimulationHover(long ViewerId, WiredSourceDomain Domain, long EntityId, bool Active = true);

public sealed record SimulationFxState(int ConfigId, string VariableId, WiredSourceDomain Domain,
    long EntityId, long ViewerId, long Value, long? RemainingMilliseconds = null, bool IsInitialize = true);

public sealed record SimulationFxPresentation(IReadOnlyList<SimulationFxState> States,
    IReadOnlyList<SimulationHover> Hovers);

public sealed record SimulationVariable(string Id, string Name, WiredSourceDomain Domain,
    int Availability = 1, bool HasValue = true, bool Writable = true,
    IReadOnlyDictionary<long, string>? Text = null, string? Reference = null,
    SimulationDerivedVariable? Derived = null, string? TextFallback = null, long? DefinitionFurnitureId = null,
    string? DisplayName = null, bool? CanCreateAndDelete = null,
    string? PointerAccessor = null, bool? RequiresExecutionContext = null,
    IReadOnlyList<long>? ObservedHolders = null);

public sealed record SimulationVariableObservation(string VariableId, long UserId,
    SimulationValue? Value, int? Change = null, int? Origin = null);

public sealed record SimulationStoredUserValue(long UserId, string Name, string Figure, SimulationValue Value);

public sealed record SimulationStoredVariable(string VariableId, IReadOnlyList<SimulationStoredUserValue> Values,
    bool Complete = false);

public sealed record SimulationLeaderboardView(long FurnitureId, string Title, string Description,
    WiredLeaderboardLocation Location, WiredSortOrderOption SortMode, IReadOnlyList<WiredLeaderboardColumn> Columns,
    IReadOnlyList<WiredLeaderboardChoice> Choices, string? DefaultVariableId, int PageSize, bool Published);

public sealed record SimulationLeaderboardPage(SimulationLeaderboardView Board, string VariableId, int Page,
    int TotalCount, IReadOnlyList<WiredLeaderboardRow> Rows);

public sealed record SimulationFurnitureState(bool Walkable, double Height,
    bool Stackable = true, AvatarStance Stance = AvatarStance.Stand);

public sealed record SimulationClock(long ElapsedMilliseconds = 0, bool Running = false, bool Initialized = false);

public enum SimulationGamePhase { Stopped, Running, Paused }

public sealed record SimulationScoreUse(long ActionId, int Count, long? UserId = null, int? Team = null);

public sealed record SimulationGame(long ClockId, SimulationGamePhase Phase = SimulationGamePhase.Stopped,
    IReadOnlyList<SimulationScoreUse>? ScoreUses = null, IReadOnlyList<long>? FiredScoreTriggers = null);

public sealed record SimulationTimer(long Value, int TickOffsetMilliseconds, bool ResetPending = false);

public sealed record SimulationTeleportTiming(int ArrivalMilliseconds, int EffectMilliseconds, int EffectId);

public sealed record SimulationTeleportTimings(SimulationTeleportTiming Normal, SimulationTeleportTiming Fast);

public sealed record SimulationBotTeleportClock(int TickMilliseconds, int NextTickOffsetMilliseconds);

public sealed record SimulationBotTiming(SimulationTeleportTiming Teleport, int StepMilliseconds, int NextStepOffsetMilliseconds,
    int FollowRefreshMilliseconds = 500, int FollowArrivalMilliseconds = 1000, int FollowRetryMilliseconds = 1000,
    int? FollowStartOffsetMilliseconds = null, int? DeliveryArrivalMilliseconds = null,
    int? DeliveryStartOffsetMilliseconds = null, SimulationBotTeleportClock? TeleportClock = null);

public sealed record SimulationBotFollow(long UserId, long ActionId, long Revision, bool Arrived = false, Tile? Destination = null,
    bool Stopping = false);

public sealed record SimulationBotFollowChoice(long BotId, long UserId, Tile Destination);

public sealed record SimulationBotDelivery(long UserId, long ActionId, long Revision, int HandItem,
    int TargetX, int TargetY, double TargetZ, Tile Destination);

public sealed record SimulationBotDeliveryChoice(long BotId, long UserId, Tile Destination);

public sealed record SimulationBotMovementStep(long BotId, long TargetId);

public sealed record SimulationBotMovementChoice(long ActionId,
    IReadOnlyList<SimulationBotMovementStep> Steps);

public sealed record SimulationFurnitureTransferStep(long FurnitureId, long TargetId);

public sealed record SimulationFurnitureTransferChoice(long ActionId,
    IReadOnlyList<SimulationFurnitureTransferStep> Steps);

public sealed record SimulationCarrierContactChoice(long ActionId, long UserId);

public sealed record SimulationSpawnValueChoice(long ActionId, WiredSourceDomain Domain, long HolderId);

public sealed record SimulationRandomChoice(long AddonId, IReadOnlyList<long> ActionIds);

public sealed record SimulationRandomHistory(long AddonId, IReadOnlyList<IReadOnlyList<long>> Executions, bool Complete = true);

public sealed record SimulationLimitHistory(long AddonId, IReadOnlyList<long> Activations, bool Complete = true, long ObservedSince = 0,
    IReadOnlyList<long>? ConditionFailures = null);

public sealed record SimulationUserActivity(int? Dance = null, int? Sign = null, bool SignKnown = false,
    long? SignRemainingMilliseconds = null, long SignObservedAt = 0);

public sealed record SimulationFreeze(int PreviousEffect, int EffectId, bool CancelOnTeleport);

public sealed record SimulationProjectileTiming(int TickMilliseconds, int NextTickOffsetMilliseconds);

public sealed record SimulationProjectileState(long ModifierId, SimulationMovement Movement,
    int VariableMask, long ElapsedMilliseconds, int Cell, int X, int Y, int Altitude, bool Travelling,
    IReadOnlyList<long> FurnitureHits, IReadOnlyList<long> UserHits);

public sealed record SimulationChest(bool StoresCoins, int? Coins = null,
    IReadOnlyList<WiredChestStorageSnapshot>? Items = null, bool ItemsComplete = false,
    IReadOnlyList<ChestItemType>? PreviewItems = null, bool? WiredEnabled = null, bool? Locked = null, int? Capacity = null);

public sealed record SimulationTrade(bool Active, long? ContractId = null, bool ChestDeposit = false,
    int? State = null, int? Multiplier = null, WiredTradeInitiate? Initiation = null, long Revision = 0,
    IReadOnlyList<SimulationReservation>? Reservations = null, bool? CanAccept = null,
    long? CreditChestId = null, long? ActionId = null, int? OfferedCredits = null, long? AcceptedAt = null);

public sealed record SimulationTradeInput(long Revision, int? Credits = null);

public sealed record SimulationReservation(long ChestId, IReadOnlyList<int> InventoryIds);

public sealed record SimulationRoomLink(long FurnitureId, long? RoomId = null, Tile? Destination = null,
    int? DepartureMilliseconds = null, int? EffectId = null, bool? Accessible = null);

public sealed record SimulationFurniture(long Id, string Key, string ClassName, int Kind, int X, int Y,
    double Z = 0, int Direction = 0, int State = 0, int Width = 1, int Length = 1,
    double Height = 0, int? StateCount = null, bool Walkable = true, bool Stackable = true,
    bool Temporary = false, bool Hidden = false,
    IReadOnlyDictionary<string, SimulationValue>? Variables = null,
    IReadOnlyDictionary<int, SimulationFurnitureState>? States = null,
    AvatarStance Stance = AvatarStance.Stand, int? FleeDirection = null, SimulationClock? Clock = null,
    int? IntrinsicState = null, string? Name = null, IReadOnlyList<int>? Directions = null,
    SimulationProjectileState? Projectile = null, SimulationChest? Chest = null,
    int? RecolorRgb = null, IReadOnlyList<int>? RecolorPalette = null, WiredContractContents? Contract = null,
    SimulationRoomLink? RoomLink = null, int? IntrinsicType = null);

public sealed record SimulationAchievement(string Name, long Value, string? Code = null);

public sealed record SimulationTrackTask(string Id, long Value, long? ResetValue = null);

public sealed record SimulationRewardTrack(string Id, IReadOnlyList<SimulationTrackTask> Tasks);

public sealed record SimulationUser(long Id, string Name, int X, int Y, double Z = 0,
    int Direction = 0, int Team = 0, int Score = 0, int Level = 0, int Effect = 0,
    int HandItem = 0, bool IsBot = false, bool IsPet = false, bool Frozen = false,
    string Figure = "", int Action = 0, IReadOnlyList<string>? Badges = null,
    IReadOnlyList<long>? Groups = null, IReadOnlyDictionary<string, SimulationValue>? Variables = null,
    AvatarStance Stance = AvatarStance.Stand, double? ActionHeight = null, Tile? NextStep = null,
    SimulationFreeze? Freeze = null, int? Index = null, SimulationBotFollow? Follow = null,
    SimulationBotDelivery? Delivery = null, WiredClickSettings? ClickSettings = null,
    SimulationTrade? Trade = null, SimulationUserActivity? Activity = null,
    IReadOnlyList<long>? WiredContacts = null,
    IReadOnlyList<SimulationAchievement>? Achievements = null,
    IReadOnlyList<SimulationRewardTrack>? RewardTracks = null);

public sealed record SimulationSavedFurniture(long Id, int? State = null, int? Direction = null,
    int? X = null, int? Y = null, double? Z = null);

public sealed record SimulationNode(long FurnitureId, WiredFormCategory Category, int Code,
    IReadOnlyDictionary<string, WiredFormValue> Fields,
    IReadOnlyList<long>? Furniture = null, IReadOnlyList<long>? SecondaryFurniture = null,
    IReadOnlyList<WiredFurnitureSource>? FurnitureSources = null,
    IReadOnlyList<WiredUserSource>? UserSources = null,
    int Delay = 0, int Quantifier = 0, bool Filter = false, bool Invert = false,
    IReadOnlyList<SimulationFurniture>? SavedFurniture = null,
    IReadOnlyDictionary<long, int>? MovementDirections = null, InputSourcesConf? InputSources = null,
    IReadOnlyList<SimulationSavedFurniture>? SavedSnapshot = null)
{
    public static SimulationNode From(WiredForm form) => new((long)form.FurniId,
        form.Definition.Category, form.DefinitionCode,
        copy_fields(form.Definition.Category, form.DefinitionCode, form.Definition.Fields.Select(field => KeyValuePair.Create(field.Name, form.GetField(field.Name)))),
        form.SelectedFurniture.Select(id => (long)id).ToArray(), form.SecondaryFurniture.Select(id => (long)id).ToArray(),
        form.FurnitureSources.ToArray(), form.UserSources.ToArray(), form.ActionDelay ?? 0,
        form.ConditionQuantifier ?? 0, form.SelectorFilter ?? false, form.SelectorInvert ?? false,
        InputSources: new(form.AllowedFurnitureSources.Select(values => (IReadOnlyList<int>)values.Select(value => (int)value).ToArray()).ToArray(),
            form.AllowedUserSources.Select(values => (IReadOnlyList<int>)values.Select(value => (int)value).ToArray()).ToArray(),
            form.DefaultFurnitureSources.Select(value => (int)value).ToArray(), form.DefaultUserSources.Select(value => (int)value).ToArray()));

    internal static IReadOnlyDictionary<string, WiredFormValue> copy_fields(WiredFormCategory category, int code,
        IEnumerable<KeyValuePair<string, WiredFormValue>> fields) => fields.ToDictionary(pair => pair.Key,
            pair => category == WiredFormCategory.Addon && code == 2002 && pair.Key is "read_key" or "write_key"
                ? new WiredFormValue(Text: "") : pair.Value, StringComparer.Ordinal);

    public long Number(string name, long fallback = 0) => Fields.TryGetValue(name, out var value)
        ? value.Integer ?? (value.Boolean is { } flag ? flag ? 1 : 0 : fallback) : fallback;
    public string Text(string name, string fallback = "") => Fields.TryGetValue(name, out var value) ? value.Text ?? fallback : fallback;
    public bool Flag(string name) => Number(name) != 0;
    public string Name => WiredFormRegistry.Find(Category, Code)?.Name ?? "Unknown";
}

public sealed record SimulationRoom(long RoomId, long OwnerId, int Width, int Height,
    IReadOnlyList<SimulationFurniture> Furniture, IReadOnlyList<SimulationUser> Users,
    IReadOnlyList<SimulationNode> Nodes,
    IReadOnlyList<SimulationVariable>? Variables = null,
    IReadOnlyDictionary<string, SimulationValue>? GlobalVariables = null,
    IReadOnlyList<double>? Floor = null, DateTimeOffset? Epoch = null,
    string? Source = null, IReadOnlyList<string>? Incomplete = null, long? GroupId = null,
    IReadOnlyDictionary<int, long>? TeamScores = null, SimulationApiAccess? Api = null,
    int? ClockTickOffsetMilliseconds = null, SimulationTimer? Timer = null,
    SimulationTeleportTimings? TeleportTimings = null, SimulationBotTiming? BotTiming = null,
    string? CalendarTimezone = null, SimulationTimeUtilitiesProfile? TimeUtilities = null,
    SimulationFxPresentation? FxPresentation = null, IReadOnlyList<int>? TeamOrder = null,
    SimulationProjectileTiming? ProjectileTiming = null, SimulationGame? Game = null,
    IReadOnlyList<SimulationStoredVariable>? StoredVariables = null,
    IReadOnlyList<SimulationVisual>? Visuals = null,
    IReadOnlyList<SimulationLinkEndpoint>? Links = null,
    IReadOnlyList<SimulationSharedPlaceholder>? SharedPlaceholders = null,
    IReadOnlyList<SimulationRandomHistory>? RandomHistory = null,
    IReadOnlyList<SimulationLimitHistory>? LimitHistory = null);

public sealed record SimulationRewardOutcome(long ActionId, long UserId, int? RewardIndex);

public sealed record SimulationOptions(int Seed = 1, int MaximumEvents = 100000,
    long MaximumWork = 1000000, long MaximumTimeMilliseconds = 3600000,
    int MaximumTrace = 2000, int MaximumFurniture = 20000,
    int MaximumStackVisitsPerExecution = 100, int MaximumMovesPerExecution = 80,
    int MaximumPendingEvents = 100000, bool RequireKnownOrder = false,
    int VariableBits = 64, int PulseMilliseconds = 500, int ShortPulseMilliseconds = 50,
    int MaximumSelectionDepth = 128, IReadOnlyList<SimulationBotFollowChoice>? BotFollowChoices = null,
    IReadOnlyList<SimulationBotDeliveryChoice>? BotDeliveryChoices = null,
    IReadOnlyList<SimulationFurnitureTransferChoice>? FurnitureTransferChoices = null,
    IReadOnlyList<SimulationSpawnValueChoice>? SpawnValueChoices = null,
    IReadOnlyList<SimulationRandomChoice>? RandomChoices = null,
    IReadOnlyList<SimulationCarrierContactChoice>? CarrierContactChoices = null,
    IReadOnlyList<SimulationBotMovementChoice>? BotMovementChoices = null,
    IReadOnlyList<SimulationRewardOutcome>? RewardOutcomes = null);

public sealed record SimulationEvent(SimulationEventKind Kind, long At = 0, long? UserId = null,
    long? FurnitureId = null, string? Text = null, int? X = null, int? Y = null,
    IReadOnlyList<long>? Furniture = null, IReadOnlyList<long>? Users = null,
    IReadOnlyDictionary<string, long>? Context = null, string? Variable = null,
    long? OldValue = null, long? NewValue = null, int Change = 0, int Origin = 0, int? Team = null,
    SimulationApiRequest? Api = null, SimulationHover? Hover = null, SimulationTradeInput? Trade = null,
    SimulationLinkPayload? Link = null, SimulationUserActivity? Activity = null,
    SimulationVariableObservation? Observation = null);

public sealed record SimulationDiagnostic(SimulationSeverity Severity, string Code,
    string Message, long At, long? FurnitureId = null);

public sealed record SimulationTrace(long Sequence, long At, string Kind, long? FurnitureId,
    string Message, IReadOnlyList<long>? Targets = null, long? Before = null, long? After = null);

public enum SimulationSpeechMode { Talk, Shout, Whisper }

public sealed record SimulationSpeech(long SpeakerId, SimulationSpeechMode Mode,
    int BubbleWidth, bool Private, bool HideUsername = false);

public sealed record SimulationMovement(WiredMovementType Type, Tile Source, Tile Destination,
    int AnimationTime, int Direction, int? HeadDirection = null, bool IsSlide = false,
    int? CurveStrength = null, int? JumpPower = null, int? OvershootDistance = null)
{
    public int? OvershootAnimationTime => OvershootDistance;
}

public sealed record SimulationEffect(long At, string Kind, long FurnitureId,
    IReadOnlyList<long> Targets, string? Text = null, long? Value = null, SimulationSpeech? Speech = null,
    WiredClickSettings? ClickSettings = null, SimulationMovement? Movement = null,
    SimulationChestTransfer? ChestTransfer = null, WiredTradeInitiate? TradeInitiation = null,
    SimulationRoomLink? RoomLink = null, SimulationVisualCommand? Visual = null,
    SimulationLinkPayload? Link = null);

public sealed record SimulationChestTransfer(long ChestId, long UserId,
    IReadOnlyList<WiredChestStorageSnapshot> SourceItems, int Coins = 0, EarningCategory? EarningsCategory = null);

public sealed record SimulationMetrics(long Events, long StackActivations, long Actions,
    long WorkUnits, long VariableReads, long VariableWrites, long SelectorCandidates,
    long Moves, long TemporaryPlacements, long PeakPendingEvents, long PeakWorkPerExecution);

public sealed record SimulationFxView(long ViewerId, bool Visible, bool Hovered, long? VisibleUntil, bool IsInitialize);

public sealed record SimulationVariableFx(VariableFxConfigEntry Config, string VariableId,
    WiredSourceDomain Domain, long EntityId, long Value, long? MinOverride, long? MaxOverride,
    IReadOnlyDictionary<string, string> Extra, IReadOnlyList<long> Audience, IReadOnlyList<SimulationFxView>? Views = null)
{
    public long MinValue => MinOverride ?? Config.DefaultMinValue;
    public long MaxValue => MaxOverride ?? Config.DefaultMaxValue;
}

public sealed record SimulationResult(SimulationStatus Status, long Time,
    SimulationRoom Room, SimulationMetrics Metrics,
    IReadOnlyList<SimulationDiagnostic> Diagnostics, IReadOnlyList<SimulationTrace> Trace,
    IReadOnlyList<SimulationEffect> Effects, int PendingEvents, bool TraceTruncated,
    bool ProvesServerBehavior = false, IReadOnlyList<SimulationApiResponse>? ApiResponses = null,
    IReadOnlyList<SimulationVariableFx>? VariableFx = null,
    IReadOnlyList<SimulationVisualSample>? Visuals = null)
{
    public bool HasUnresolvedRules => Diagnostics.Any(value => value.Severity is SimulationSeverity.Unsupported or SimulationSeverity.Uncertain);
    public bool Passed => Status == SimulationStatus.Completed && Diagnostics.All(value => value.Severity == SimulationSeverity.Information);
}

public sealed record SimulationCoverage(WiredFormCategory Category, int Code, string Name,
    string Support, IReadOnlyList<string> Limits);

public sealed record SimulationAssertion(string Kind, long? EntityId = null, string? Variable = null,
    long? Value = null, int? X = null, int? Y = null, bool? Present = null);

public sealed record SimulationAssertionResult(SimulationAssertion Assertion, bool Passed, string Actual);
