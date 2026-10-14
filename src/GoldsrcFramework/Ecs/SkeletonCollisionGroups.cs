using Stride.BepuPhysics.Definitions;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Works out which collidables of a physics skeleton may collide with one another, and gives every
/// skeleton an id of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the engine allows.</b> Stride builds its narrow phase callbacks inside
/// <c>BepuSimulation</c> and does not let a game replace them, so the only pair filter a mod can
/// reach is <see cref="CollisionGroup"/>: a contact between two collidables is dropped when they
/// carry the same non-zero <c>Id</c> <i>and</i> all three of their subgroup indices differ by at
/// most one (<c>StrideNarrowPhaseCallbacks.AllowContactGeneration</c>). Layers, triggers and
/// contact handlers all act on one collidable at a time, so none of them can name a pair.
/// </para>
/// <para>
/// <b>What the bones need.</b> The reference implementation runs on Bullet, whose
/// <c>addConstraint(constraint, disableCollisionsBetweenLinkedBodies: true)</c> disables collisions
/// between exactly the two bodies a joint links (<c>gsphysics/PhysicsComponent.cpp</c>). A bone
/// still collides with its neighbour's neighbour, with another limb, and with another corpse -
/// only a bone and the bone it is jointed to are allowed to overlap, which they do by design, the
/// limb capsules modelling the same continuous flesh the joint sits inside.
/// </para>
/// <para>
/// <b>The two filters are the same filter, read differently.</b> The three indices are a cell of an
/// integer grid, "within one on every axis" is the Chebyshev distance, and a pair is stopped
/// exactly when its two cells are one step apart or closer. So a layout that stops exactly the
/// linked pairs is an embedding of the skeleton's joint graph into that grid as an <i>induced</i>
/// subgraph: bodies a joint links must end up one step apart, and no other two bodies may.
/// </para>
/// <para>
/// <b>A group carries two independent things.</b> The indices say which pairs <i>inside</i> one
/// skeleton are stopped and follow from the joint graph alone; the id says which skeletons are
/// compared at all and belongs to one instance. Both are written when a skeleton is loaded
/// (<see cref="PhysicsController.LoadSkeleton"/>), from the joints the skeleton carries then. An id
/// shared between instances - a single one baked into a prefab, which is what the framework used to
/// have - filters those instances against each other wherever their bones line up, which is why two
/// corpses used to pass through one another.
/// </para>
/// <para>
/// <b>When no layout exists.</b> Not every graph fits: a cell has 26 neighbours, and one bone's
/// neighbours must be two steps apart from each other as well, so the search gives up on a skeleton
/// far bushier than a ragdoll. The fallback is the group that filters everything - every body at
/// subgroup zero, under the id of that instance - which is what every skeleton did before any of
/// this existed, so such a ragdoll holds together instead of blasting itself apart. A layout that is
/// found is exact, never approximate: the search only accepts a cell that keeps every body already
/// placed at its proper distance from the new one, so every pair is weighed when the later of its
/// two bodies is placed.
/// </para>
/// <para>
/// <b>Checked rather than assumed</b> - <c>artifacts/_verify_collision_groups.py</c> walks a
/// <c>.gpd</c> the same way and confirms that every pair of its rigid bodies is filtered exactly
/// when a joint links it.
/// </para>
/// </remarks>
internal static class SkeletonCollisionGroups
{
    /// <summary>
    /// The id a group carries while no instance has stamped one of its own: what a bare prefab is
    /// built with, and what a layout hands back.
    /// </summary>
    /// <remarks>
    /// Non-zero on purpose. A prefab that reaches a simulation without a
    /// <see cref="PhysicsController"/> then still does not let one skeleton collide with itself, and
    /// a laid-out group that is used as it comes back filters a skeleton rather than letting its
    /// overlapping limb capsules push it apart.
    /// </remarks>
    internal const ushort PrefabGroupId = 1;

    /// <summary>
    /// The steps a joint may take in the index grid: the 26 cells around a cell, in a fixed order so
    /// that a layout is reproducible.
    /// </summary>
    private static readonly (int X, int Y, int Z)[] Steps = BuildSteps();

    /// <summary>Placements the search may spend on one body, and on a whole skeleton.</summary>
    /// <remarks>
    /// A budget rather than a time limit, so the layout does not depend on how loaded the machine
    /// is. Generous for the skeletons that exist: a shipped ragdoll resolves within a few thousand
    /// placements, in under a millisecond (<c>artifacts/_probe_layout</c>). The cap is what a model
    /// with hundreds of rigid bodies is held to, the search running while the model is being built
    /// and a frame must not be held for long - the trade being a fully filtered skeleton, which is
    /// the layout that needs no search at all.
    /// </remarks>
    private const int PlacementsPerBody = 4096;
    private const int MaxPlacements = 50_000;

    /// <summary>Slot the first bone of a component is placed at; every other slot has a parent.</summary>
    private const int NoParent = -1;

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Collision group per collidable of a skeleton, in the order the collidables were handed over:
    /// the prefab id, and the subgroup indices that stop only the bones a joint links.
    /// </summary>
    /// <param name="bodyCount">Number of collidables the skeleton carries.</param>
    /// <param name="joints">Pairs of collidable indices a joint links.</param>
    /// <remarks>
    /// Collidables no joint reaches - a skeleton that carries no joint at all, or one whose graph
    /// falls into several pieces - are laid out as well, each piece starting clear of the last, so
    /// that a collidable is never left in the same cell as another by accident.
    /// </remarks>
    internal static CollisionGroup[] Layout(int bodyCount, ReadOnlySpan<(int BodyA, int BodyB)> joints)
    {
        var groups = new CollisionGroup[bodyCount];
        for (var body = 0; body < bodyCount; body++)
            groups[body].Id = PrefabGroupId;

        if (bodyCount <= 1)
            return groups;

        var neighbours = BuildAdjacency(bodyCount, joints);

        // Bodies in the order the search may start from them: the busiest first, that being where a
        // graph has the most of itself to fit around one cell. Walking the list this way reaches
        // every component of a graph that falls into several.
        var roots = new int[bodyCount];
        for (var body = 0; body < bodyCount; body++)
            roots[body] = body;
        Array.Sort(roots, (left, right) => neighbours[right].Length.CompareTo(neighbours[left].Length));

        // One budget for the whole skeleton, shared by every attempt at it, so that trying more
        // starting points widens the search instead of lengthening its worst case.
        var budget = Math.Min(bodyCount * PlacementsPerBody, MaxPlacements);
        var cells = new Cell[bodyCount];
        var placed = new bool[bodyCount];
        var placedCount = 0;
        var rightMost = 0;

        foreach (var root in roots)
        {
            if (placed[root])
                continue;

            // A component the walk has not covered yet. It starts two cells clear of everything
            // placed so far, which the filter cannot reach across, and every bone of it is a cell the
            // search may start from - a graph that has no layout around one bone may well have one
            // around another.
            var origin = new Cell(placedCount == 0 ? 0 : rightMost + 2, 0, 0);
            var component = ComponentOf(neighbours, root);
            var laidOut = false;

            foreach (var candidate in component)
            {
                if (budget <= 0)
                    break;

                var search = new LayoutSearch(neighbours, candidate, origin, budget);
                if (!search.TryPlaceAll(cells))
                {
                    budget = search.BudgetLeft;
                    continue;
                }

                foreach (var bone in search.Bones)
                {
                    placed[bone] = true;
                    placedCount++;
                    rightMost = Math.Max(rightMost, cells[bone].X);
                }

                laidOut = true;
                break;
            }

            if (!laidOut)
                return groups; // no layout: the whole skeleton is filtered, as it was before
        }

        // The indices are unsigned, so the layout is moved into the positive octant. Only the
        // distances between the cells mean anything, and a move keeps every one of them.
        var minX = cells.Min(cell => cell.X);
        var minY = cells.Min(cell => cell.Y);
        var minZ = cells.Min(cell => cell.Z);

        for (var body = 0; body < bodyCount; body++)
        {
            groups[body].IndexA = (ushort)(cells[body].X - minX);
            groups[body].IndexB = (ushort)(cells[body].Y - minY);
            groups[body].IndexC = (ushort)(cells[body].Z - minZ);
        }

        return groups;
    }

    /// <summary>
    /// A collision group id no other skeleton of this process is holding.
    /// </summary>
    /// <remarks>
    /// Handed out in a cycle, skipping zero - the id that filters nothing, which everything outside a
    /// skeleton keeps (brush geometry, the worldspawn, props) and which a skeleton of a single
    /// collidable is given, there being no pair inside it to stop. Two skeletons would have to land
    /// on the same id to stop colliding with each other, which takes a full cycle of 65535 skeletons
    /// loaded into one session.
    /// </remarks>
    internal static ushort NextId()
    {
        var value = (uint)Interlocked.Increment(ref nextId);
        return (ushort)(value % ushort.MaxValue + 1);
    }

    private static int nextId;

    // ── Graph ─────────────────────────────────────────────────────────────────

    /// <summary>Bodies each body is jointed to, indexed by body.</summary>
    private static int[][] BuildAdjacency(int bodyCount, ReadOnlySpan<(int BodyA, int BodyB)> joints)
    {
        var neighbours = new List<int>[bodyCount];
        for (var body = 0; body < bodyCount; body++)
            neighbours[body] = [];

        foreach (var (bodyA, bodyB) in joints)
        {
            if ((uint)bodyA >= (uint)bodyCount || (uint)bodyB >= (uint)bodyCount || bodyA == bodyB)
                continue;

            if (!neighbours[bodyA].Contains(bodyB))
            {
                neighbours[bodyA].Add(bodyB);
                neighbours[bodyB].Add(bodyA);
            }
        }

        var result = new int[bodyCount][];
        for (var body = 0; body < bodyCount; body++)
            result[body] = [.. neighbours[body]];

        return result;
    }

    /// <summary>The bodies reachable from one, busiest first.</summary>
    private static int[] ComponentOf(int[][] neighbours, int root)
    {
        var component = new List<int>(neighbours.Length) { root };
        var seen = new bool[neighbours.Length];
        seen[root] = true;

        for (var slot = 0; slot < component.Count; slot++)
        {
            foreach (var neighbour in neighbours[component[slot]])
            {
                if (seen[neighbour])
                    continue;

                seen[neighbour] = true;
                component.Add(neighbour);
            }
        }

        var result = component.ToArray();
        Array.Sort(result, (left, right) => neighbours[right].Length.CompareTo(neighbours[left].Length));
        return result;
    }

    private static (int X, int Y, int Z)[] BuildSteps()
    {
        var steps = new List<(int X, int Y, int Z)>(26);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    if (x != 0 || y != 0 || z != 0)
                        steps.Add((x, y, z));
                }
            }
        }

        return [.. steps];
    }

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>One cell of the index grid.</summary>
    private readonly record struct Cell(int X, int Y, int Z)
    {
        public static Cell operator +(Cell cell, (int X, int Y, int Z) step)
            => new(cell.X + step.X, cell.Y + step.Y, cell.Z + step.Z);

        /// <summary>Whether the two are one step apart or closer - the filter's own test.</summary>
        public bool IsWithinOneStepOf(Cell other)
            => Math.Abs(X - other.X) <= 1 && Math.Abs(Y - other.Y) <= 1 && Math.Abs(Z - other.Z) <= 1;
    }

    /// <summary>
    /// The placement search for one component of a skeleton: the bones in the order they are placed,
    /// the cell each one was given, and what is left of the budget.
    /// </summary>
    /// <remarks>
    /// Bones are placed breadth-first from the root, each in a cell next to the bone it is jointed
    /// to, and a bone that runs out of cells gives the placement of the bone before it back: a cell
    /// that looks free can still leave a later bone nowhere to go, and only trying again tells the
    /// two apart.
    /// </remarks>
    private sealed class LayoutSearch
    {
        private readonly int[][] neighbours;
        private readonly int[] order;      // bones in placement order; the component's root first
        private readonly int[] parentSlot; // per slot, the slot of the bone it is placed against
        private readonly Cell[] cells;     // cell per body, meaningful once the body is placed
        private readonly bool[] placed;    // bodies of this component, whether or not placed yet
        private readonly int count;
        private int budget;

        public LayoutSearch(int[][] neighbours, int root, Cell origin, int budget)
        {
            this.neighbours = neighbours;
            this.budget = budget;

            cells = new Cell[neighbours.Length];
            placed = new bool[neighbours.Length];
            order = new int[neighbours.Length];
            parentSlot = new int[neighbours.Length];

            order[0] = root;
            parentSlot[0] = NoParent;
            placed[root] = true;
            count = 1;

            // Breadth first: a skeleton is a tree, so a bone is discovered through the bone it hangs
            // off, and that discovery order becomes the order the cells are handed out in - which is
            // what makes the cell of the bone a new one is placed against already known.
            for (var slot = 0; slot < count; slot++)
            {
                foreach (var neighbour in neighbours[order[slot]])
                {
                    if (placed[neighbour])
                        continue;

                    placed[neighbour] = true;
                    parentSlot[count] = slot;
                    order[count++] = neighbour;
                }
            }

            cells[root] = origin;
        }

        /// <summary>The bodies of this component.</summary>
        public ReadOnlySpan<int> Bones => order.AsSpan(0, count);

        /// <summary>Placements this search did not spend, for the caller to hand to the next one.</summary>
        public int BudgetLeft => budget;

        /// <summary>
        /// Places every bone of the component, writing the cells into <paramref name="result"/>.
        /// False when the budget ran out with bones left unplaced, in which case nothing is written.
        /// </summary>
        public bool TryPlaceAll(Cell[] result)
        {
            if (!Place(1))
                return false;

            foreach (var bone in Bones)
                result[bone] = cells[bone];

            return true;
        }

        private bool Place(int slot)
        {
            if (slot == count)
                return true;
            if (budget-- <= 0)
                return false;

            var parent = cells[order[parentSlot[slot]]];
            Span<int> candidates = stackalloc int[Steps.Length];
            Span<int> room = stackalloc int[Steps.Length];
            var candidateCount = 0;

            for (var step = 0; step < Steps.Length; step++)
            {
                var cell = parent + Steps[step];
                if (!IsFree(cell, slot))
                    continue;

                candidates[candidateCount] = step;
                room[candidateCount] = FreeNeighbours(cell, slot);
                candidateCount++;
            }

            // Roomiest first. A cell whose own neighbourhood is still empty leaves somewhere for the
            // bones below this one to go, and that is the difference between a layout found at once
            // and a search that has to take the whole component back apart.
            SortByRoomDescending(candidates, room, candidateCount);

            for (var candidate = 0; candidate < candidateCount; candidate++)
            {
                cells[order[slot]] = parent + Steps[candidates[candidate]];

                if (Place(slot + 1))
                    return true;
            }

            cells[order[slot]] = default;
            return false;
        }

        /// <summary>
        /// Whether a cell may hold the bone at <paramref name="slot"/>: one step from the bones it is
        /// jointed to, and two or more from every other bone already placed.
        /// </summary>
        /// <remarks>
        /// That is the layout condition itself, stated pair by pair, so a layout this accepts is
        /// exact by construction - every pair of the component is weighed exactly once, when the
        /// later of its two bodies is placed.
        /// </remarks>
        private bool IsFree(Cell cell, int slot)
        {
            var jointedTo = neighbours[order[slot]];

            for (var other = 0; other < slot; other++)
            {
                if (cells[order[other]].IsWithinOneStepOf(cell) != Jointed(jointedTo, order[other]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a body is one of the bones a list holds. Written out rather than asked of the
        /// list: this is the search's innermost test, and the framework's other lists are asked with
        /// <c>Contains</c>, which goes through an interface and costs an order of magnitude more over
        /// the millions of pairs a stubborn skeleton makes it weigh.
        /// </summary>
        private static bool Jointed(int[] bones, int bone)
        {
            foreach (var candidate in bones)
            {
                if (candidate == bone)
                    return true;
            }

            return false;
        }

        /// <summary>Cells around one that no placed bone has taken, for the candidate order.</summary>
        private int FreeNeighbours(Cell cell, int slot)
        {
            var free = 0;

            foreach (var step in Steps)
            {
                var neighbour = cell + step;
                var taken = false;

                for (var other = 0; other < slot && !taken; other++)
                    taken = cells[order[other]] == neighbour;

                if (!taken)
                    free++;
            }

            return free;
        }

        private static void SortByRoomDescending(Span<int> candidates, Span<int> room, int count)
        {
            for (var i = 1; i < count; i++)
            {
                var candidate = candidates[i];
                var value = room[i];
                var j = i - 1;

                while (j >= 0 && room[j] < value)
                {
                    candidates[j + 1] = candidates[j];
                    room[j + 1] = room[j];
                    j--;
                }

                candidates[j + 1] = candidate;
                room[j + 1] = value;
            }
        }
    }
}
