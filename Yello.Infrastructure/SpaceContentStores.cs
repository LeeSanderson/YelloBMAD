using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Invitations;
using Yello.Application.Memberships;
using Yello.Application.Spaces;
using Yello.Domain.Boards;
using Yello.Domain.Labels;
using Yello.Domain.Lists;
using Yello.Domain.Projects;
using Yello.Domain.Tasks;
using Yello.Infrastructure.Memberships;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers the stores behind a Space's own content. Story 2.1 onwards.
/// </summary>
internal static class SpaceContentStores
{
    /// <summary>
    /// Adds the Project, Task and Label stores, the Board reader and the List reader (story 2.10), the Invitation store
    /// (story 4.1), the Membership store with its <c>MembershipChanged</c> publisher (story 4.4) and the Ownership Offer store.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Scoped with the request's unit of work: every Space-scoped store runs in the transaction the
    /// pipeline opened, under the Space it resolved.
    /// </remarks>
    internal static IServiceCollection AddSpaceContentStores(this IServiceCollection services)
    {
        services.AddScoped<IProjectStore, ProjectStore>();
        services.AddScoped<ITaskStore, TaskStore>();
        services.AddScoped<ITaskAssignments, TaskAssignmentStore>();
        services.AddScoped<IBoardReader, BoardReader>();
        services.AddScoped<IListReader, ListReader>();
        services.AddScoped<ILabelStore, LabelStore>();

        // Story 4.1: the Space's Invitations, on the request's unit of work like every Space-scoped store. Story 4.3: the
        // same instance answers the accept's screen, which the pipeline reads before it consults the bound.
        services.AddScoped<InvitationStore>();
        services.AddScoped<IInvitationStore>(provider => provider.GetRequiredService<InvitationStore>());
        services.AddScoped<IInvitationAcceptanceScreen>(provider => provider.GetRequiredService<InvitationStore>());

        // Story 4.3: the Invitation unit of work - the token resolved to its Space through the policy-free token index, and
        // the SpaceId key alone set to that Space (Lee's Q1 (a) and Q2 (a); the AD-2 / AD-24 amendment of 2026-11-02). The
        // pipeline's Invitation step is its only caller.
        services.AddScoped<IInvitationScope, InvitationScope>();

        // Story 4.4: the Space's Memberships - the list and the Role change - on the request's unit of work, and the
        // MembershipChanged publisher both units of work tell after they commit (AC6, Q7 (a)). No subscriber is registered
        // until stories 7.3 and 7.7.
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddScoped<MembershipChangePublisher>();
        services.AddScoped<IMembershipChanges>(provider => provider.GetRequiredService<MembershipChangePublisher>());

        // The Space's Ownership Offer, on the request's unit of work under the same Memberships lock as a removal.
        services.AddScoped<IOwnershipOfferStore, OwnershipOfferStore>();

        return services;
    }
}
