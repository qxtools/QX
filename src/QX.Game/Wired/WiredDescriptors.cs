using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Game.Rooms;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Wired;

internal static partial class WiredDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.WiredState,
        "Wired state",
        "Reads the immutable generation and revision bound Wired room state.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(WiredStateRequest),
        typeof(WiredStateView),
        [
            new("chest_offset", typeof(int), false, 0, "Zero-based first Wired chest index.", new(Minimum: 0)),
            new("chest_limit", typeof(int), false, 5, "Maximum Wired chests returned.", new(Minimum: 0, Maximum: 10)),
            new("item_offset", typeof(int), false, 0, "Zero-based first item index within every returned chest.", new(Minimum: 0)),
            new("item_limit", typeof(int), false, 20, "Maximum items returned per chest.", new(Minimum: 0, Maximum: 50))
        ],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ConfigurationGet { get; } = Call<
        WiredConfigurationGetRequest,
        WiredConfigurationSnapshot>(
        ApiIds.WiredConfigurationGet,
        "Get Wired configuration",
        "Requests and returns the exact trigger, action, condition, selector, add-on or variable configuration for one furni.",
        [
            new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint()),
            TimeoutParameter()
        ],
        [
            Send(MessageKeys.Wired.Configuration.OpenRequest),
            Observe(MessageKeys.Wired.Configuration.Trigger, false),
            Observe(MessageKeys.Wired.Configuration.Action, false),
            Observe(MessageKeys.Wired.Configuration.Condition, false),
            Observe(MessageKeys.Wired.Configuration.Selector, false),
            Observe(MessageKeys.Wired.Configuration.Addon, false),
            Observe(MessageKeys.Wired.Configuration.Variable, false)
        ],
        ReadHints());

    public static ApiDescriptor ConfigurationOpen { get; } = SendCall<
        WiredConfigurationOpenRequest>(
        ApiIds.WiredConfigurationOpen,
        "Open Wired configuration",
        "Requests one Wired furni configuration without waiting for its kind-specific response.",
        MessageKeys.Wired.Configuration.OpenRequest,
        [new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint())],
        ReadHints());

    public static ApiDescriptor ConfigurationSnapshotApply { get; } = SendCall<
        WiredConfigurationApplySnapshotRequest>(
        ApiIds.WiredConfigurationSnapshotApply,
        "Apply Wired snapshot",
        "Requests restoration of selected furniture from a Wired's stored snapshot. Does not capture current values. Unsupported forms may leave the room unchanged; this operation only confirms dispatch.",
        MessageKeys.Wired.Configuration.ApplySnapshot,
        [new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint())],
        WriteHints(false, true));

    public static ApiDescriptor ConfigurationTriggerSave { get; } = Save<
        WiredTriggerSaveRequest>(
        ApiIds.WiredConfigurationTriggerSave,
        "Save Wired trigger",
        "Replaces a trigger configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.TriggerUpdate,
        typeof(UpdateTrigger));

    public static ApiDescriptor ConfigurationActionSave { get; } = Save<
        WiredActionSaveRequest>(
        ApiIds.WiredConfigurationActionSave,
        "Save Wired action",
        "Replaces an action configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.ActionUpdate,
        typeof(UpdateAction));

    public static ApiDescriptor ConfigurationConditionSave { get; } = Save<
        WiredConditionSaveRequest>(
        ApiIds.WiredConfigurationConditionSave,
        "Save Wired condition",
        "Replaces a condition configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.ConditionUpdate,
        typeof(UpdateCondition));

    public static ApiDescriptor ConfigurationSelectorSave { get; } = Save<
        WiredSelectorSaveRequest>(
        ApiIds.WiredConfigurationSelectorSave,
        "Save Wired selector",
        "Replaces a selector configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.SelectorUpdate,
        typeof(UpdateSelector));

    public static ApiDescriptor ConfigurationAddonSave { get; } = Save<
        WiredAddonSaveRequest>(
        ApiIds.WiredConfigurationAddonSave,
        "Save Wired add-on",
        "Replaces an add-on configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.AddonUpdate,
        typeof(UpdateAddon));

    public static ApiDescriptor ConfigurationVariableSave { get; } = Save<
        WiredVariableSaveRequest>(
        ApiIds.WiredConfigurationVariableSave,
        "Save Wired variable",
        "Replaces a variable-box configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.VariableUpdate,
        typeof(UpdateVariable));

    public static ApiDescriptor VariablesHashGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredAllVariablesHash>(
        ApiIds.WiredVariablesHashGet,
        "Get Wired variables hash",
        "Returns the room-wide hash used to detect Wired variable definition changes.",
        MessageKeys.Wired.Variables.HashRequest,
        MessageKeys.Wired.Variables.Hash,
        [TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor VariablesDifferencesGet { get; } = RequestResponse<
        WiredVariableDifferencesRequest,
        WiredVariableDifferencesSnapshot>(
        ApiIds.WiredVariablesDifferencesGet,
        "Get Wired variable differences",
        "Returns one correlated variable-definition difference chunk for the supplied cache hashes.",
        MessageKeys.Wired.Variables.DifferencesRequest,
        MessageKeys.Wired.Variables.Differences,
        [
            new("cache", typeof(IReadOnlyList<VariableHashEntry>), false, null, "Known variable identifiers and per-variable hashes."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor VariablesList { get; } = RequestResponse<
        WiredVariableListRequest,
        WiredVariableCollectionSnapshot>(
        ApiIds.WiredVariablesList,
        "List Wired variables",
        "Returns a complete immutable variable-definition snapshot. Repeated reads verify the live room hash before reusing a complete snapshot. A changed or missing hash triggers a fresh complete difference stream within the original timeout; stale cached data is never returned. After a missing hash, reads use fresh difference streams for the rest of that room generation so late hash replies cannot validate a newer cache. Room/session changes cancel the read.",
        MessageKeys.Wired.Variables.DifferencesRequest,
        MessageKeys.Wired.Variables.Differences,
        [
            new("maximum_chunks", typeof(int), false, 64, "Fail-closed upper bound for difference chunks.", new(Minimum: 1, Maximum: 256)),
            TimeoutParameter(30000)
        ],
        ReadHints());

    public static ApiDescriptor VariablesObjectGet { get; } = RequestResponse<
        WiredVariablesObjectRequest,
        WiredVariablesObjectSnapshot>(
        ApiIds.WiredVariablesObjectGet,
        "Inspect Wired object variables",
        "Returns the Wired variable values held by one furni, room user or global scope. The client display is signed 32-bit; MayBeClamped marks boundary values that may represent larger 64-bit Wired values.",
        MessageKeys.Wired.Variables.ObjectRequest,
        MessageKeys.Wired.Variables.Object,
        [
            new("target", typeof(WiredTarget), true, null, "Furni, user or global variable target."),
            new("object_id", typeof(int), true, null, "Furni identifier, room user index or zero for global scope."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor VariablesHoldersGet { get; } = RequestResponse<
        WiredVariableHoldersRequest,
        WiredVariableHoldersSnapshot>(
        ApiIds.WiredVariablesHoldersGet,
        "Get Wired variable holders",
        "Returns one variable definition and every object currently holding its value. MayBeClamped identifies signed 32-bit display boundaries; do not infer an exact 64-bit value from them.",
        MessageKeys.Wired.Variables.HoldersRequest,
        MessageKeys.Wired.Variables.Holders,
        [VariableIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor VariablesHoldersDelete { get; } = SendCall<
        WiredVariableHoldersDeleteRequest>(
        ApiIds.WiredVariablesHoldersDelete,
        "Delete Wired variable from all holders",
        "Removes one variable from every furni and user holding it, as the variable overview's delete does. The game client only offers this with wired modify rights, for a persisted furni or user variable that can be created and deleted.",
        MessageKeys.Wired.Variables.HoldersDelete,
        [VariableIdParameter()],
        WriteHints(true, true));

    public static ApiDescriptor VariablesPermanentGet { get; } = RequestResponse<
        WiredPermanentVariablesRequest,
        WiredPermanentVariablesSnapshot>(
        ApiIds.WiredVariablesPermanentGet,
        "Get permanent Wired variables",
        "Returns permanent variable storage and timestamps for one entity. MayBeClamped marks signed 32-bit display boundaries that may hide a larger 64-bit value.",
        MessageKeys.Wired.Variables.PermanentRequest,
        MessageKeys.Wired.Variables.Permanent,
        [EntityTypeParameter(), EntityIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor VariablesOwnersGet { get; } = RequestResponse<
        WiredVariableOwnersRequest,
        WiredVariableOwnersSnapshot>(
        ApiIds.WiredVariablesOwnersGet,
        "Get Wired variable owners",
        "Returns one correlated page of entities that own a permanent variable.",
        MessageKeys.Wired.Variables.OwnersRequest,
        MessageKeys.Wired.Variables.Owners,
        [
            VariableIdParameter(),
            new("page", typeof(int), false, 1, "One-based result page.", new(Minimum: 1)),
            new("page_size", typeof(int), false, 50, "Rows requested from the hotel.", new(Minimum: 1, Maximum: 250)),
            new("sort_type_filter", typeof(int), false, 0, "Sort order of the management table; the game client starts with 0."),
            new("user_type_filter", typeof(int), false, -1, "Entity-type filter of the management table; the game client starts with -1 for all."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor VariablesObjectSet { get; } = SendCall<
        WiredObjectVariableSetRequest>(
        ApiIds.WiredVariablesObjectSet,
        "Set Wired object variable",
        "Writes, creates or deletes one variable value on a furni, user or global target.",
        MessageKeys.Wired.Variables.ObjectValueSet,
        VariableSetParameters(false),
        WriteHints(true, false));

    public static ApiDescriptor VariablesPermanentSet { get; } = RequestResponse<
        WiredPermanentVariableSetRequest,
        WiredSetUserPermanentVariableResult>(
        ApiIds.WiredVariablesPermanentSet,
        "Set permanent Wired variable",
        "Writes, creates or deletes one permanent variable and waits for the verified Flash result.",
        MessageKeys.Wired.Variables.PermanentValueSet,
        MessageKeys.Wired.Variables.PermanentValueSetResult,
        [.. VariableSetParameters(true), TimeoutParameter()],
        WriteHints(true, false));

    public static ApiDescriptor VariablesPermanentSetSend { get; } = SendCall<
        WiredPermanentVariableSendRequest>(
        ApiIds.WiredVariablesPermanentSetSend,
        "Send permanent Wired variable update",
        "Sends a permanent variable update without waiting for its result.",
        MessageKeys.Wired.Variables.PermanentValueSet,
        VariableSetParameters(true),
        WriteHints(true, false));

    public static ApiDescriptor RoomSettingsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredRoomSettings>(
        ApiIds.WiredRoomSettingsGet,
        "Get Wired room settings",
        "Returns the room's modify mask, read mask and Wired timezone.",
        MessageKeys.Wired.Room.SettingsRequest,
        MessageKeys.Wired.Room.Settings,
        [TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor RoomSettingsSet { get; } = RequestResponse<
        WiredRoomSettingsSetRequest,
        WiredRoomSettings>(
        ApiIds.WiredRoomSettingsSet,
        "Set Wired room settings",
        "Replaces the room's Wired permission masks and timezone and returns the committed settings.",
        MessageKeys.Wired.Room.SettingsUpdate,
        MessageKeys.Wired.Room.Settings,
        [
            new("modify_permission_mask", typeof(int), true, null, "Who may modify Wired in the room."),
            new("read_permission_mask", typeof(int), true, null, "Who may read Wired in the room."),
            new("timezone", typeof(string), true, null, "Wired room timezone.", TextConstraint()),
            TimeoutParameter()
        ],
        WriteHints(true, true));

    public static ApiDescriptor RoomStatsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredRoomStats>(
        ApiIds.WiredRoomStatsGet,
        "Get Wired room statistics",
        "Returns execution cost, furniture limits and permanent-variable usage for the room.",
        MessageKeys.Wired.Room.StatsRequest,
        MessageKeys.Wired.Room.Stats,
        [TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor RoomLogsGet { get; } = RequestResponse<
        WiredRoomLogsRequest,
        WiredRoomLogs>(
        ApiIds.WiredRoomLogsGet,
        "Get Wired room logs",
        "Returns one exactly correlated page of Wired room log entries and filters.",
        MessageKeys.Wired.Room.LogsRequest,
        MessageKeys.Wired.Room.Logs,
        [
            new("page", typeof(int), false, 1, "One-based log page.", new(Minimum: 1)),
            new("page_size", typeof(int), false, 50, "Log rows requested.", new(Minimum: 1, Maximum: 250)),
            new("log_level_filter", typeof(int), false, -1, "Log-level filter or -1."),
            new("log_source_filter", typeof(int), false, -1, "Log-source filter or -1."),
            new("query", typeof(string), false, string.Empty, "Optional log text query.", TextConstraint()),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor RoomErrorLogsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredErrorLogs>(
        ApiIds.WiredRoomErrorLogsGet,
        "Get Wired error logs",
        "Returns the room's aggregated Wired execution errors.",
        MessageKeys.Wired.ErrorLogs.Request,
        MessageKeys.Wired.ErrorLogs.Snapshot,
        [TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor RoomErrorLogsClear { get; } = SendCall<WiredCommandRequest>(
        ApiIds.WiredRoomErrorLogsClear,
        "Clear Wired error logs",
        "Clears the room's Wired execution error counters.",
        MessageKeys.Wired.ErrorLogs.Clear,
        [],
        WriteHints(true, true));

    public static ApiDescriptor RoomUserClick { get; } = RequestResponse<
        WiredUserClickRequest,
        WiredClickUserResponse>(
        ApiIds.WiredRoomUserClick,
        "Click Wired room user",
        "Asks the hotel whether the Wired user menu should open for one room index.",
        MessageKeys.Wired.UserClick.Request,
        MessageKeys.Wired.UserClick.Result,
        [
            new("index", typeof(int), true, null, "Room user index.", new(Minimum: 0)),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor RoomReload { get; } = SendCall<WiredCommandRequest>(
        ApiIds.WiredRoomReload,
        "Reload Wired room state",
        "Reloads the room's saved Wired state without rolling back changes.",
        MessageKeys.Wired.Room.Update,
        [],
        WriteHints(false, true));

    public static ApiDescriptor RoomRollback { get; } = SendCall<WiredCommandRequest>(
        ApiIds.WiredRoomRollback,
        "Roll back Wired room state",
        "Discards room state changes made since the last saved Wired state.",
        MessageKeys.Wired.Room.Update,
        [],
        WriteHints(true, false));

    public static ApiDescriptor PreferencesGet { get; } = new(
        ApiIds.WiredPreferencesGet,
        "Read account preferences",
        "Reads cached account preferences, including optional Wired settings. Sends nothing.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(WiredCommandRequest),
        typeof(WiredAccountPreferencesView),
        [],
        messages: [Observe(MessageKeys.Wired.Account.Preferences, false)],
        toolHints: ReadHints());

    public static ApiDescriptor WebApiKeyGenerate { get; } = RequestResponse<WiredWebApiKeyRequest, WiredWebApiKeyResult>(
        ApiIds.WiredWebApiKeyGenerate,
        "Generate Wired Web API key",
        "Generates a key and waits for a result matching both Wired ID and read/write access. The key is not retained in room state.",
        MessageKeys.Wired.WebApi.KeyGenerate,
        MessageKeys.Wired.WebApi.KeyResult,
        [
            new("wired_id", typeof(Id), true, null, "The Wired furniture ID.", IdConstraint()),
            new("read_key", typeof(bool), true, null, "True for a read key; false for a write key."),
            TimeoutParameter()
        ],
        WriteHints(true, false));

    public static ApiDescriptor PreferencesSet { get; } = SendCall<WiredPreferencesSetRequest>(
        ApiIds.WiredPreferencesSet,
        "Set Wired preferences",
        "Updates Wired menu, inspection, play-test, whisper, notification and UI preferences.",
        MessageKeys.Wired.Room.PreferencesUpdate,
        [new("preferences", typeof(WiredSetPreferences), true, null, "Complete Wired preference set.")],
        WriteHints(false, true));

    public static ApiDescriptor ChestOpen { get; } = SendCall<WiredChestRequest>(
        ApiIds.WiredChestOpen,
        "Open Wired chest",
        "Requests the current contents of one Wired chest.",
        MessageKeys.Wired.Chests.OpenRequest,
        [ChestIdParameter()],
        ReadHints());

    public static ApiDescriptor ChestClose { get; } = SendCall<WiredChestRequest>(
        ApiIds.WiredChestClose,
        "Close Wired chest",
        "Closes one Wired chest for the active session.",
        MessageKeys.Wired.Chests.Close,
        [ChestIdParameter()],
        WriteHints(false, true));

    public static ApiDescriptor ChestsLock { get; } = SendCall<WiredChestsLockRequest>(
        ApiIds.WiredChestsLock,
        "Lock Wired chests",
        "Locks or unlocks the selected chest scope in the room.",
        MessageKeys.Wired.Chests.LockAll,
        [
            new("locked", typeof(bool), true, null, "Whether the target chests are locked."),
            new("apply_to_all_in_room", typeof(bool), false, false, "Apply to every chest in the room.")
        ],
        WriteHints(true, true));

    public static ApiDescriptor ChestUpgrade { get; } = SendCall<WiredChestUpgradeRequest>(
        ApiIds.WiredChestUpgrade,
        "Upgrade Wired chest",
        "Purchases one or more capacity upgrades for a Wired chest.",
        MessageKeys.Wired.Chests.Upgrade,
        [
            new("chest_id", typeof(int), true, null, "Positive chest identifier.", new(Minimum: 1)),
            new("upgrade_amount", typeof(int), true, null, "Number of upgrades to buy.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApiDescriptor ChestWithdrawAll { get; } = SendCall<WiredChestRequest>(
        ApiIds.WiredChestWithdrawAll,
        "Withdraw all from Wired chest",
        "Withdraws every available item or coin from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawAll,
        [ChestIdParameter()],
        WriteHints(true, false));

    public static ApiDescriptor ChestWithdrawCoins { get; } = SendCall<
        WiredChestCoinsWithdrawRequest>(
        ApiIds.WiredChestWithdrawCoins,
        "Withdraw coins from Wired chest",
        "Withdraws a positive coin amount from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawCoins,
        [
            ChestIdParameter(),
            new("coin_amount", typeof(int), true, null, "Positive number of coins to withdraw.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApiDescriptor ChestWithdrawItems { get; } = SendCall<
        WiredChestItemsWithdrawRequest>(
        ApiIds.WiredChestWithdrawItems,
        "Withdraw items from Wired chest",
        "Withdraws a furniture type and count from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawItems,
        [
            ChestIdParameter(),
            new("item_type", typeof(ChestItemType), true, null, "Exact furniture type descriptor."),
            new("count", typeof(int), true, null, "Positive number of items to withdraw.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApiDescriptor ChestAddStart { get; } = SendCall<WiredChestRequest>(
        ApiIds.WiredChestAddStart,
        "Start adding to Wired chest",
        "Starts the chest-backed Wired trade used to deposit inventory items.",
        MessageKeys.Wired.Chests.StartAdding,
        [ChestIdParameter()],
        WriteHints(false, false));

    public static ApiDescriptor ChestOptionsSet { get; } = SendCall<
        WiredChestOptionsSetRequest>(
        ApiIds.WiredChestOptionsSet,
        "Set Wired chest options",
        "Updates lock, auto-lock and capacity options for one Wired chest.",
        MessageKeys.Wired.Chests.OptionsUpdate,
        [new("options", typeof(SetChestOptions), true, null, "Complete chest option set.")],
        WriteHints(true, true));

    public static ApiDescriptor ChestPreferencesSet { get; } = SendCall<
        WiredChestPreferencesSetRequest>(
        ApiIds.WiredChestPreferencesSet,
        "Set Wired chest preferences",
        "Updates the name, description, state and preview preferences for one Wired chest.",
        MessageKeys.Wired.Chests.PreferencesUpdate,
        [new("preferences", typeof(SetChestPreferences), true, null, "Complete chest preference set.")],
        WriteHints(true, true));

    public static ApiDescriptor ChestNotificationPreferencesSet { get; } = SendCall<
        WiredChestNotificationPreferencesSetRequest>(
        ApiIds.WiredChestNotificationPreferencesSet,
        "Set Wired chest notification preferences",
        "Updates notification and event flags for one Wired chest.",
        MessageKeys.Wired.Chests.NotificationPreferencesUpdate,
        [new("preferences", typeof(SetChestNotificationPreferences), true, null, "Complete chest notification preference set.")],
        WriteHints(false, true));

    public static ApiDescriptor ChestDeposit { get; } = Call<
        WiredChestDepositRequest,
        WiredChestDepositResult>(
        ApiIds.WiredChestDeposit,
        "Deposit into Wired chest",
        "Runs one exclusive session-bound chest trade through open, offer, acceptance, confirmation and exact contents update.",
        [
            ChestIdParameter(),
            new("inventory_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct non-zero signed 32-bit inventory item identifiers.", new(MinItems: 1, MaxItems: 1000)),
            TimeoutParameter(30000)
        ],
        [
            Send(MessageKeys.Wired.Chests.OpenRequest),
            Observe(MessageKeys.Wired.Chests.ItemsChunk),
            Send(MessageKeys.Wired.Chests.Close),
            Send(MessageKeys.Wired.Chests.StartAdding),
            Observe(MessageKeys.Wired.Trade.Initiated),
            Send(MessageKeys.Wired.Trade.ItemsUpdate),
            Observe(MessageKeys.Wired.Trade.ItemsUpdated),
            Send(MessageKeys.Wired.Trade.Confirm),
            Observe(MessageKeys.Wired.Trade.Completed),
            Observe(MessageKeys.Wired.Trade.Cancelled),
            Observe(MessageKeys.Wired.Transaction.Failed),
            Observe(MessageKeys.Wired.Chests.ItemsUpdated),
            Send(MessageKeys.Wired.Trade.Cancel)
        ],
        WriteHints(true, false));

    public static ApiDescriptor TransactionChestLogsGet { get; } = RequestResponse<
        WiredTransactionChestLogsRequest,
        WiredTransactionLogList>(
        ApiIds.WiredTransactionChestLogsGet,
        "Get Wired chest transactions",
        "Returns one exactly correlated page of transactions for a chest log list.",
        MessageKeys.Wired.Transaction.ChestLogsRequest,
        MessageKeys.Wired.Transaction.Logs,
        TransactionPagingParameters(true),
        ReadHints());

    public static ApiDescriptor TransactionRoomLogsGet { get; } = RequestResponse<
        WiredTransactionRoomLogsRequest,
        WiredTransactionLogList>(
        ApiIds.WiredTransactionRoomLogsGet,
        "Get Wired room transactions",
        "Returns one exactly correlated page of room-wide Wired transactions.",
        MessageKeys.Wired.Transaction.RoomLogsRequest,
        MessageKeys.Wired.Transaction.Logs,
        TransactionPagingParameters(false),
        ReadHints());

    public static ApiDescriptor TransactionDetailsGet { get; } = RequestResponse<
        WiredTransactionDetailsRequest,
        WiredTransactionLogDetails>(
        ApiIds.WiredTransactionDetailsGet,
        "Get Wired transaction details",
        "Returns the exact chest and furniture details for one 64-bit transaction identifier.",
        MessageKeys.Wired.Transaction.LogDetailsRequest,
        MessageKeys.Wired.Transaction.LogDetails,
        [
            new("transaction_id", typeof(long), true, null, "Positive 64-bit transaction identifier.", new(Minimum: 1)),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApiDescriptor ContractOpen { get; } = RequestResponse<
        WiredContractOpenRequest,
        WiredContractContents>(
        ApiIds.WiredContractOpen,
        "Open Wired contract",
        "Requests and returns the exact contents of one Wired contract on verified Flash layouts.",
        MessageKeys.Wired.Contracts.OpenRequest,
        MessageKeys.Wired.Contracts.Contents,
        [ContractIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor ContractOpenSend { get; } = SendCall<WiredContractOpenSendRequest>(
        ApiIds.WiredContractOpenSend,
        "Send Wired contract open",
        "Requests one Wired contract without waiting for its contents.",
        MessageKeys.Wired.Contracts.OpenRequest,
        [ContractIdParameter()],
        ReadHints());

    public static ApiDescriptor ContractUpdate { get; } = RequestResponse<
        WiredContractUpdateRequest,
        WiredContractUpdateResult>(
        ApiIds.WiredContractUpdate,
        "Update Wired contract",
        "Replaces one Wired contract and waits for the verified Flash result.",
        MessageKeys.Wired.Contracts.Update,
        MessageKeys.Wired.Contracts.UpdateResult,
        [
            new("contract", typeof(WiredContractContents), true, null, "Complete contract definition."),
            TimeoutParameter()
        ],
        WriteHints(true, true));

    public static ApiDescriptor ContractUpdateSend { get; } = SendCall<
        WiredContractSendRequest>(
        ApiIds.WiredContractUpdateSend,
        "Send Wired contract update",
        "Sends a complete contract update without waiting for its result.",
        MessageKeys.Wired.Contracts.Update,
        [new("contract", typeof(WiredContractContents), true, null, "Complete contract definition.")],
        WriteHints(true, true));

    public static ApiDescriptor TradeItemsAdd { get; } = SendCall<WiredTradeItemsRequest>(
        ApiIds.WiredTradeItemsAdd,
        "Add Wired trade items",
        "Adds distinct inventory items to the active Wired chest trade.",
        MessageKeys.Wired.Trade.ItemsUpdate,
        [InventoryIdsParameter()],
        WriteHints(false, false));

    public static ApiDescriptor TradeItemsRemove { get; } = SendCall<WiredTradeItemsRequest>(
        ApiIds.WiredTradeItemsRemove,
        "Remove Wired trade items",
        "Removes distinct inventory items from the active Wired chest trade.",
        MessageKeys.Wired.Trade.ItemsUpdate,
        [InventoryIdsParameter()],
        WriteHints(false, false));

    public static ApiDescriptor TradeComplete { get; } = Call<WiredTradeCompleteRequest, WiredTradeCompleteResult>(
        ApiIds.WiredTradeComplete,
        "Complete Wired trade",
        "Accepts the reviewed offer, waits three seconds, verifies it is unchanged, confirms and waits for completion.",
        [
            new("expected_generation", typeof(long), true, null, "Wired generation from the reviewed state.", new(Minimum: 0)),
            new("expected_revision", typeof(long), true, null, "Wired revision from the reviewed state.", new(Minimum: 0)),
            TimeoutParameter(30000)
        ],
        [Send(MessageKeys.Wired.Trade.Confirm), Observe(MessageKeys.Wired.Trade.Initiated),
            Observe(MessageKeys.Wired.Trade.ItemsUpdated), Observe(MessageKeys.Wired.Trade.Completed),
            Observe(MessageKeys.Wired.Trade.Cancelled), Observe(MessageKeys.Wired.Transaction.Failed),
            Send(MessageKeys.Wired.Trade.Cancel)],
        WriteHints(true, false));

    public static ApiDescriptor TradeStageSend { get; } = SendCall<WiredTradeStageRequest>(
        ApiIds.WiredTradeStageSend,
        "Send Wired trade confirmation stage",
        "Sends Accept first or Confirm after the countdown; does not wait for completion.",
        MessageKeys.Wired.Trade.Confirm,
        [new("stage", typeof(WiredTradeConfirmationStage), true, null, "Accept for initial acceptance; Confirm for final confirmation.")],
        WriteHints(true, true));

    public static ApiDescriptor TradeConfirm { get; } = SendCall<WiredTradeConfirmRequest>(
        ApiIds.WiredTradeConfirm,
        "Confirm Wired trade",
        "Sends one confirmation stage: false accepts initially; true confirms after the countdown. Prefer wired.trade.complete for the full sequence.",
        MessageKeys.Wired.Trade.Confirm,
        [new("confirm", typeof(bool), false, true, "False for initial acceptance; true for final confirmation after the countdown. True alone can fail with Invalid trade.")],
        WriteHints(false, true));

    public static ApiDescriptor TradeCancel { get; } = SendCall<WiredCommandRequest>(
        ApiIds.WiredTradeCancel,
        "Cancel Wired trade",
        "Cancels the active Wired chest trade.",
        MessageKeys.Wired.Trade.Cancel,
        [],
        WriteHints(true, true));

    public static ApiDescriptor Changed { get; } = Event<WiredChanged>(
        ApiIds.WiredChanged,
        "Wired state changed",
        "Publishes every ordered immutable Wired state revision, including reset.",
        StateMessages());

    public static ApiDescriptor PermissionsChanged { get; } = Event<WiredEvent<WiredPermissions>>(
        ApiIds.WiredPermissionsChanged,
        "Wired permissions changed",
        "Publishes the active user's latest Wired read and modify permissions.",
        [Observe(MessageKeys.Wired.State.Permissions)]);

    public static ApiDescriptor EnvironmentChanged { get; } = Event<WiredEvent<WiredEnvironment>>(
        ApiIds.WiredEnvironmentChanged,
        "Wired environment changed",
        "Publishes the room's Wired environment and enabled achievements.",
        [Observe(MessageKeys.Wired.State.Environment)]);

    public static ApiDescriptor ClickSettingsChanged { get; } = Event<WiredEvent<WiredClickSettings>>(
        ApiIds.WiredClickSettingsChanged,
        "Wired click settings changed",
        "Publishes the latest Wired user and furniture click options.",
        [Observe(MessageKeys.Wired.State.ClickSettings)]);

    public static ApiDescriptor RoomSettingsChanged { get; } = Event<WiredEvent<WiredRoomSettings>>(
        ApiIds.WiredRoomSettingsChanged,
        "Wired room settings changed",
        "Publishes the latest room permission masks and Wired timezone.",
        [Observe(MessageKeys.Wired.Room.Settings)]);

    public static ApiDescriptor ConfigurationOpened { get; } = Event<WiredEvent<Id>>(
        ApiIds.WiredConfigurationOpened,
        "Wired configuration opened",
        "Publishes the furni identifier named by each configuration-open handshake.",
        [Observe(MessageKeys.Wired.Configuration.Opened)]);

    public static ApiDescriptor ConfigurationReceived { get; } = Event<
        WiredEvent<WiredConfigurationSnapshot>>(
        ApiIds.WiredConfigurationReceived,
        "Wired configuration received",
        "Publishes immutable complete configurations for every supported Wired kind.",
        [
            Observe(MessageKeys.Wired.Configuration.Trigger, false),
            Observe(MessageKeys.Wired.Configuration.Action, false),
            Observe(MessageKeys.Wired.Configuration.Condition, false),
            Observe(MessageKeys.Wired.Configuration.Selector, false),
            Observe(MessageKeys.Wired.Configuration.Addon, false),
            Observe(MessageKeys.Wired.Configuration.Variable, false)
        ]);

    public static ApiDescriptor ConfigurationSaveResult { get; } = Event<
        WiredEvent<WiredConfigurationSaveResult>>(
        ApiIds.WiredConfigurationSaveResult,
        "Wired configuration save result",
        "Publishes each successful save or exact validation failure.",
        [
            Observe(MessageKeys.Wired.Configuration.SaveSucceeded),
            Observe(MessageKeys.Wired.Configuration.ValidationFailed)
        ]);

    public static ApiDescriptor MenuError { get; } = Event<WiredEvent<WiredMenuError>>(
        ApiIds.WiredMenuError,
        "Wired menu error",
        "Publishes Wired menu error codes reported by the hotel.",
        [Observe(MessageKeys.Wired.State.MenuError)]);

    public static ApiDescriptor RewardResult { get; } = Event<WiredEvent<WiredRewardResult>>(
        ApiIds.WiredRewardResult,
        "Wired reward result",
        "Publishes each Wired reward result reason.",
        [Observe(MessageKeys.Wired.State.RewardResult)]);

    public static ApiDescriptor ChestOpened { get; } = Event<WiredEvent<OpenChest>>(
        ApiIds.WiredChestOpened,
        "Wired chest opened",
        "Publishes hotel requests to open a Wired chest.",
        [Observe(MessageKeys.Wired.Chests.Opened)]);

    public static ApiDescriptor ChestCoinsReceived { get; } = Event<WiredEvent<CoinsChestContents>>(
        ApiIds.WiredChestCoinsReceived,
        "Wired chest coins received",
        "Publishes complete coin contents for one Wired chest.",
        [Observe(MessageKeys.Wired.Chests.Coins)]);

    public static ApiDescriptor ChestItemsChunkReceived { get; } = Event<
        WiredEvent<WiredChestItemsChunkSnapshot>>(
        ApiIds.WiredChestItemsChunkReceived,
        "Wired chest item chunk received",
        "Publishes one immutable item-content fragment for a Wired chest.",
        [Observe(MessageKeys.Wired.Chests.ItemsChunk)]);

    public static ApiDescriptor ChestItemsUpdated { get; } = Event<
        WiredEvent<WiredChestItemsUpdatedSnapshot>>(
        ApiIds.WiredChestItemsUpdated,
        "Wired chest items updated",
        "Publishes exact removed identifiers and added immutable chest storage entries.",
        [Observe(MessageKeys.Wired.Chests.ItemsUpdated)]);

    public static ApiDescriptor ChestUpgradeResult { get; } = Event<WiredEvent<UpgradeChestResult>>(
        ApiIds.WiredChestUpgradeResult,
        "Wired chest upgrade result",
        "Publishes each chest capacity-upgrade result.",
        [Observe(MessageKeys.Wired.Chests.UpgradeResult)]);

    public static ApiDescriptor ChestPreferencesUpdated { get; } = Event<
        WiredEvent<ChestPreferencesUpdateSuccess>>(
        ApiIds.WiredChestPreferencesUpdated,
        "Wired chest preferences updated",
        "Publishes confirmed chest preference and notification-preference updates.",
        [Observe(MessageKeys.Wired.Chests.PreferencesUpdated)]);

    public static ApiDescriptor TransactionSucceeded { get; } = Event<
        WiredEvent<WiredTransactionSuccess>>(
        ApiIds.WiredTransactionSucceeded,
        "Wired transaction succeeded",
        "Publishes successful Wired transaction outcomes and optional rewards.",
        [Observe(MessageKeys.Wired.Transaction.Succeeded)]);

    public static ApiDescriptor TransactionFailed { get; } = Event<
        WiredEvent<WiredTransactionFail>>(
        ApiIds.WiredTransactionFailed,
        "Wired transaction failed",
        "Publishes Wired transaction failure type identifiers.",
        [Observe(MessageKeys.Wired.Transaction.Failed)]);

    public static ApiDescriptor ContractContentsReceived { get; } = Event<
        WiredEvent<WiredContractContents>>(
        ApiIds.WiredContractContentsReceived,
        "Wired contract contents received",
        "Publishes complete immutable Wired contract definitions.",
        [Observe(MessageKeys.Wired.Contracts.Contents)]);

    public static ApiDescriptor ContractOpened { get; } = Event<WiredEvent<WiredOpenContract>>(
        ApiIds.WiredContractOpened,
        "Wired contract opened",
        "Publishes contract-editor open requests from the hotel.",
        [Observe(MessageKeys.Wired.Contracts.Opened)]);

    public static ApiDescriptor ContractUpdateResult { get; } = Event<
        WiredEvent<WiredContractUpdateResult>>(
        ApiIds.WiredContractUpdateResult,
        "Wired contract update result",
        "Publishes success and failure codes for contract updates.",
        [Observe(MessageKeys.Wired.Contracts.UpdateResult)]);

    public static ApiDescriptor TradeInitiated { get; } = Event<WiredEvent<WiredTradeInitiate>>(
        ApiIds.WiredTradeInitiated,
        "Wired trade initiated",
        "Publishes the exact requirement and timeout for a new Wired chest trade.",
        [Observe(MessageKeys.Wired.Trade.Initiated)]);

    public static ApiDescriptor TradeItemsUpdated { get; } = Event<
        WiredEvent<WiredTradingItemsSnapshot>>(
        ApiIds.WiredTradeItemsUpdated,
        "Wired trade items updated",
        "Publishes immutable item offers and acceptance readiness for the active Wired trade.",
        [Observe(MessageKeys.Wired.Trade.ItemsUpdated)]);

    public static ApiDescriptor TradeCancelled { get; } = Event<WiredEvent<WiredTradeCancelled>>(
        ApiIds.WiredTradeCancelled,
        "Wired trade cancelled",
        "Publishes the hotel failure type for a cancelled Wired trade.",
        [Observe(MessageKeys.Wired.Trade.Cancelled)]);

    public static ApiDescriptor TradeCompleted { get; } = Event<WiredEvent<WiredTradeCompleted>>(
        ApiIds.WiredTradeCompleted,
        "Wired trade completed",
        "Publishes completion of the active Wired trade.",
        [Observe(MessageKeys.Wired.Trade.Completed)]);

    public static ApiDescriptor TradeNotification { get; } = Event<
        WiredEvent<WiredTradeTransactionNotification>>(
        ApiIds.WiredTradeNotification,
        "Wired trade notification",
        "Publishes Wired trade transaction notification identifiers.",
        [Observe(MessageKeys.Wired.Trade.Notification)]);

    private static ApiDescriptor Save<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        Type update_type) => Call<TRequest, WiredConfigurationSaveResult>(
        id,
        title,
        description,
        [
            new("update", update_type, true, null, "Complete replacement configuration."),
            TimeoutParameter()
        ],
        [
            Send(request_key),
            Observe(MessageKeys.Wired.Configuration.SaveSucceeded),
            Observe(MessageKeys.Wired.Configuration.ValidationFailed)
        ],
        WriteHints(true, true));

    private static ApiDescriptor RequestResponse<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey response_key,
        IReadOnlyList<ApiParameter> parameters,
        ApiToolHints hints) => Call<TRequest, TResult>(
        id,
        title,
        description,
        parameters,
        [Send(request_key), Observe(response_key)],
        hints);

    private static ApiDescriptor Call<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        IReadOnlyList<ApiMessageRequirement> messages,
        ApiToolHints hints) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TRequest),
        typeof(TResult),
        parameters,
        [GameRequirement.HotelConnected, GameRequirement.RoomActive],
        messages: messages,
        toolHints: hints);

    private static ApiDescriptor SendCall<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey message,
        IReadOnlyList<ApiParameter> parameters,
        ApiToolHints hints) => Call<TRequest, WiredDispatchResult>(
        id,
        title,
        description,
        parameters,
        [Send(message)],
        hints);

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

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiParameter TimeoutParameter(int default_value = 10000) => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        default_value,
        "Total maximum time for the session-bound operation.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter VariableIdParameter() => new(
        "variable_id",
        typeof(string),
        true,
        null,
        "Non-empty Wired variable identifier.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue));

    private static ApiParameter EntityTypeParameter() => new(
        "entity_type",
        typeof(int),
        true,
        null,
        "Hotel entity type identifier.");

    private static ApiParameter EntityIdParameter() => new(
        "entity_id",
        typeof(int),
        true,
        null,
        "Positive entity identifier.",
        new(Minimum: 1));

    private static ApiParameter ChestIdParameter() => new(
        "chest_id",
        typeof(Id),
        true,
        null,
        "Positive Wired chest identifier.",
        IdConstraint());

    private static ApiParameter ContractIdParameter() => new(
        "contract_id",
        typeof(int),
        true,
        null,
        "Positive Wired contract identifier.",
        new(Minimum: 1));

    private static ApiParameter InventoryIdsParameter() => new(
        "inventory_ids",
        typeof(IReadOnlyList<Id>),
        true,
        null,
        "Distinct non-zero signed 32-bit inventory item identifiers.",
        new(MinItems: 1, MaxItems: 1000));

    private static ApiParameter[] VariableSetParameters(bool permanent) =>
    [
        permanent
            ? EntityTypeParameter()
            : new("target", typeof(WiredTarget), true, null, "Furni, user or global variable target."),
        permanent
            ? EntityIdParameter()
            : new("object_id", typeof(int), true, null, "Furni identifier, room user index or zero for global scope."),
        VariableIdParameter(),
        new("value", typeof(int), true, null, "Integer variable value; ignored for delete operations."),
        new("operation", typeof(int), false, WiredVariableOperation.Write, "Zero writes, one creates and two deletes.", new(Minimum: 0, Maximum: 2))
    ];

    private static ApiParameter[] TransactionPagingParameters(bool chest)
    {
        var values = new List<ApiParameter>();
        if (chest)
        {
            values.Add(new(
                "log_list_id",
                typeof(int),
                true,
                null,
                "Positive chest transaction-log list identifier.",
                new(Minimum: 1)));
        }
        values.Add(new("page", typeof(int), false, 1, "One-based transaction page.", new(Minimum: 1)));
        values.Add(new("page_size", typeof(int), false, 50, "Transaction rows requested.", new(Minimum: 1, Maximum: 250)));
        values.Add(TimeoutParameter());
        return values.ToArray();
    }

    private static ApiConstraints IdConstraint() =>
        new(Pattern: @"^[1-9][0-9]*$");

    private static ApiConstraints TextConstraint() =>
        new(MaxUtf8Bytes: ushort.MaxValue);

    private static ApiToolHints ReadHints() => new(true, false, true, true);

    private static ApiToolHints WriteHints(bool destructive, bool idempotent) =>
        new(false, destructive, idempotent, true);

    private static ApiMessageRequirement[] StateMessages() =>
    [
        Observe(MessageKeys.Wired.State.Permissions, false),
        Observe(MessageKeys.Wired.State.Environment, false),
        Observe(MessageKeys.Wired.State.ClickSettings, false),
        Observe(MessageKeys.Wired.State.MenuError, false),
        Observe(MessageKeys.Wired.State.RewardResult, false),
        Observe(MessageKeys.Wired.Configuration.Opened, false),
        Observe(MessageKeys.Wired.Configuration.Trigger, false),
        Observe(MessageKeys.Wired.Configuration.Action, false),
        Observe(MessageKeys.Wired.Configuration.Condition, false),
        Observe(MessageKeys.Wired.Configuration.Selector, false),
        Observe(MessageKeys.Wired.Configuration.Addon, false),
        Observe(MessageKeys.Wired.Configuration.Variable, false),
        Observe(MessageKeys.Wired.Configuration.SaveSucceeded, false),
        Observe(MessageKeys.Wired.Configuration.ValidationFailed, false),
        Observe(MessageKeys.Wired.Room.Settings, false),
        Observe(MessageKeys.Wired.Chests.Opened, false),
        Observe(MessageKeys.Wired.Chests.Coins, false),
        Observe(MessageKeys.Wired.Chests.ItemsChunk, false),
        Observe(MessageKeys.Wired.Chests.ItemsUpdated, false),
        Observe(MessageKeys.Wired.Chests.UpgradeResult, false),
        Observe(MessageKeys.Wired.Chests.PreferencesUpdated, false),
        Observe(MessageKeys.Wired.Transaction.Succeeded, false),
        Observe(MessageKeys.Wired.Transaction.Failed, false),
        Observe(MessageKeys.Wired.Contracts.Contents, false),
        Observe(MessageKeys.Wired.Contracts.Opened, false),
        Observe(MessageKeys.Wired.Contracts.UpdateResult, false),
        Observe(MessageKeys.Wired.Trade.Initiated, false),
        Observe(MessageKeys.Wired.Trade.ItemsUpdated, false),
        Observe(MessageKeys.Wired.Trade.Cancelled, false),
        Observe(MessageKeys.Wired.Trade.Completed, false),
        Observe(MessageKeys.Wired.Trade.Notification, false)
    ];
}
