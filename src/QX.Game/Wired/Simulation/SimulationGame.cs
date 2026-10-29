using Qx.Model.Wired;

namespace Qx.Game.Wired.Simulation;

public sealed partial class Simulator
{
    private SimulationGamePhase game_phase;
    private readonly Dictionary<long, SimulationScoreUse> score_uses = [];
    private bool score_uses_known;
    private readonly HashSet<long> fired_scores = [];
    private bool score_triggers_known;

    private static int? team_score_variable(string name) => name switch
    {
        "@teams.red.score" => 1, "@teams.green.score" => 2, "@teams.blue.score" => 3, "@teams.yellow.score" => 4, _ => null
    };

    private long known_team_score(int team)
    {
        if (team_scores.TryGetValue(team, out long score)) return score;
        unsupported_variable("team score without TeamScores metadata");
        return 0;
    }

    private SimulationValue? user_team_value(SimulationUser user, string name)
    {
        if (user.Team == 0) return null;
        if (user.Team is < 1 or > 4) unsupported_variable("unknown user team type");
        return new(name switch { "@team.color" => user.Team, "@team.type" => 4, _ => known_team_score(user.Team) });
    }

    private static SimulationGame? copy_game(SimulationGame? game) => game is null ? null :
        game with { ScoreUses = game.ScoreUses?.ToArray(), FiredScoreTriggers = game.FiredScoreTriggers?.ToArray() };

    private void restore_game()
    {
        if (initial.Game is not { } game) return;
        if (!Enum.IsDefined(game.Phase) || !furniture.ContainsKey(game.ClockId))
            throw new ArgumentException("Game metadata requires an existing source clock and a known game phase.");
        game_phase = game.Phase;
        score_uses_known = game.ScoreUses is not null;
        score_triggers_known = game.FiredScoreTriggers is not null;
        foreach (long id in game.FiredScoreTriggers ?? [])
        {
            if (!nodes.TryGetValue(id, out var trigger) || trigger.Category != WiredFormCategory.Trigger ||
                trigger.Name != "ScoreAchieved" || !fired_scores.Add(id))
                throw new ArgumentException("Invalid or duplicate fired score trigger metadata.");
        }
        foreach (var use in game.ScoreUses ?? [])
        {
            if (use is null || use.Count < 0 || !nodes.TryGetValue(use.ActionId, out var action) ||
                action.Category != WiredFormCategory.Action || action.Name is not ("GiveScore" or "GiveScoreToPredefinedTeam") ||
                use.UserId is { } user && users.TryGetValue(user, out var actor) && (actor.IsBot || actor.IsPet) ||
                use.Team is < 1 or > 4 || use.UserId.HasValue && use.Team.HasValue || !score_uses.TryAdd(use.ActionId, use))
                throw new ArgumentException("Invalid or duplicate game score allowance metadata.");
        }
    }

    private SimulationGame? game_snapshot() => initial.Game is null ? null :
        initial.Game with { Phase = game_phase, ScoreUses = score_uses_known ? score_uses.Values.ToArray() : null,
            FiredScoreTriggers = score_triggers_known ? fired_scores.Order().ToArray() : null };

    private bool score_reached(SimulationNode node, SimulationEvent input)
    {
        if (input.Kind != SimulationEventKind.ScoreChanged || input.NewValue is not { } score || score < node.Number("score") || !score_team(node, input)) return false;
        if (initial.Game is null || !score_triggers_known)
            unsupported(node, "Score thresholds require known Game.FiredScoreTriggers or a simulated fresh game start; each box fires once per game across all eligible teams, even when its conditions fail.");
        return fired_scores.Add(node.FurnitureId);
    }

    private bool game_active(SimulationNode node)
    {
        if (initial.Game is null) unsupported(node, "Scoring requires explicit Game metadata; team scores and a visible clock do not establish whether a game is running.");
        return game_phase != SimulationGamePhase.Stopped;
    }

    private bool score_available(SimulationNode node, long? user = null, int? team = null)
    {
        long maximum = node.Number("per_game_limit");
        if (maximum is < 0 or > 10) unsupported(node, "Score allowance must be unlimited (0) or between 1 and 10.");
        if (maximum == 0) return true;
        if (!score_uses_known) unsupported(node, "Finite scoring requires known per-game ScoreUses, or a simulated fresh game start.");
        if (!score_uses.TryGetValue(node.FurnitureId, out var previous)) return true;
        if (previous.Count == 0) return true;
        if (previous.UserId != user || previous.Team != team)
            unsupported(node, "Score allowance sharing between different users or changed team configurations requires separate conformance; supply verified history for the selected branch.");
        return previous.Count < maximum;
    }

    private void score_used(SimulationNode node, long? user = null, int? team = null)
    {
        if (node.Number("per_game_limit") == 0) return;
        score_uses[node.FurnitureId] = new(node.FurnitureId, score_uses.GetValueOrDefault(node.FurnitureId)?.Count + 1 ?? 1, user, team);
    }

    private long score_before(SimulationNode node, int team)
    {
        if (team is < 1 or > 4 || !team_scores.TryGetValue(team, out long score))
        {
            unsupported(node, "Scoring requires a Wired team from 1 through 4 and its current TeamScores value.");
            return 0;
        }
        return score;
    }

    private long score_after(SimulationNode node, long previous)
    {
        return Math.Max(0, checked(previous + node.Number("points")));
    }

    private void give_score(SimulationNode node, Frame frame, long[] people)
    {
        if (!game_active(node)) return;
        if (people.Length > 1 || people.Any(id => users[id].IsBot || users[id].IsPet))
            unsupported(node, "Scoring multiple users, bots or pets in one action requires separate conformance.");
        foreach (long id in people)
        {
            var user = users[id];
            if (!score_available(node, user: id)) continue;
            if (user.Team == 0 || game_phase == SimulationGamePhase.Paused)
            {
                score_used(node, user: id);
                continue;
            }
            long previous = score_before(node, user.Team), next = score_after(node, previous);
            int personal = checked(user.Score + (int)node.Number("points"));
            users[id] = user with { Score = personal };
            set_team_score(user.Team, next);
            score_used(node, user: id);
            enqueue(now, frame with { Event = new(SimulationEventKind.ScoreChanged, now, id, OldValue: previous, NewValue: next, Team: user.Team) }, external: true);
        }
    }

    private void give_team_score(SimulationNode node, Frame frame)
    {
        if (!game_active(node)) return;
        int team = checked((int)node.Number("team"));
        if (!score_available(node, team: team)) return;
        if (game_phase == SimulationGamePhase.Paused)
        {
            score_used(node, team: team);
            return;
        }
        long previous = score_before(node, team), next = score_after(node, previous);
        set_team_score(team, next);
        score_used(node, team: team);
        enqueue(now, frame with { Event = new(SimulationEventKind.ScoreChanged, now, OldValue: previous, NewValue: next, Team: team) }, external: true);
    }

    private void leave_team(SimulationNode node, long[] people)
    {
        if (people.All(id => users[id].Team == 0)) return;
        if (initial.Game is null) unsupported(node, "Leaving a Wired team requires explicit game phase metadata.");
        if (game_phase == SimulationGamePhase.Stopped) return;
        if (people.Any(id => users[id].IsBot || users[id].IsPet)) unsupported(node, "Bot and pet team membership requires separate conformance.");
        foreach (long id in people) users[id] = users[id] with { Team = 0 };
    }

    private void game_notice(SimulationEventKind kind, long clock)
    {
        var notice = new Frame(++root_sequence, new(kind, now, FurnitureId: clock), new(StringComparer.Ordinal), [], [], [], GameNotification: true);
        enqueue(now, notice, external: true);
        record("game.lifecycle", clock, kind.ToString());
    }

    private void start_game(long clock, bool notify)
    {
        if (game_phase != SimulationGamePhase.Stopped && notify) game_notice(SimulationEventKind.GameEnds, clock);
        game_phase = SimulationGamePhase.Running;
        score_uses.Clear();
        score_uses_known = true;
        fired_scores.Clear();
        score_triggers_known = true;
        for (int team = 1; team <= 4; team++) team_scores[team] = 0;
        team_order = null;
        foreach (var user in users.Values.ToArray()) users[user.Id] = user with { Score = 0 };
        if (notify) game_notice(SimulationEventKind.GameStarts, clock);
    }

    private void end_game(long clock, bool notify)
    {
        bool active = game_phase != SimulationGamePhase.Stopped;
        game_phase = SimulationGamePhase.Stopped;
        if (active && notify) game_notice(SimulationEventKind.GameEnds, clock);
    }

    private bool game_clock(SimulationFurniture item, SimulationNode node)
    {
        if (initial.Game?.ClockId == item.Id) return true;
        if (item.ClassName == "wf_game_upcounter1") unsupported(node, "A game-aware clock requires explicit Game metadata naming that clock. Multiple game counters require separate conformance.");
        return false;
    }

    private void game_clock_control(SimulationNode node, long clock)
    {
        switch (node.Number("clock_control"))
        {
            case 0: start_game(clock, true); break;
            case 1: case 2: end_game(clock, true); break;
            case 3:
                if (game_phase == SimulationGamePhase.Running) game_phase = SimulationGamePhase.Paused;
                break;
            case 4:
                if (game_phase == SimulationGamePhase.Paused) game_phase = SimulationGamePhase.Running;
                break;
        }
    }
}
