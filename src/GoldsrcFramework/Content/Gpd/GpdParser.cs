using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using GoldsrcFramework.LinearMath;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;

namespace GoldsrcFramework.Content.Gpd;

/// <summary>
/// Strict reader for the legacy <c>.gpd</c> physics construction data of a studio model.
/// </summary>
/// <remarks>
/// <para>
/// The format is the XML schema used by <c>gsphysics</c> and the mods derived from it:
/// </para>
/// <code>
/// &lt;goldsrc-physics-data checksum="..."&gt;
///   &lt;collision-shape-block&gt;  &lt;collision-shape&gt; &lt;sub-collision-shape type="..."&gt;
///   &lt;rigidbody-block&gt;        &lt;rigidbody bone=".." shape=".." type=".."&gt; &lt;local&gt;
///   &lt;constraint-block&gt;       &lt;constraint rba=".." rbb=".." type=".."&gt; &lt;locala&gt; &lt;localb&gt;
/// &lt;/goldsrc-physics-data&gt;
/// </code>
/// <para>
/// Everything is validated before anything is built, so a file that parses is a file that can be
/// turned into a physics skeleton. Unknown shapes and constraint types make the asset invalid
/// instead of being skipped, because a silently partial ragdoll is worse than a rejected one.
/// </para>
/// <para>
/// Numbers are parsed with <see cref="CultureInfo.InvariantCulture"/> and split on the same
/// separator set gsphysics uses, so a file parses identically under any process culture. Transforms
/// use the GoldSrc column-vector layout (translation in the last column) and map onto
/// <see cref="Matrix3x4"/> directly: only the first twelve of the sixteen values are kept, the
/// homogeneous row is read but not stored.
/// </para>
/// </remarks>
public static class GpdParser
{
    /// <summary>Root element of the legacy format.</summary>
    public const string RootElement = "goldsrc-physics-data";

    /// <summary>Length of the hexadecimal SHA-1 the <c>checksum</c> attribute holds.</summary>
    public const int ChecksumLength = 40;

    private const int MaxShapes = 64;
    private const int MaxSubShapesPerShape = 32;
    private const int MaxRigidBodies = 256;
    private const int MaxConstraints = 1024;
    private const int MaxBoneIndex = 512;
    private const int MaxTextLength = 4096;

    /// <summary>
    /// Separators gsphysics tokenizes vector and matrix text with. Whitespace is included, so the
    /// multi-line matrices of real files split the same way as comma separated ones.
    /// </summary>
    private static readonly char[] FieldSeparators = [' ', ',', '\n', '\r', '\t', ';'];

    /// <summary>
    /// Parses a legacy <c>.gpd</c> document.
    /// </summary>
    /// <param name="xml">Raw file bytes, byte order mark included.</param>
    /// <param name="asset">The parsed asset when this returns true, otherwise null.</param>
    /// <param name="error">Empty on success, otherwise the first validation failure.</param>
    /// <returns>False on any structural, index or numeric problem.</returns>
    public static bool TryParse(byte[] xml, out GpdAsset? asset, out string error)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var parser = new Parser();
        asset = parser.Parse(xml);
        error = parser.Error ?? string.Empty;
        return asset is not null;
    }

    /// <summary>
    /// Parse state for one document. A single instance is used so every helper can report through
    /// one error slot instead of threading it through each signature.
    /// </summary>
    private sealed class Parser
    {
        public string? Error;

        public GpdAsset? Parse(byte[] xml)
        {
            XDocument document;
            try
            {
                // DTD processing is prohibited and the resolver is disabled, so a document can neither
                // pull in external entities nor expand into more content than the bytes contain.
                using var stream = new MemoryStream(xml, writable: false);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                });
                document = XDocument.Load(reader, LoadOptions.None);
            }
            catch (XmlException exception)
            {
                Report($"malformed XML: {exception.Message}");
                return null;
            }

            var root = document.Root;
            if (root is null || root.Name.LocalName != RootElement)
            {
                Report($"the root element must be <{RootElement}>");
                return null;
            }

            string? checksum = (string?)root.Attribute("checksum");
            if (!IsValidChecksum(checksum))
            {
                Report("the checksum attribute must be a 40 character hexadecimal SHA-1");
                return null;
            }

            if (!TryReadShapes(root.Element("collision-shape-block"), out var shapes))
                return null;
            if (!TryReadRigidBodies(root.Element("rigidbody-block"), shapes, out var rigidBodies))
                return null;

            // A shapes-and-bodies only document describes a single body prop, which is the one case
            // where the legacy constraint block may be absent. Everything else is required.
            if (!TryReadConstraints(root.Element("constraint-block"), rigidBodies, out var constraints))
                return null;

            return new GpdAsset
            {
                Checksum = checksum!,
                Shapes = shapes,
                RigidBodies = rigidBodies,
                Constraints = constraints,
            };
        }

        // ── Blocks ────────────────────────────────────────────────────────────

        private bool TryReadShapes(XElement? block, out GpdShape[] shapes)
        {
            shapes = [];

            if (block is null)
                return Fail("the <collision-shape-block> element is missing");

            var elements = block.Elements("collision-shape").Take(MaxShapes + 1).ToArray();
            if (elements.Length == 0)
                return Fail("<collision-shape-block> holds no <collision-shape>");
            if (elements.Length > MaxShapes)
                return Fail($"more than {MaxShapes} <collision-shape> elements");

            var result = new GpdShape[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                var subElements = elements[i]
                    .Elements("sub-collision-shape")
                    .Take(MaxSubShapesPerShape + 1)
                    .ToArray();

                if (subElements.Length == 0)
                    return Fail($"collision shape {i} holds no <sub-collision-shape>");
                if (subElements.Length > MaxSubShapesPerShape)
                    return Fail($"collision shape {i} holds more than {MaxSubShapesPerShape} sub-shapes");

                var subShapes = new GpdSubShape[subElements.Length];
                for (int j = 0; j < subElements.Length; j++)
                {
                    if (!TryReadSubShape(subElements[j], i, j, out subShapes[j]))
                        return false;
                }

                result[i] = new GpdShape(subShapes);
            }

            shapes = result;
            return true;
        }

        private bool TryReadSubShape(XElement element, int shapeIndex, int subShapeIndex, out GpdSubShape subShape)
        {
            subShape = null!;
            string where = $"collision shape {shapeIndex} sub-shape {subShapeIndex}";

            string? type = (string?)element.Attribute("type");
            if (type is null)
                return Fail($"{where} has no type attribute");

            // The local pose defaults to identity: the shipped models never author one.
            var local = Matrix3x4.Identity;
            if (!TryReadTransform(element, "local", required: false, out local, out _))
                return false;

            switch (type)
            {
                case "primitive.box":
                {
                    if (!TryReadVector(element, "halfextent", out var halfExtent))
                        return false;

                    if (halfExtent.X <= 0.0f || halfExtent.Y <= 0.0f || halfExtent.Z <= 0.0f)
                        return Fail($"{where} has a non-positive halfextent");

                    subShape = new GpdSubShape
                    {
                        ShapeType = GpdShapeType.Box,
                        HalfExtent = halfExtent,
                        LocalTransform = local,
                    };
                    return true;
                }

                case "primitive.capsule":
                {
                    if (!TryReadFloat(element, "radius", out float radius))
                        return false;
                    if (!TryReadFloat(element, "height", out float height))
                        return false;

                    if (radius <= 0.0f)
                        return Fail($"{where} has a non-positive radius");

                    // The legacy height is the total end-to-end height, both spherical caps included.
                    float cylinderLength = height - (2.0f * radius);
                    if (cylinderLength <= 0.0f)
                        return Fail($"{where} has a height that does not exceed its diameter");

                    subShape = new GpdSubShape
                    {
                        ShapeType = GpdShapeType.Capsule,
                        Radius = radius,
                        CylinderLength = cylinderLength,
                        LocalTransform = local,
                    };
                    return true;
                }

                case "primitive.other":
                    return Fail($"{where} uses the unsupported shape 'primitive.other'");

                default:
                    return Fail($"{where} uses the unknown shape '{type}'");
            }
        }

        private bool TryReadRigidBodies(XElement? block, GpdShape[] shapes, out GpdRigidBody[] rigidBodies)
        {
            rigidBodies = [];

            if (block is null)
                return Fail("the <rigidbody-block> element is missing");

            var elements = block.Elements("rigidbody").Take(MaxRigidBodies + 1).ToArray();
            if (elements.Length == 0)
                return Fail("<rigidbody-block> holds no <rigidbody>");
            if (elements.Length > MaxRigidBodies)
                return Fail($"more than {MaxRigidBodies} <rigidbody> elements");

            var result = new GpdRigidBody[elements.Length];
            var seenBones = new HashSet<int>();

            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];

                if (!TryReadIntAttribute(element, "bone", out int boneIndex))
                    return Fail($"rigidbody {i} has no valid bone attribute");
                if ((uint)boneIndex >= MaxBoneIndex)
                    return Fail($"rigidbody {i} refers to bone {boneIndex}, which is out of range");
                if (!seenBones.Add(boneIndex))
                    return Fail($"bone {boneIndex} carries more than one rigidbody");

                if (!TryReadIntAttribute(element, "shape", out int shapeIndex))
                    return Fail($"rigidbody {i} has no valid shape attribute");
                if ((uint)shapeIndex >= (uint)shapes.Length)
                    return Fail($"rigidbody {i} refers to collision shape {shapeIndex}, which does not exist");

                if (!TryReadIntAttribute(element, "type", out int userType))
                    return Fail($"rigidbody {i} has no valid type attribute");
                if (userType is not ((int)GpdRigidBodyType.None) and not ((int)GpdRigidBodyType.Addon))
                    return Fail($"rigidbody {i} uses the unknown body type {userType}");

                if (!TryReadTransform(element, "local", required: true, out var boneToRigid, out _))
                    return false;

                result[i] = new GpdRigidBody(
                    boneIndex,
                    shapeIndex,
                    (GpdRigidBodyType)userType,
                    boneToRigid);
            }

            rigidBodies = result;
            return true;
        }

        private bool TryReadConstraints(XElement? block, GpdRigidBody[] rigidBodies, out GpdConstraint[] constraints)
        {
            constraints = [];

            if (block is null)
                return true; // a single body prop needs no joints

            var elements = block.Elements("constraint").Take(MaxConstraints + 1).ToArray();
            if (elements.Length > MaxConstraints)
                return Fail($"more than {MaxConstraints} <constraint> elements");

            var result = new GpdConstraint[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];

                if (!TryReadIntAttribute(element, "rba", out int bodyA))
                    return Fail($"constraint {i} has no valid rba attribute");
                if (!TryReadIntAttribute(element, "rbb", out int bodyB))
                    return Fail($"constraint {i} has no valid rbb attribute");

                // rba/rbb index the rigidbody block, not the bone table.
                if ((uint)bodyA >= (uint)rigidBodies.Length || (uint)bodyB >= (uint)rigidBodies.Length)
                    return Fail($"constraint {i} refers to a rigidbody that does not exist");

                string? type = (string?)element.Attribute("type");
                GpdConstraintType constraintType;
                switch (type)
                {
                    case "spherical": constraintType = GpdConstraintType.Spherical; break;
                    case "hinge": constraintType = GpdConstraintType.Hinge; break;
                    case "cone": constraintType = GpdConstraintType.Cone; break;
                    default: return Fail($"constraint {i} uses the unknown type '{type ?? "<missing>"}'");
                }

                if (!TryReadTransform(element, "locala", required: true, out var frameInA, out _))
                    return false;
                if (!TryReadTransform(element, "localb", required: true, out var frameInB, out _))
                    return false;

                if (!TryReadFloat(element, "low", out float low, required: false))
                    return false;
                if (!TryReadFloat(element, "high", out float high, required: false))
                    return false;
                if (!TryReadFloat(element, "twistspan", out float twistSpan, required: false))
                    return false;
                if (!TryReadFloat(element, "swingspan1", out float swingSpan1, required: false))
                    return false;
                if (!TryReadFloat(element, "swingspan2", out float swingSpan2, required: false))
                    return false;

                bool isAddon =
                    rigidBodies[bodyA].Type == GpdRigidBodyType.Addon ||
                    rigidBodies[bodyB].Type == GpdRigidBodyType.Addon;

                result[i] = new GpdConstraint(
                    bodyA,
                    bodyB,
                    constraintType,
                    frameInA,
                    frameInB,
                    low,
                    high,
                    twistSpan,
                    swingSpan1,
                    swingSpan2,
                    isAddon);
            }

            constraints = result;
            return true;
        }

        // ── Field readers ─────────────────────────────────────────────────────

        /// <summary>
        /// Reads a 4x4 transform from the child element <paramref name="name"/> and keeps its first
        /// twelve values.
        /// </summary>
        private bool TryReadTransform(
            XElement parent,
            string name,
            bool required,
            out Matrix3x4 transform,
            out bool present)
        {
            transform = Matrix3x4.Identity;
            present = false;

            var element = parent.Element(name);
            if (element is null)
            {
                if (required)
                    return Fail($"<{parent.Name.LocalName}> is missing its <{name}> transform");
                return true;
            }

            present = true;

            Span<float> values = stackalloc float[16];
            if (!TryReadNumbers(element.Value, name, values, out int count))
                return false;
            if (count != 16)
                return Fail($"<{name}> must hold exactly 16 numbers but holds {count}");

            transform = new Matrix3x4
            {
                M11 = values[0], M12 = values[1], M13 = values[2], M14 = values[3],
                M21 = values[4], M22 = values[5], M23 = values[6], M24 = values[7],
                M31 = values[8], M32 = values[9], M33 = values[10], M34 = values[11],
            };
            return true;
        }

        private bool TryReadVector(XElement parent, string name, out GoldsrcVector3 vector)
        {
            vector = default;

            var element = parent.Element(name);
            if (element is null)
                return Fail($"<{parent.Name.LocalName}> is missing its <{name}>");

            Span<float> values = stackalloc float[3];
            if (!TryReadNumbers(element.Value, name, values, out int count))
                return false;
            if (count != 3)
                return Fail($"<{name}> must hold exactly 3 numbers but holds {count}");

            vector = new GoldsrcVector3(values[0], values[1], values[2]);
            return true;
        }

        /// <summary>
        /// Reads a scalar. As in gsphysics the value may be an attribute of
        /// <paramref name="parent"/> or the text of a child element of the same name.
        /// </summary>
        private bool TryReadFloat(XElement parent, string name, out float value, bool required = true)
        {
            value = 0.0f;

            string? text = parent.Attribute(name)?.Value ?? parent.Element(name)?.Value;
            if (text is null)
            {
                if (required)
                    return Fail($"<{parent.Name.LocalName}> is missing <{name}>");
                return true;
            }

            if (!TryParseNumber(text, out value))
                return Fail($"<{name}> is not a finite number");

            return true;
        }

        /// <summary>Splits the field text and parses it as finite invariant-culture floats.</summary>
        private bool TryReadNumbers(string text, string name, Span<float> destination, out int count)
        {
            count = 0;

            if (text.Length > MaxTextLength)
                return Fail($"<{name}> holds {text.Length} characters, over the {MaxTextLength} limit");

            var tokens = text.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
            count = tokens.Length;
            if (count > destination.Length)
                return true; // the caller reports the count mismatch with the exact number

            for (int i = 0; i < count; i++)
            {
                if (!TryParseNumber(tokens[i], out destination[i]))
                    return Fail($"<{name}> holds '{tokens[i]}', which is not a finite number");
            }

            return true;
        }

        private static bool TryParseNumber(string token, out float value)
        {
            // NumberStyles.Float accepts a sign, a decimal point and an exponent, which covers every
            // value the format uses. NaN and Infinity parse but are rejected by the finiteness check.
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            return float.IsFinite(value);
        }

        private static bool TryReadIntAttribute(XElement element, string name, out int value)
        {
            value = 0;

            var attribute = element.Attribute(name);
            return attribute is not null &&
                   int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsValidChecksum(string? checksum)
        {
            if (checksum is null || checksum.Length != ChecksumLength)
                return false;

            foreach (char character in checksum)
            {
                bool isHex =
                    character is >= '0' and <= '9' or
                    >= 'a' and <= 'f' or
                    >= 'A' and <= 'F';
                if (!isHex)
                    return false;
            }

            return true;
        }

        /// <summary>Records the first failure and reports it as "no asset".</summary>
        private GpdAsset? Report(string message)
        {
            Error ??= message;
            return null;
        }

        /// <summary>Records the first failure and reports it as "this step failed".</summary>
        private bool Fail(string message)
        {
            Error ??= message;
            return false;
        }
    }
}
