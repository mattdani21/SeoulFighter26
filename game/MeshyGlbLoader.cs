using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace FirstFighter.Game
{
    public sealed class MeshyRawPanel
    {
        public string Name { get; init; } = "";
        public Vector3[] Vertices { get; init; } = Array.Empty<Vector3>();
        public int[] Indices { get; init; } = Array.Empty<int>();
        public float MinY { get; init; }
        public float MaxY { get; init; }
        public float Height => MaxY - MinY;
    }

    public sealed class MeshyRawModelData
    {
        public MeshyRawPanel Front { get; init; } = null!;
        public MeshyRawPanel Side { get; init; } = null!;
        public MeshyRawPanel Back { get; init; } = null!;
        public MeshyRawPanel Guard { get; init; } = null!;
    }

    public sealed class MeshyTurnaroundPanels
    {
        public MeshyRawModelData Raw { get; init; } = null!;

        private ArrayMesh? _frontMesh;
        private ArrayMesh? _sideMesh;
        private ArrayMesh? _backMesh;
        private ArrayMesh? _guardMesh;

        public ArrayMesh FrontPanelMesh => _frontMesh ??= MeshyGlbLoader.BuildArrayMesh(Raw.Front);
        public ArrayMesh SidePanelMesh => _sideMesh ??= MeshyGlbLoader.BuildArrayMesh(Raw.Side);
        public ArrayMesh BackPanelMesh => _backMesh ??= MeshyGlbLoader.BuildArrayMesh(Raw.Back);
        public ArrayMesh GuardPanelMesh => _guardMesh ??= MeshyGlbLoader.BuildArrayMesh(Raw.Guard);

        // Raw vertex/index arrays for Panel 1 (FRONT) in normalized 1.85m space
        public Vector3[] FrontVertices => Raw.Front.Vertices;
        public int[] FrontIndices => Raw.Front.Indices;
    }

    /// <summary>
    /// Fast standalone binary reader and extractor for Meshy AI turnaround GLB files.
    /// Extracts the 4 disjoint 3D turnaround statues (FRONT, SIDE, BACK, GUARD) and auto-partitions
    /// Panel 1 (FRONT) across the 19-bone Skeleton3D hierarchy.
    /// </summary>
    public static class MeshyGlbLoader
    {
        public const string DefaultAssetPath = "assets/first_fighter_meshy.glb";
        private static MeshyTurnaroundPanels? _cachedPanels;

        public static string ResolveAssetPath(string path = DefaultAssetPath)
        {
            if (File.Exists(path)) return path;

            // In Godot editor or runtime
            string resPath = ProjectSettings.GlobalizePath($"res://{path}");
            if (File.Exists(resPath)) return resPath;

            // Check relative to current working directory or executable
            string fallback = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (File.Exists(fallback)) return fallback;

            return path;
        }

        public static bool AssetExists(string path = DefaultAssetPath)
        {
            return File.Exists(ResolveAssetPath(path));
        }

        public static MeshyRawModelData ParseGlb(string path = DefaultAssetPath)
        {
            string resolved = ResolveAssetPath(path);
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException($"Meshy GLB file not found at: {resolved}");
            }

            using var stream = File.OpenRead(resolved);
            using var reader = new BinaryReader(stream);

            uint magic = reader.ReadUInt32(); // 0x46546C67 ("glTF")
            uint version = reader.ReadUInt32();
            uint totalLength = reader.ReadUInt32();

            // Read JSON chunk
            uint jsonChunkLength = reader.ReadUInt32();
            uint jsonChunkType = reader.ReadUInt32(); // 0x4E4F534A ("JSON")
            byte[] jsonBytes = reader.ReadBytes((int)jsonChunkLength);
            string jsonText = Encoding.UTF8.GetString(jsonBytes);
            using var jsonDoc = JsonDocument.Parse(jsonText);
            var root = jsonDoc.RootElement;

            // Read BIN chunk
            uint binChunkLength = reader.ReadUInt32();
            uint binChunkType = reader.ReadUInt32(); // 0x004E4942 ("BIN\0")
            byte[] binBytes = reader.ReadBytes((int)binChunkLength);

            var accessors = root.GetProperty("accessors");
            var bufferViews = root.GetProperty("bufferViews");

            // Accessor 0 is POSITION (VEC3, FLOAT)
            var posAcc = accessors[0];
            int posCount = posAcc.GetProperty("count").GetInt32();
            int posBvIdx = posAcc.GetProperty("bufferView").GetInt32();
            var posBv = bufferViews[posBvIdx];
            int posOffset = posBv.TryGetProperty("byteOffset", out var pOff) ? pOff.GetInt32() : 0;

            // Accessor 1 is INDICES (SCALAR, UNSIGNED_INT)
            var idxAcc = accessors[1];
            int idxCount = idxAcc.GetProperty("count").GetInt32();
            int idxBvIdx = idxAcc.GetProperty("bufferView").GetInt32();
            var idxBv = bufferViews[idxBvIdx];
            int idxOffset = idxBv.TryGetProperty("byteOffset", out var iOff) ? iOff.GetInt32() : 0;

            Vector3[] allVerts = new Vector3[posCount];
            for (int i = 0; i < posCount; i++)
            {
                int baseByte = posOffset + i * 12;
                float x = BitConverter.ToSingle(binBytes, baseByte + 0);
                float y = BitConverter.ToSingle(binBytes, baseByte + 4);
                float z = BitConverter.ToSingle(binBytes, baseByte + 8);
                allVerts[i] = new Vector3(x, y, z);
            }

            int[] allIndices = new int[idxCount];
            for (int i = 0; i < idxCount; i++)
            {
                int baseByte = idxOffset + i * 4;
                allIndices[i] = (int)BitConverter.ToUInt32(binBytes, baseByte);
            }

            // The 4 disjoint figures along the raw X axis:
            // Gap cuts: [-1.0 .. -0.50] = FRONT, [-0.50 .. -0.13] = SIDE, [-0.13 .. 0.36] = BACK, [0.36 .. 1.0] = GUARD
            float[] cuts = { -1.0f, -0.50f, -0.13f, 0.36f, 1.0f };

            var panelFront = ExtractAndNormalizePanel("FRONT", allVerts, allIndices, cuts[0], cuts[1], flipZ: false);
            var panelSide = ExtractAndNormalizePanel("SIDE", allVerts, allIndices, cuts[1], cuts[2], flipZ: false);
            var panelBack = ExtractAndNormalizePanel("BACK", allVerts, allIndices, cuts[2], cuts[3], flipZ: false);
            var panelGuard = ExtractAndNormalizePanel("GUARD", allVerts, allIndices, cuts[3], cuts[4], flipZ: false);

            return new MeshyRawModelData
            {
                Front = panelFront,
                Side = panelSide,
                Back = panelBack,
                Guard = panelGuard
            };
        }

        public static MeshyTurnaroundPanels LoadOrGetTurnaroundPanels(string path = DefaultAssetPath)
        {
            if (_cachedPanels != null) return _cachedPanels;

            var raw = ParseGlb(path);
            _cachedPanels = new MeshyTurnaroundPanels
            {
                Raw = raw
            };

            return _cachedPanels;
        }

        private static MeshyRawPanel ExtractAndNormalizePanel(
            string name,
            Vector3[] allVerts,
            int[] allIndices,
            float xMinCut,
            float xMaxCut,
            bool flipZ)
        {
            var vertMap = new Dictionary<int, int>();
            var newVertsList = new List<Vector3>();
            var newIndicesList = new List<int>();

            int triCount = allIndices.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = allIndices[t * 3 + 0];
                int i1 = allIndices[t * 3 + 1];
                int i2 = allIndices[t * 3 + 2];

                float avgX = (allVerts[i0].X + allVerts[i1].X + allVerts[i2].X) / 3f;
                if (avgX >= xMinCut && avgX < xMaxCut)
                {
                    newIndicesList.Add(GetOrCreateVert(i0, allVerts, vertMap, newVertsList));
                    newIndicesList.Add(GetOrCreateVert(i1, allVerts, vertMap, newVertsList));
                    newIndicesList.Add(GetOrCreateVert(i2, allVerts, vertMap, newVertsList));
                }
            }

            Vector3[] verts = newVertsList.ToArray();
            int[] indices = newIndicesList.ToArray();

            if (verts.Length == 0)
            {
                return new MeshyRawPanel
                {
                    Name = name,
                    Vertices = Array.Empty<Vector3>(),
                    Indices = Array.Empty<int>(),
                    MinY = 0f,
                    MaxY = 0f
                };
            }

            // Compute AABB and scale to 1.85m tall (Y = [0.00, 1.85])
            float minY = float.MaxValue, maxY = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                if (v.Y < minY) minY = v.Y;
                if (v.Y > maxY) maxY = v.Y;
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
                if (v.Z < minZ) minZ = v.Z;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            float rawHeight = Math.Max(0.001f, maxY - minY);
            float scale = 1.85f / rawHeight;
            float midX = 0.5f * (minX + maxX);
            float midZ = 0.5f * (minZ + maxZ);

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float nx = (v.X - midX) * scale;
                float ny = (v.Y - minY) * scale;
                float nz = (v.Z - midZ) * scale;
                if (flipZ) nz = -nz;
                verts[i] = new Vector3(nx, ny, nz);
            }

            return new MeshyRawPanel
            {
                Name = name,
                Vertices = verts,
                Indices = indices,
                MinY = 0.00f,
                MaxY = 1.85f
            };
        }

        public static ArrayMesh BuildArrayMesh(MeshyRawPanel panel)
        {
            var verts = panel.Vertices;
            var indices = panel.Indices;
            if (verts.Length == 0 || indices.Length == 0) return new ArrayMesh();

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            Color baseCol = new Color("bdac9a");

            for (int t = 0; t < indices.Length / 3; t++)
            {
                Vector3 v0 = verts[indices[t * 3 + 0]];
                Vector3 v1 = verts[indices[t * 3 + 1]];
                Vector3 v2 = verts[indices[t * 3 + 2]];

                Vector3 normal = (v1 - v0).Cross(v2 - v0).Normalized();
                if (normal.LengthSquared() < 0.0001f) normal = Vector3.Up;

                st.SetNormal(normal);
                st.SetColor(baseCol);
                st.AddVertex(v0);

                st.SetNormal(normal);
                st.SetColor(baseCol);
                st.AddVertex(v1);

                st.SetNormal(normal);
                st.SetColor(baseCol);
                st.AddVertex(v2);
            }

            st.SetMaterial(FirstFighterModelBuilder.CreateFlatFacetMaterial(Colors.White, 0.85f, useVertexColors: true));
            return st.Commit();
        }

        private static int GetOrCreateVert(int originalIdx, Vector3[] allVerts, Dictionary<int, int> vertMap, List<Vector3> newVertsList)
        {
            if (vertMap.TryGetValue(originalIdx, out int mapped))
            {
                return mapped;
            }
            int newIdx = newVertsList.Count;
            newVertsList.Add(allVerts[originalIdx]);
            vertMap[originalIdx] = newIdx;
            return newIdx;
        }

        /// <summary>
        /// Partitions Panel 1 (FRONT) across the 19 anatomical bones and attaches them to the 19-bone Skeleton3D.
        /// </summary>
        public static void AttachPartitionedMeshyToSkeleton(
            Node3D[] boneNodes,
            Color shirtColor,
            Color slacksColor,
            Color skinColor,
            Color hairColor,
            Color browColor,
            Color loaferColor,
            Color soleColor)
        {
            var panels = LoadOrGetTurnaroundPanels();
            Vector3[] verts = panels.FrontVertices;
            int[] indices = panels.FrontIndices;
            if (verts.Length == 0 || indices.Length == 0) return;

            // Calculate rest world transforms for all 19 bones
            Vector3[] boneWorldPositions = new Vector3[FirstFighterModelBuilder.BoneCount];
            ComputeBoneWorldPositions(boneWorldPositions);

            // Separate SurfaceTools per bone
            var boneTools = new SurfaceTool[FirstFighterModelBuilder.BoneCount];
            for (int b = 0; b < FirstFighterModelBuilder.BoneCount; b++)
            {
                boneTools[b] = new SurfaceTool();
                boneTools[b].Begin(Mesh.PrimitiveType.Triangles);
            }

            int triCount = indices.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 v0 = verts[indices[t * 3 + 0]];
                Vector3 v1 = verts[indices[t * 3 + 1]];
                Vector3 v2 = verts[indices[t * 3 + 2]];

                Vector3 center = (v0 + v1 + v2) / 3f;
                Vector3 normal = (v1 - v0).Cross(v2 - v0).Normalized();
                if (normal.LengthSquared() < 0.0001f) normal = Vector3.Up;

                FighterBone bone = ClassifyCentroidToBone(center);
                Color color = DetermineFacetColor(center, bone, shirtColor, slacksColor, skinColor, hairColor, browColor, loaferColor, soleColor);

                // Subtly modulate facet color for flat-shaded visual pop
                float facetShift = ((t & 1) == 0) ? 0.025f : -0.022f;
                Color facetColor = facetShift >= 0 ? color.Lightened(facetShift) : color.Darkened(-facetShift);

                // Transform triangle vertices to the bone's local rest coordinate space
                Vector3 bWorld = boneWorldPositions[(int)bone];
                Vector3 lv0 = v0 - bWorld;
                Vector3 lv1 = v1 - bWorld;
                Vector3 lv2 = v2 - bWorld;

                var st = boneTools[(int)bone];
                st.SetNormal(normal);
                st.SetColor(facetColor);
                st.AddVertex(lv0);

                st.SetNormal(normal);
                st.SetColor(facetColor);
                st.AddVertex(lv1);

                st.SetNormal(normal);
                st.SetColor(facetColor);
                st.AddVertex(lv2);
            }

            // Commit meshes to bone attachment nodes
            for (int b = 0; b < FirstFighterModelBuilder.BoneCount; b++)
            {
                var mesh = boneTools[b].Commit();
                if (mesh.GetSurfaceCount() > 0)
                {
                    mesh.SurfaceSetMaterial(0, FirstFighterModelBuilder.CreateFlatFacetMaterial(Colors.White, 0.85f, useVertexColors: true));
                    var inst = new MeshInstance3D
                    {
                        Name = $"Meshy_{((FighterBone)b)}",
                        Mesh = mesh,
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.On
                    };
                    boneNodes[b].AddChild(inst);
                }
            }
        }

        public static void ComputeBoneWorldPositions(Vector3[] outPos)
        {
            // Hierarchy parent offsets
            outPos[(int)FighterBone.Hips] = FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Hips]; // (0, 0.96, 0)
            outPos[(int)FighterBone.Abdomen] = outPos[(int)FighterBone.Hips] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Abdomen]; // (0, 1.10, 0)
            outPos[(int)FighterBone.Torso] = outPos[(int)FighterBone.Abdomen] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Torso]; // (0, 1.32, 0)
            outPos[(int)FighterBone.Neck] = outPos[(int)FighterBone.Torso] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Neck]; // (0, 1.53, 0)
            outPos[(int)FighterBone.Head] = outPos[(int)FighterBone.Neck] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Head]; // (0, 1.62, 0)

            outPos[(int)FighterBone.ShoulderL] = outPos[(int)FighterBone.Torso] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.ShoulderL]; // (-0.18, 1.49, 0)
            outPos[(int)FighterBone.ShoulderR] = outPos[(int)FighterBone.Torso] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.ShoulderR]; // (0.18, 1.49, 0)

            outPos[(int)FighterBone.UpperArmL] = outPos[(int)FighterBone.ShoulderL] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.UpperArmL]; // (-0.225, 1.49, 0)
            outPos[(int)FighterBone.UpperArmR] = outPos[(int)FighterBone.ShoulderR] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.UpperArmR]; // (0.225, 1.49, 0)

            outPos[(int)FighterBone.LowerArmL] = outPos[(int)FighterBone.UpperArmL] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.LowerArmL]; // (-0.225, 1.17, 0)
            outPos[(int)FighterBone.LowerArmR] = outPos[(int)FighterBone.UpperArmR] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.LowerArmR]; // (0.225, 1.17, 0)

            outPos[(int)FighterBone.FistL] = outPos[(int)FighterBone.LowerArmL] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.FistL]; // (-0.225, 0.88, 0)
            outPos[(int)FighterBone.FistR] = outPos[(int)FighterBone.LowerArmR] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.FistR]; // (0.225, 0.88, 0)

            outPos[(int)FighterBone.UpperLegL] = outPos[(int)FighterBone.Hips] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.UpperLegL]; // (-0.11, 0.94, 0)
            outPos[(int)FighterBone.UpperLegR] = outPos[(int)FighterBone.Hips] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.UpperLegR]; // (0.11, 0.94, 0)

            outPos[(int)FighterBone.LowerLegL] = outPos[(int)FighterBone.UpperLegL] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.LowerLegL]; // (-0.11, 0.48, 0)
            outPos[(int)FighterBone.LowerLegR] = outPos[(int)FighterBone.UpperLegR] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.LowerLegR]; // (0.11, 0.48, 0)

            outPos[(int)FighterBone.FootL] = outPos[(int)FighterBone.LowerLegL] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.FootL]; // (-0.11, 0.06, 0.05)
            outPos[(int)FighterBone.FootR] = outPos[(int)FighterBone.LowerLegR] + FirstFighterModelBuilder.RestOffsets[(int)FighterBone.FootR]; // (0.11, 0.06, 0.05)
        }

        public static FighterBone ClassifyCentroidToBone(Vector3 p)
        {
            float y = p.Y;
            float x = p.X;
            float absX = Math.Abs(x);
            bool isLeft = x < 0f;

            // Head / Crown
            if (y >= 1.61f) return FighterBone.Head;

            // Neck
            if (y >= 1.52f && absX <= 0.11f) return FighterBone.Neck;

            // Arms vs Torso/Hips:
            // Notice the measured clear air gap at |X| = 0.17m..0.21m for Y < 1.48m
            float torsoOuterBoundary = y >= 1.30f ? 0.175f : 0.160f;

            if (absX > torsoOuterBoundary && y <= 1.52f && y >= 0.70f)
            {
                // Shoulder deltoid caps
                if (y >= 1.42f) return isLeft ? FighterBone.ShoulderL : FighterBone.ShoulderR;
                // Upper arm down to elbow
                if (y >= 1.16f) return isLeft ? FighterBone.UpperArmL : FighterBone.UpperArmR;
                // Bare forearm down to wrist
                if (y >= 0.88f) return isLeft ? FighterBone.LowerArmL : FighterBone.LowerArmR;
                // Hand / Fist
                return isLeft ? FighterBone.FistL : FighterBone.FistR;
            }

            // Central Upper Body: Torso, Abdomen, Hips
            if (y >= 1.28f) return FighterBone.Torso;
            if (y >= 1.04f) return FighterBone.Abdomen;
            if (y >= 0.90f) return FighterBone.Hips;

            // Lower Body: Legs & Feet
            if (y >= 0.48f) return isLeft ? FighterBone.UpperLegL : FighterBone.UpperLegR;
            if (y >= 0.075f) return isLeft ? FighterBone.LowerLegL : FighterBone.LowerLegR;
            return isLeft ? FighterBone.FootL : FighterBone.FootR;
        }

        public static Color DetermineFacetColor(
            Vector3 p,
            FighterBone bone,
            Color shirtColor,
            Color slacksColor,
            Color skinColor,
            Color hairColor,
            Color browColor,
            Color loaferColor,
            Color soleColor)
        {
            float y = p.Y;
            float z = p.Z;
            float absX = Math.Abs(p.X);

            // Head & Hair
            if (bone == FighterBone.Head)
            {
                // Top, back, or temples = hair
                if (y >= 1.76f || z <= -0.015f || (y >= 1.68f && absX >= 0.065f))
                {
                    return hairColor;
                }
                // Brow ridge
                if (y >= 1.70f && y <= 1.73f && z >= 0.060f)
                {
                    return browColor;
                }
                return skinColor;
            }

            // Neck
            if (bone == FighterBone.Neck)
            {
                return skinColor;
            }

            // Torso & Open Henley V-Placket
            if (bone == FighterBone.Torso)
            {
                // Front V-placket throat notch exposing skin
                if (z >= 0.055f && y >= 1.40f && y <= 1.53f && absX <= 0.045f)
                {
                    return skinColor;
                }
                return shirtColor;
            }

            if (bone == FighterBone.Abdomen)
            {
                return shirtColor;
            }

            // Arms: rolled sleeves vs bare forearms & hands
            if (bone == FighterBone.ShoulderL || bone == FighterBone.ShoulderR ||
                bone == FighterBone.UpperArmL || bone == FighterBone.UpperArmR)
            {
                return shirtColor;
            }

            if (bone == FighterBone.LowerArmL || bone == FighterBone.LowerArmR ||
                bone == FighterBone.FistL || bone == FighterBone.FistR)
            {
                return skinColor;
            }

            // Trousers, Cropped Bare Ankle & Loafers
            if (bone == FighterBone.Hips ||
                bone == FighterBone.UpperLegL || bone == FighterBone.UpperLegR)
            {
                return slacksColor;
            }

            if (bone == FighterBone.LowerLegL || bone == FighterBone.LowerLegR)
            {
                // Cropped cuff exposes 0.04m of bare ankle skin at Y = 0.075m .. 0.11m
                if (y < 0.115f)
                {
                    return skinColor;
                }
                return slacksColor;
            }

            if (bone == FighterBone.FootL || bone == FighterBone.FootR)
            {
                // Flat rubber sole strip (#4a433c) at floor level
                if (y < 0.020f)
                {
                    return soleColor;
                }
                return loaferColor;
            }

            return shirtColor;
        }
    }
}
