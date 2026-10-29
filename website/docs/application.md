# Game API

Game operations have stable IDs such as `room.chat.talk`. Native extensions reach them through
`host.Api`. In scripts, `Api` exposes the same operations with the script's cancellation and
subscription lifetime.

## Call a member

`ApiIds` names every member. `InvokeAsync` takes the member's request and returns its
result:

```csharp
await Api.InvokeAsync<ProfileUserRequest, ProfileDispatchResult>(
    ApiIds.ProfileBlockAdd,
    new ProfileUserRequest(12345));
```

The request and result types must be the ones the member declares; any other pair throws an
`ArgumentException` that names the right ones. A member that is not available in the current
session, for example without a room, throws `GameNotReadyException`.

## Subscribe to an event

```csharp
Api.Subscribe<WalletChanged>(ApiIds.WalletChanged, change =>
    Log($"credits: {change.Credits}"));
```

Handlers added through a script's `Api` are removed when the script stops, and calls without a
cancellation token stop with the script. Native extensions own their subscriptions and cancellation
tokens; dispose subscriptions when their consumer closes.

## Ids and tools

`Api.Describe(id)` returns a member's title, request, result and availability. The same
members are MCP tools named `application_` plus the id with dots turned into underscores, so
`room.chat.talk` is `application_room_chat_talk`. The MCP tool `describe_application_member`
shows the C# call for any member.
