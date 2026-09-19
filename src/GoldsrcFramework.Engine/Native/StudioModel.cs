namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// A loaded GoldSrc studio model (<c>*.mdl</c>), lending out the engine-owned studio model header.
/// </summary>
/// <remarks>
/// <para>
/// This is the handle callers get from the content manager
/// (<c>IContentManager.Load&lt;StudioModel&gt;("models/player/gordon.mdl")</c>). It is intentionally a
/// thin wrapper and nothing more: it adds no interpretation on top of the header, so anything derived
/// from the model - the bone hierarchy, hitboxes, sequences - is read by the caller from
/// <see cref="Header"/>.
/// </para>
/// <para>
/// <b>Lifetime</b>: <see cref="Header"/> points into the engine's model cache, and the engine can free
/// or reload that model (map change, model eviction). The pointer is not refcounted and this type does
/// not keep the model alive, so hold it only as long as the engine keeps the model loaded, and re-read
/// it from the content manager instead of caching it across maps.
/// </para>
/// </remarks>
public sealed unsafe class StudioModel
{
    /// <summary>
    /// The engine-owned studio model header (<c>studiohdr_t*</c>) - the root of every studio array
    /// (bones, sequences, body parts, hitboxes, ...).
    /// </summary>
    /// <remarks>
    /// Read-only: the header is engine data. What it points at is not owned or guarded by this type,
    /// see the lifetime remarks on <see cref="StudioModel"/>.
    /// </remarks>
    public studiohdr_t* Header { get; }

    /// <summary>
    /// Wraps a studio model header obtained from the engine, e.g. through
    /// <c>engine_studio_api_t.Mod_Extradata</c>.
    /// </summary>
    /// <param name="header">The engine-owned header. Must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
    public StudioModel(studiohdr_t* header)
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
    }
}
