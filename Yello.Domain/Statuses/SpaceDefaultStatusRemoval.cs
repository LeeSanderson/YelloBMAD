namespace Yello.Domain.Statuses;

/// <summary>
/// Plans the removal of one Space default across every Project of the Space, and judges it by the set each Project would
/// then hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every set, before and after, comes from <see cref="EffectiveStatusSet.Of(IEnumerable{StatusDefinition}, ProjectStatusDelta)"/></b>:
/// the after-set is the defaults without the Status and the delta without every operation naming it, with the places it
/// anchored re-anchored by <see cref="EffectiveStatusSet.Reanchored"/>. So the rest of each Project's order and every name
/// stay as they were, and the planned places are exactly the rows the store writes.
/// </para>
/// <para>
/// <b>Nothing is placed for the Admin.</b> A Project whose set after the removal lacks the Space-wide destination and that
/// has Tasks to move must be given a destination of its own; without one the whole removal is refused, never resolved to
/// some Status the Admin did not choose. A Project with no Task in the Status has nothing to place and is never asked.
/// </para>
/// <para>
/// <b>Never empty, at either level.</b> The last default is never removed, and neither is a default that is the only Status
/// some Project still holds: no destination exists there, so the Admin's way out is to give that Project another Status
/// first.
/// </para>
/// </remarks>
public static class SpaceDefaultStatusRemoval
{
    /// <summary>
    /// The Projects whose set holds <paramref name="statusId"/>: the ones whose Tasks lock the removal must hold.
    /// </summary>
    /// <param name="defaults">The Space's defaults, in Space order.</param>
    /// <param name="deltas">Every Project's delta, keyed by Project; a Project that changed nothing is absent.</param>
    /// <param name="projectIds">Every Project of the Space.</param>
    /// <param name="statusId">The default being removed.</param>
    /// <returns>The holders.</returns>
    public static IReadOnlyList<Guid> Holders(
        IReadOnlyList<StatusDefinition> defaults,
        IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas,
        IReadOnlyCollection<Guid> projectIds,
        Guid statusId)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(projectIds);

        return [.. projectIds.Where(projectId => EffectiveStatusSet.Contains(EffectiveStatusSet.Of(defaults, DeltaOf(deltas, projectId)), statusId))];
    }

    /// <summary>
    /// Plans the removal, in a fixed order of decisions: not a default; the last default; a Project left empty; an invalid
    /// Space-wide destination; occupied with no destination; an invalid or repeated exception; a blocked Project with no
    /// exception.
    /// </summary>
    /// <param name="space">The defaults, every delta, every Project and the occupancy, all read under the removal's locks.</param>
    /// <param name="request">The Status, the Space-wide destination and the exceptions.</param>
    /// <param name="identifiers">Produces the ids of the re-anchored places.</param>
    /// <param name="now">Stamps the re-anchored places.</param>
    /// <returns>The plan, or why the removal cannot be made.</returns>
    public static SpaceDefaultStatusRemovalPlan Plan(
        SpaceStatusRemovalState space,
        SpaceStatusRemovalRequest request,
        IIdentifierGenerator identifiers,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);

        var statusId = request.StatusId;

        if (space.Defaults.FirstOrDefault(status => status.Id == statusId) is not { } removed)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.NotFound);
        }

        var defaultsAfter = space.Defaults.Where(status => status.Id != statusId).ToList();

        if (SpaceDefaultStatusPlan.CheckDefaults(defaultsAfter) == SpaceDefaultStatusVerdict.SetEmpty)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.SetEmpty);
        }

        var projects = space.ProjectIds
            .Select(projectId => Judge(space, defaultsAfter, projectId, removed, identifiers, now))
            .OfType<ProjectRemoval>()
            .ToList();

        var leftEmpty = projects.Where(project => EffectiveStatusSet.Check(project.After, namedStatusId: null) == StatusDeltaVerdict.SetEmpty).ToList();

        if (leftEmpty.Count > 0)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.SetEmpty) with { Projects = [.. leftEmpty.Select(project => project.ProjectId)] };
        }

        return Decide(defaultsAfter, projects, request);
    }

    private static SpaceDefaultStatusRemovalPlan Decide(List<StatusDefinition> defaultsAfter, List<ProjectRemoval> projects, SpaceStatusRemovalRequest request)
    {
        var destination = request.DestinationStatusId;

        if (destination is { } chosen && !defaultsAfter.Exists(status => status.Id == chosen))
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.DestinationInvalid);
        }

        var occupied = projects.Where(project => project.TaskCount > 0).ToList();

        if (destination is null && occupied.Count > 0)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.DestinationRequired) with { Report = [.. occupied.Select(Occupancy)] };
        }

        return request.Exceptions.GroupBy(exception => exception.ProjectId).Any(group => group.Count() > 1)
            ? SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.DestinationInvalid)
            : Mapped(projects, occupied, request);
    }

    /// <summary>
    /// The plan once every Project's destination is known, or the Projects whose own destination is missing or invalid.
    /// </summary>
    /// <remarks>
    /// An exception for a Project that can take the Space-wide destination, or for no Project of this Space, is ignored: the
    /// Space-wide destination is the Admin's choice wherever it can apply, and ignoring a foreign id answers exactly as
    /// ignoring a random one.
    /// </remarks>
    private static SpaceDefaultStatusRemovalPlan Mapped(List<ProjectRemoval> projects, List<ProjectRemoval> occupied, SpaceStatusRemovalRequest request)
    {
        var destination = request.DestinationStatusId;
        var blocked = occupied.Where(project => destination is not { } wide || !EffectiveStatusSet.Contains(project.After, wide)).ToList();
        var exceptions = request.Exceptions.ToDictionary(exception => exception.ProjectId, exception => exception.DestinationStatusId);
        var invalid = blocked
            .Where(project => exceptions.TryGetValue(project.ProjectId, out var own) && !EffectiveStatusSet.Contains(project.After, own))
            .Select(project => project.ProjectId)
            .ToList();

        if (invalid.Count > 0)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.DestinationInvalid) with { Projects = invalid };
        }

        var unanswered = blocked.Where(project => !exceptions.ContainsKey(project.ProjectId)).ToList();

        if (unanswered.Count > 0)
        {
            return SpaceDefaultStatusRemovalPlan.Refused(SpaceDefaultStatusVerdict.ProjectDestinationRequired) with { Report = [.. unanswered.Select(Occupancy)] };
        }

        var blockedIds = blocked.Select(project => project.ProjectId).ToHashSet();

        return new SpaceDefaultStatusRemovalPlan(SpaceDefaultStatusVerdict.Admitted)
        {
            StatusId = request.StatusId,
            Migrations =
            [
                .. occupied.Select(project => new SpaceStatusMigration(
                    project.ProjectId,
                    blockedIds.Contains(project.ProjectId) ? exceptions[project.ProjectId] : destination!.Value)),
            ],
            Places = [.. projects.Where(project => project.Places is not null).Select(project => new SpaceStatusReanchoring(project.ProjectId, project.Places!))],
        };
    }

    /// <summary>
    /// One Project's part in the removal, or <c>null</c> when its set does not hold the Status: a Project that removed it is
    /// unaffected, its <c>Remove</c> going with the definition.
    /// </summary>
    private static ProjectRemoval? Judge(
        SpaceStatusRemovalState space,
        IReadOnlyList<StatusDefinition> defaultsAfter,
        Guid projectId,
        StatusDefinition removed,
        IIdentifierGenerator identifiers,
        DateTimeOffset now)
    {
        var delta = DeltaOf(space.Deltas, projectId);
        var before = EffectiveStatusSet.Of(space.Defaults, delta);

        if (!EffectiveStatusSet.Contains(before, removed.Id))
        {
            return null;
        }

        var kept = delta.Without(removed.Id);
        IReadOnlyList<StatusDeltaOperation>? places = null;

        if (delta.Operations.Any(operation => operation.Kind == StatusDeltaKind.Place && operation.AfterStatusId == removed.Id))
        {
            var placed = kept.Operations.Where(operation => operation.Kind == StatusDeltaKind.Place).Select(operation => operation.StatusId).ToHashSet();
            var sequence = delta.NextSequence;

            places =
            [
                .. EffectiveStatusSet.Reanchored(before, placed, removed.Id).Select(placement => new StatusDeltaOperation
                {
                    Id = identifiers.Next(),
                    SpaceId = removed.SpaceId,
                    ProjectId = projectId,
                    StatusId = placement.StatusId,
                    Kind = StatusDeltaKind.Place,
                    AfterStatusId = placement.AfterStatusId,
                    Sequence = sequence++,
                    CreatedAt = now,
                }),
            ];
            kept = kept with { Operations = [.. kept.Operations.Where(operation => operation.Kind != StatusDeltaKind.Place), .. places] };
        }

        return new ProjectRemoval(projectId, space.Occupancy.GetValueOrDefault(projectId), EffectiveStatusSet.Of(defaultsAfter, kept), places);
    }

    private static SpaceDefaultStatusOccupancy Occupancy(ProjectRemoval project) => new(project.ProjectId, project.TaskCount, project.After);

    private static ProjectStatusDelta DeltaOf(IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas, Guid projectId) =>
        deltas.GetValueOrDefault(projectId) ?? ProjectStatusDelta.Empty;

    private sealed record ProjectRemoval(Guid ProjectId, int TaskCount, IReadOnlyList<EffectiveStatus> After, IReadOnlyList<StatusDeltaOperation>? Places);
}
