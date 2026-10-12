using GoldsrcFramework.Ecs;

namespace GoldsrcFramework.Rendering;

/// <summary>
/// The studio renderer that draws physics-driven entities: a ragdoll corpse, or a server entity whose
/// animation drives a physics skeleton, is drawn from the pose that skeleton carries rather than from
/// its animation.
/// </summary>
/// <remarks>
/// <para>
/// It replaces the animation pipeline and nothing else. Everything up to and including the model
/// resolution stays in <see cref="StudioModelRenderer.StudioDrawModel"/>, and the drawing of a pose
/// that is already in place stays in <see cref="StudioModelRenderer.StudioDrawPhysModel"/>; what is
/// overridden here is only the decision of which pose to draw - <c>StudioDrawModelEx</c>, the seam
/// between the two.
/// </para>
/// <para>
/// It has to reach the runtime - which entity has a pose to give, and whether an entity has been
/// replaced by a corpse - and it is called straight out of the engine's draw loop, where there is no
/// registry to resolve anything from. The game is therefore handed over whole at construction and
/// asked through its rendering callbacks; nothing here knows what a ragdoll or a physics skeleton is.
/// </para>
/// <para>
/// <b>Players are deliberately left alone.</b> <c>StudioDrawPlayer</c> is not overridden: the player
/// renderer carries the gait and prediction logic, and the third person pose has no physics skeleton
/// to read back yet. A player model is drawn by the base class even when it is drawn through this
/// one.
/// </para>
/// </remarks>
internal sealed class PhyStudioModelRenderer : StudioModelRenderer
{
    private readonly GoldsrcClientGame game;

    /// <param name="game">The client game the pose and the corpse state are asked of.</param>
    public PhyStudioModelRenderer(GoldsrcClientGame game) => this.game = game;

    /// <summary>
    /// Chooses the pose this entity is drawn from: none at all when a corpse replaced it, the physics
    /// skeleton when there is one, and its animation otherwise.
    /// </summary>
    /// <remarks>
    /// An entity a corpse has replaced is not drawn, but is still sent through the animation pipeline
    /// with the render bit cleared: attachment points and client events are part of what the rest of
    /// the engine expects from drawing it, and skipping the draw while keeping those is what the
    /// reference implementation does too.
    /// </remarks>
    protected unsafe override bool StudioDrawModelEx(int flags)
    {
        var entity = m_pCurrentEntity;

        if (game.IsReplacedByCorpse(entity))
        {
            base.StudioDrawModelEx(flags & ~STUDIO_RENDER);
            return false;
        }

        // The pose is only worth asking for when this entity has one to give; when it turns out not
        // to, the model is simply drawn from its animation as any other.
        if (m_pStudioHeader != null && game.IsPhysAnimating(entity))
        {
            var boneCapacity = Math.Min(m_pStudioHeader->numbones, StudioConstants.MAXSTUDIOBONES);

            if (game.TryFillPhysicsPose(entity, m_protationmatrix, m_pbonetransform, boneCapacity))
                return StudioDrawPhysModel(flags);
        }

        return base.StudioDrawModelEx(flags);
    }
}
