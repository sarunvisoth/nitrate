using Prismedia.Contracts.Plugins;

namespace Prismedia.Infrastructure.Plugins;

/// <summary>
/// Marks an accepted identify proposal tree as organized before apply, so every entity the proposal
/// touches (root, structural children, relationship targets) comes out of the apply flagged
/// organized. Shared by the manual review accept path and the auto identify apply path.
/// </summary>
internal static class AcceptedProposalMarker {
    /// <summary>Returns the proposal with <c>IsOrganized</c> forced true across its whole tree.</summary>
    public static EntityMetadataProposal MarkTreeOrganized(EntityMetadataProposal proposal) {
        var children = (proposal.Children ?? []).Select(MarkTreeOrganized).ToArray();
        var relationships = (proposal.Relationships ?? []).Select(MarkTreeOrganized).ToArray();

        if (proposal.Patch is null) {
            return proposal with {
                Children = children,
                Relationships = relationships
            };
        }

        return proposal with {
            Patch = proposal.Patch with {
                Flags = MarkOrganized(proposal.Patch.Flags)
            },
            Children = children,
            Relationships = relationships
        };
    }

    private static EntityMetadataFlagsPatch MarkOrganized(EntityMetadataFlagsPatch? flags) =>
        flags is null
            ? new EntityMetadataFlagsPatch(null, null, true)
            : flags with { IsOrganized = true };
}
