using System;
using System.Collections.Generic;
using Godot;

namespace FirstFighter.Game
{
    /// <summary>
    /// 19-bone anatomical hierarchy for FIRST FIGHTER (1.85m total height, 7.8 heads tall).
    /// </summary>
    public enum FighterBone : int
    {
        Hips = 0,
        Abdomen = 1,
        Torso = 2,
        Neck = 3,
        Head = 4,
        ShoulderL = 5,
        ShoulderR = 6,
        UpperArmL = 7,
        UpperArmR = 8,
        LowerArmL = 9,
        LowerArmR = 10,
        FistL = 11,
        FistR = 12,
        UpperLegL = 13,
        UpperLegR = 14,
        LowerLegL = 15,
        LowerLegR = 16,
        FootL = 17,
        FootR = 18
    }

    public readonly struct RingSpec
    {
        public readonly Vector3 Center;
        public readonly float RadiusX;
        public readonly float RadiusZ;
        public readonly float TwistRad;
        public readonly float FrontBackCreaseFactor;
        public readonly float FacetJitter;

        public RingSpec(
            Vector3 center,
            float radiusX,
            float radiusZ,
            float twistRad = 0f,
            float frontBackCreaseFactor = 1.0f,
            float facetJitter = 0.0f)
        {
            Center = center;
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            TwistRad = twistRad;
            FrontBackCreaseFactor = frontBackCreaseFactor;
            FacetJitter = facetJitter;
        }
    }

    public enum FighterMeshMode : int
    {
        MeshyGlbWithGuardStatue = 0,
        MeshyGlbFullRig = 1,
        ProceduralRig = 2
    }

    public sealed class BuiltFighterModel
    {
        public Skeleton3D Skeleton { get; init; } = null!;
        public Node3D[] BoneAttachmentNodes { get; init; } = Array.Empty<Node3D>();
        public Vector3[] RestLocalPositions { get; init; } = Array.Empty<Vector3>();
        public MeshInstance3D LeftFistClenchedMesh { get; init; } = null!;
        public MeshInstance3D RightFistClenchedMesh { get; init; } = null!;
        public MeshInstance3D LeftHandRelaxedMesh { get; init; } = null!;
        public MeshInstance3D RightHandRelaxedMesh { get; init; } = null!;

        public Node3D? ProceduralRootNode { get; set; }
        public Node3D? MeshyRigRootNode { get; set; }
        public MeshInstance3D? MeshyGuardStatueInstance { get; set; }

        public void SetMeshMode(FighterMeshMode mode, bool inNeutralIdle)
        {
            if (ProceduralRootNode == null || MeshyRigRootNode == null) return;

            switch (mode)
            {
                case FighterMeshMode.ProceduralRig:
                    ProceduralRootNode.Visible = true;
                    MeshyRigRootNode.Visible = false;
                    if (MeshyGuardStatueInstance != null) MeshyGuardStatueInstance.Visible = false;
                    break;

                case FighterMeshMode.MeshyGlbFullRig:
                    ProceduralRootNode.Visible = false;
                    MeshyRigRootNode.Visible = true;
                    if (MeshyGuardStatueInstance != null) MeshyGuardStatueInstance.Visible = false;
                    break;

                case FighterMeshMode.MeshyGlbWithGuardStatue:
                default:
                    ProceduralRootNode.Visible = false;
                    if (inNeutralIdle && MeshyGuardStatueInstance != null)
                    {
                        MeshyRigRootNode.Visible = false;
                        MeshyGuardStatueInstance.Visible = true;
                    }
                    else
                    {
                        MeshyRigRootNode.Visible = true;
                        if (MeshyGuardStatueInstance != null) MeshyGuardStatueInstance.Visible = false;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Procedural low-poly flat-shaded triangulated 3D character builder matching reference_first_fighter.jpg.
    /// Zero black cartoon outlines; uses crisp per-triangle flat normals and subtle facet tone variation.
    /// </summary>
    public static class FirstFighterModelBuilder
    {
        public const int BoneCount = 19;

        // Sampled Reference Palette (Section 1.2)
        public static readonly Color P1HenleyBase = new Color("bdac9a");     // Oatmeal / Sand Henley midtone
        public static readonly Color P1HenleyLit = new Color("cfc0ae");      // Lit facet
        public static readonly Color P1HenleyShadow = new Color("9c8c7b");   // Shadow facet

        public static readonly Color P2HenleyBase = new Color("283142");     // Midnight Navy (P2 mirror alt)
        public static readonly Color P1SlacksBase = new Color("34302c");     // Deep Espresso-Charcoal tailored trousers
        public static readonly Color P2SlacksBase = new Color("8c7d6b");     // Khaki slacks (P2 mirror alt)

        public static readonly Color SkinBase = new Color("b68a67");         // Golden-tan sculpted skin
        public static readonly Color SkinLit = new Color("c89a74");
        public static readonly Color SkinShadow = new Color("8c6244");

        public static readonly Color HairBase = new Color("272422");         // Tousled dark hair locks
        public static readonly Color BrowColor = new Color("161413");        // Crisp dark brows & eyelashes
        public static readonly Color LoaferUpperColor = new Color("282523"); // Matte dark charcoal slip-on loafer
        public static readonly Color LoaferSoleColor = new Color("4a433c");  // Contrasting flat rubber sole strip

        private static readonly string[] BoneNames = new[]
        {
            "Hips",
            "Abdomen",
            "Torso",
            "Neck",
            "Head",
            "ShoulderL",
            "ShoulderR",
            "UpperArmL",
            "UpperArmR",
            "LowerArmL",
            "LowerArmR",
            "FistL",
            "FistR",
            "UpperLegL",
            "UpperLegR",
            "LowerLegL",
            "LowerLegR",
            "FootL",
            "FootR"
        };

        private static readonly int[] BoneParents = new[]
        {
            -1, // 0: Hips
            0,  // 1: Abdomen -> Hips
            1,  // 2: Torso -> Abdomen
            2,  // 3: Neck -> Torso
            3,  // 4: Head -> Neck
            2,  // 5: ShoulderL -> Torso
            2,  // 6: ShoulderR -> Torso
            5,  // 7: UpperArmL -> ShoulderL
            6,  // 8: UpperArmR -> ShoulderR
            7,  // 9: LowerArmL -> UpperArmL
            8,  // 10: LowerArmR -> UpperArmR
            9,  // 11: FistL -> LowerArmL
            10, // 12: FistR -> LowerArmR
            0,  // 13: UpperLegL -> Hips
            0,  // 14: UpperLegR -> Hips
            13, // 15: LowerLegL -> UpperLegL
            14, // 16: LowerLegR -> UpperLegR
            15, // 17: FootL -> LowerLegL
            16  // 18: FootR -> LowerLegR
        };

        /// <summary>
        /// Exact 19-bone rest offsets (1.85m total height, 1.04m waistband = 56.2% leg ratio).
        /// Note: In Godot 3D, character local forward is +X (along the 2.5D stage plane) or +Z;
        /// here we build character local space with +Z as anatomical front and +Y as up,
        /// then orient the root so facingRight maps to +X on the 2.5D plane.
        /// </summary>
        public static readonly Vector3[] RestOffsets = new[]
        {
            new Vector3(0.00f,  0.96f, 0.00f), // 0: Hips (0.96m)
            new Vector3(0.00f,  0.14f, 0.00f), // 1: Abdomen (1.10m)
            new Vector3(0.00f,  0.22f, 0.00f), // 2: Torso (1.32m, chest up to 1.53m)
            new Vector3(0.00f,  0.21f, 0.00f), // 3: Neck (1.53m)
            new Vector3(0.00f,  0.09f, 0.00f), // 4: Head (1.62m..1.85m crown)
            new Vector3(-0.18f, 0.17f, 0.00f), // 5: ShoulderL (relative to Torso)
            new Vector3( 0.18f, 0.17f, 0.00f), // 6: ShoulderR (relative to Torso)
            new Vector3(-0.045f, 0.00f, 0.00f),// 7: UpperArmL (length 0.32m)
            new Vector3( 0.045f, 0.00f, 0.00f),// 8: UpperArmR (length 0.32m)
            new Vector3(0.00f, -0.32f, 0.00f), // 9: LowerArmL (length 0.29m)
            new Vector3(0.00f, -0.32f, 0.00f), // 10: LowerArmR (length 0.29m)
            new Vector3(0.00f, -0.29f, 0.00f), // 11: FistL (length 0.13m)
            new Vector3(0.00f, -0.29f, 0.00f), // 12: FistR (length 0.13m)
            new Vector3(-0.11f, -0.02f, 0.00f),// 13: UpperLegL (length 0.46m)
            new Vector3( 0.11f, -0.02f, 0.00f),// 14: UpperLegR (length 0.46m)
            new Vector3(0.00f, -0.46f, 0.00f), // 15: LowerLegL (length 0.42m)
            new Vector3(0.00f, -0.46f, 0.00f), // 16: LowerLegR (length 0.42m)
            new Vector3(0.00f, -0.42f, 0.05f), // 17: FootL (0.00m..0.07m)
            new Vector3(0.00f, -0.42f, 0.05f)  // 18: FootR (0.00m..0.07m)
        };

        public static BuiltFighterModel BuildCharacterRig(bool isPlayerTwoPalette = false)
        {
            Color shirtColor = isPlayerTwoPalette ? P2HenleyBase : P1HenleyBase;
            Color slacksColor = isPlayerTwoPalette ? P2SlacksBase : P1SlacksBase;

            var skeleton = new Skeleton3D
            {
                Name = "FirstFighterSkeleton"
            };

            var boneNodes = new Node3D[BoneCount];
            var restLocalPositions = new Vector3[BoneCount];

            for (int i = 0; i < BoneCount; i++)
            {
                skeleton.AddBone(BoneNames[i]);
                if (BoneParents[i] >= 0)
                {
                    skeleton.SetBoneParent(i, BoneParents[i]);
                }

                Transform3D rest = new Transform3D(Basis.Identity, RestOffsets[i]);
                skeleton.SetBoneRest(i, rest);
                skeleton.SetBonePosePosition(i, RestOffsets[i]);
                skeleton.SetBonePoseRotation(i, Quaternion.Identity);
                skeleton.SetBonePoseScale(i, Vector3.One);
                restLocalPositions[i] = RestOffsets[i];

                var attachmentNode = new Node3D
                {
                    Name = $"{BoneNames[i]}_Geom",
                    Position = RestOffsets[i]
                };

                if (BoneParents[i] < 0)
                {
                    skeleton.AddChild(attachmentNode);
                }
                else
                {
                    boneNodes[BoneParents[i]].AddChild(attachmentNode);
                }
                boneNodes[i] = attachmentNode;
            }

            // Containers for mesh mode switching: Procedural vs Meshy GLB
            var proceduralRoot = new Node3D { Name = "ProceduralRoot" };
            skeleton.AddChild(proceduralRoot);

            var meshyRoot = new Node3D { Name = "MeshyRigRoot" };
            skeleton.AddChild(meshyRoot);

            // Per-bone subnodes for procedural geometry
            var procBoneNodes = new Node3D[BoneCount];
            // Per-bone subnodes for partitioned Meshy GLB geometry
            var meshyBoneNodes = new Node3D[BoneCount];

            for (int i = 0; i < BoneCount; i++)
            {
                var pSub = new Node3D { Name = "ProcGeom" };
                boneNodes[i].AddChild(pSub);
                procBoneNodes[i] = pSub;

                var mSub = new Node3D { Name = "MeshyGeom" };
                boneNodes[i].AddChild(mSub);
                meshyBoneNodes[i] = mSub;
            }

            // 1. Pelvis / Hip Sockets & Tailored Waistband (Hips: 0.94m - 1.04m)
            BuildHipsAndWaistband(procBoneNodes[(int)FighterBone.Hips], slacksColor);

            // 2. Waist & Tucked Oatmeal Henley Hem (Abdomen: 1.04m - 1.24m)
            BuildAbdomenHenley(procBoneNodes[(int)FighterBone.Abdomen], shirtColor);

            // 3. Broad V-Tapered Chest, Band-Collar Henley & Exposed V-Placket Notch (Torso: 1.24m - 1.53m)
            BuildTorsoHenleyAndCollar(procBoneNodes[(int)FighterBone.Torso], shirtColor, SkinBase);

            // 4. Muscular Neck (Neck: 1.53m - 1.62m)
            BuildNeck(procBoneNodes[(int)FighterBone.Neck], SkinBase);

            // 5. Angular 8-Plane Sculpted Head, Brows & 16 Tousled Wedge Hair Locks (Head: 1.62m - 1.85m)
            BuildHeadAndHair(procBoneNodes[(int)FighterBone.Head], SkinBase, HairBase, BrowColor);

            // 6. Deltoid Caps & Rolled-Cuff Henley Upper Sleeves (ShoulderL/R, UpperArmL/R)
            BuildShoulderAndUpperArm(procBoneNodes[(int)FighterBone.ShoulderL], procBoneNodes[(int)FighterBone.UpperArmL], shirtColor, isLeft: true);
            BuildShoulderAndUpperArm(procBoneNodes[(int)FighterBone.ShoulderR], procBoneNodes[(int)FighterBone.UpperArmR], shirtColor, isLeft: false);

            // 7. Bare Muscular Forearms (LowerArmL/R)
            BuildBareForearm(procBoneNodes[(int)FighterBone.LowerArmL], SkinBase, isLeft: true);
            BuildBareForearm(procBoneNodes[(int)FighterBone.LowerArmR], SkinBase, isLeft: false);

            // 8. Hands / Fists (Clenched Fist for GUARD/combat + Relaxed Open Fingers for Turnaround FRONT/SIDE/BACK)
            var (fistClenchedL, handRelaxedL) = BuildHandMeshes(procBoneNodes[(int)FighterBone.FistL], SkinBase, isLeft: true);
            var (fistClenchedR, handRelaxedR) = BuildHandMeshes(procBoneNodes[(int)FighterBone.FistR], SkinBase, isLeft: false);

            // 9. Tailored Charcoal Trousers with Front/Back Crease Ridge (UpperLegL/R)
            BuildTailoredThigh(procBoneNodes[(int)FighterBone.UpperLegL], slacksColor, isLeft: true);
            BuildTailoredThigh(procBoneNodes[(int)FighterBone.UpperLegR], slacksColor, isLeft: false);

            // 10. Cropped Trouser Calf (82%) + Exposed Bare Ankle Skin (18%, Y = 0.07m - 0.11m) (LowerLegL/R)
            BuildCroppedCalfAndAnkle(procBoneNodes[(int)FighterBone.LowerLegL], slacksColor, SkinBase, isLeft: true);
            BuildCroppedCalfAndAnkle(procBoneNodes[(int)FighterBone.LowerLegR], slacksColor, SkinBase, isLeft: false);

            // 11. Low-Profile Matte Slip-On Loafers with #4a433c Rubber Sole (FootL/R)
            BuildSlipOnLoafer(procBoneNodes[(int)FighterBone.FootL], LoaferUpperColor, LoaferSoleColor, isLeft: true);
            BuildSlipOnLoafer(procBoneNodes[(int)FighterBone.FootR], LoaferUpperColor, LoaferSoleColor, isLeft: false);

            // 12. Attach Meshy GLB Partitioned Rig if GLB asset is present
            MeshInstance3D? guardStatueInst = null;
            if (MeshyGlbLoader.AssetExists())
            {
                try
                {
                    MeshyGlbLoader.AttachPartitionedMeshyToSkeleton(
                        meshyBoneNodes,
                        shirtColor,
                        slacksColor,
                        SkinBase,
                        HairBase,
                        BrowColor,
                        LoaferUpperColor,
                        LoaferSoleColor);

                    // Also prepare Panel 4 (GUARD) standalone statue for neutral guard stance
                    var panels = MeshyGlbLoader.LoadOrGetTurnaroundPanels();
                    guardStatueInst = new MeshInstance3D
                    {
                        Name = "MeshyGuardStatue",
                        Mesh = panels.GuardPanelMesh,
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                        Visible = false
                    };
                    skeleton.AddChild(guardStatueInst);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to attach Meshy GLB rig: {ex.Message}");
                }
            }

            var builtModel = new BuiltFighterModel
            {
                Skeleton = skeleton,
                BoneAttachmentNodes = boneNodes,
                RestLocalPositions = restLocalPositions,
                LeftFistClenchedMesh = fistClenchedL,
                RightFistClenchedMesh = fistClenchedR,
                LeftHandRelaxedMesh = handRelaxedL,
                RightHandRelaxedMesh = handRelaxedR,
                ProceduralRootNode = proceduralRoot,
                MeshyRigRootNode = meshyRoot,
                MeshyGuardStatueInstance = guardStatueInst
            };

            // Default to Meshy GLB mode if loaded, otherwise fallback to procedural rig
            bool hasMeshy = MeshyGlbLoader.AssetExists();
            builtModel.SetMeshMode(hasMeshy ? FighterMeshMode.MeshyGlbWithGuardStatue : FighterMeshMode.ProceduralRig, inNeutralIdle: true);

            return builtModel;
        }

        private static void BuildHipsAndWaistband(Node3D hipsNode, Color slacksColor)
        {
            // Pelvis block (0.32m wide x 0.21m deep) with tailored flat-front waistband and belt loops at Y = +0.08m (world 1.04m)
            var pelvisRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.05f, 0f), 0.145f, 0.095f, 0.05f, 1.05f, 0.004f),
                new RingSpec(new Vector3(0f,  0.01f, 0f), 0.160f, 0.105f, -0.04f, 1.06f, 0.005f),
                new RingSpec(new Vector3(0f,  0.065f, 0f), 0.150f, 0.100f, 0.03f, 1.05f, 0.004f),
                new RingSpec(new Vector3(0f,  0.090f, 0f), 0.144f, 0.096f, 0.00f, 1.04f, 0.002f)
            };
            AddMeshChild(hipsNode, "PelvisSlacks", CreateFacetedLoftMesh(pelvisRings, slacksColor, sides: 10, roughness: 0.88f));

            // Crisp tailored waistband & belt loops
            Color waistbandColor = slacksColor.Darkened(0.08f);
            var bandRings = new[]
            {
                new RingSpec(new Vector3(0f, 0.065f, 0f), 0.148f, 0.099f, 0f, 1.04f),
                new RingSpec(new Vector3(0f, 0.098f, 0f), 0.146f, 0.098f, 0f, 1.04f)
            };
            AddMeshChild(hipsNode, "TailoredWaistband", CreateFacetedLoftMesh(bandRings, waistbandColor, sides: 10, roughness: 0.85f));
        }

        private static void BuildAbdomenHenley(Node3D abdomenNode, Color shirtColor)
        {
            // Tucked Henley waist tapering from 0.28m waist up toward lower ribs with faceted diagonal cloth folds
            var rings = new[]
            {
                new RingSpec(new Vector3(0f, -0.05f, 0f), 0.142f, 0.095f,  0.06f, 1.02f, 0.006f),
                new RingSpec(new Vector3(0f,  0.03f, 0f), 0.152f, 0.102f, -0.08f, 1.03f, 0.008f),
                new RingSpec(new Vector3(0f,  0.12f, 0f), 0.162f, 0.108f,  0.07f, 1.02f, 0.007f),
                new RingSpec(new Vector3(0f,  0.22f, 0f), 0.176f, 0.114f, -0.05f, 1.02f, 0.006f)
            };
            AddMeshChild(abdomenNode, "AbdomenHenley", CreateFacetedLoftMesh(rings, shirtColor, sides: 10, roughness: 0.86f));
        }

        private static void BuildTorsoHenleyAndCollar(Node3D torsoNode, Color shirtColor, Color skinColor)
        {
            // V-tapered martial artist torso (expanding to 0.44m shoulder span at Y = +0.18m)
            var chestRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.02f, 0.00f), 0.174f, 0.112f,  0.05f, 1.02f, 0.006f),
                new RingSpec(new Vector3(0f,  0.06f, 0.005f), 0.195f, 0.122f, -0.06f, 1.04f, 0.007f),
                new RingSpec(new Vector3(0f,  0.14f, 0.008f), 0.214f, 0.126f,  0.04f, 1.03f, 0.006f),
                new RingSpec(new Vector3(0f,  0.20f, 0.00f),  0.185f, 0.110f, -0.03f, 1.01f, 0.005f),
                new RingSpec(new Vector3(0f,  0.225f, -0.005f), 0.085f, 0.075f, 0.00f, 1.00f, 0.002f)
            };
            AddMeshChild(torsoNode, "TorsoHenley", CreateFacetedLoftMesh(chestRings, shirtColor, sides: 10, roughness: 0.85f));

            // Band-Collar ring around neck base
            var collarRings = new[]
            {
                new RingSpec(new Vector3(0f, 0.205f, -0.004f), 0.078f, 0.072f, 0f, 1.0f),
                new RingSpec(new Vector3(0f, 0.232f, -0.004f), 0.072f, 0.068f, 0f, 1.0f)
            };
            AddMeshChild(torsoNode, "BandCollar", CreateFacetedLoftMesh(collarRings, shirtColor.Lightened(0.05f), sides: 8, roughness: 0.85f));

            // Open Henley V-Placket Notch exposing muscular golden-tan chest/clavicle triangle on +Z front
            var placketSkinMesh = CreateVPlacketChestWedge(skinColor, shirtColor.Darkened(0.08f));
            AddMeshChild(torsoNode, "OpenHenleyPlacket", placketSkinMesh);
        }

        private static ArrayMesh CreateVPlacketChestWedge(Color skinColor, Color lapelColor)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            // Exposed V-shaped collarbone/sternum notch on front chest (+Z)
            Vector3 topL = new Vector3(-0.042f, 0.228f, 0.068f);
            Vector3 topR = new Vector3( 0.042f, 0.228f, 0.068f);
            Vector3 midC = new Vector3( 0.000f, 0.165f, 0.118f);
            Vector3 botV = new Vector3( 0.000f, 0.085f, 0.129f);
            Vector3 midL = new Vector3(-0.034f, 0.150f, 0.116f);
            Vector3 midR = new Vector3( 0.034f, 0.150f, 0.116f);

            AddFlatTriangle(st, topL, midC, topR, skinColor.Lightened(0.04f));
            AddFlatTriangle(st, topL, midL, midC, skinColor);
            AddFlatTriangle(st, midC, midR, topR, skinColor.Darkened(0.03f));
            AddFlatTriangle(st, midL, botV, midC, skinColor.Darkened(0.05f));
            AddFlatTriangle(st, midC, botV, midR, skinColor);

            // Crisp placket lapel borders
            Vector3 lapOutL = new Vector3(-0.056f, 0.224f, 0.066f);
            Vector3 lapMidL = new Vector3(-0.048f, 0.145f, 0.118f);
            Vector3 lapOutR = new Vector3( 0.056f, 0.224f, 0.066f);
            Vector3 lapMidR = new Vector3( 0.048f, 0.145f, 0.118f);

            AddFlatTriangle(st, lapOutL, lapMidL, topL, lapelColor);
            AddFlatTriangle(st, topL, lapMidL, midL, lapelColor);
            AddFlatTriangle(st, midL, lapMidL, botV, lapelColor);

            AddFlatTriangle(st, topR, lapMidR, lapOutR, lapelColor);
            AddFlatTriangle(st, midR, lapMidR, topR, lapelColor);
            AddFlatTriangle(st, botV, lapMidR, midR, lapelColor);

            st.SetMaterial(CreateFlatFacetMaterial( Colors.White, 0.82f, useVertexColors: true));
            return st.Commit();
        }

        private static void BuildNeck(Node3D neckNode, Color skinColor)
        {
            var neckRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.02f, -0.004f), 0.058f, 0.058f, 0.0f, 1.04f, 0.002f),
                new RingSpec(new Vector3(0f,  0.04f,  0.000f), 0.053f, 0.054f, 0.04f, 1.03f, 0.002f),
                new RingSpec(new Vector3(0f,  0.10f,  0.004f), 0.052f, 0.055f, 0.0f, 1.02f, 0.002f)
            };
            AddMeshChild(neckNode, "MuscularNeck", CreateFacetedLoftMesh(neckRings, skinColor, sides: 8, roughness: 0.78f));
        }

        private static void BuildHeadAndHair(Node3D headNode, Color skinColor, Color hairColor, Color browColor)
        {
            // Angular 8-plane sculpted head (1.62m - 1.85m, height 0.23m, width 0.15m, depth 0.18m)
            var headRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.01f,  0.018f), 0.042f, 0.052f, 0f, 1.12f, 0.002f), // Chiseled chin & jawline base
                new RingSpec(new Vector3(0f,  0.035f, 0.014f), 0.068f, 0.082f, 0f, 1.08f, 0.003f), // Jaw angle & lower cheeks
                new RingSpec(new Vector3(0f,  0.095f, 0.012f), 0.075f, 0.090f, 0f, 1.06f, 0.003f), // Angular cheekbones
                new RingSpec(new Vector3(0f,  0.145f, 0.008f), 0.074f, 0.092f, 0f, 1.05f, 0.002f), // Strong brow ridge & temples
                new RingSpec(new Vector3(0f,  0.195f, -0.004f), 0.066f, 0.084f, 0f, 1.02f, 0.002f),// Upper cranium
                new RingSpec(new Vector3(0f,  0.220f, -0.010f), 0.032f, 0.042f, 0f, 1.00f, 0.001f) // Crown closure
            };
            AddMeshChild(headNode, "SculptedHead", CreateFacetedLoftMesh(headRings, skinColor, sides: 8, roughness: 0.76f));

            // Sculpted nose bridge, ears, dark eyebrows, and focused eyes
            AddMeshChild(headNode, "FacialFeatures", CreateFacialFeaturesMesh(skinColor, browColor));

            // Tousled layered dark hair with 16 faceted low-poly wedge locks parted off-center + 3 front forehead locks + tapered nape
            AddMeshChild(headNode, "TousledHairLocks", CreateTousledHairMesh(hairColor));
        }

        private static ArrayMesh CreateFacialFeaturesMesh(Color skinColor, Color browColor)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            // Planar nose bridge & tip on +Z face
            Vector3 nTop = new Vector3(0f, 0.135f, 0.094f);
            Vector3 nTip = new Vector3(0f, 0.082f, 0.112f);
            Vector3 nBot = new Vector3(0f, 0.068f, 0.096f);
            Vector3 nL = new Vector3(-0.016f, 0.076f, 0.092f);
            Vector3 nR = new Vector3( 0.016f, 0.076f, 0.092f);

            AddFlatTriangle(st, nTop, nL, nTip, skinColor.Lightened(0.05f));
            AddFlatTriangle(st, nTop, nTip, nR, skinColor.Darkened(0.03f));
            AddFlatTriangle(st, nL, nBot, nTip, skinColor.Darkened(0.08f));
            AddFlatTriangle(st, nTip, nBot, nR, skinColor.Darkened(0.08f));

            // Strong angular dark eyebrows & eye planes
            AddFacetedBox(st, new Vector3(-0.032f, 0.138f, 0.090f), new Vector3(0.024f, 0.007f, 0.012f), new Vector3(0, 0, -0.10f), browColor);
            AddFacetedBox(st, new Vector3( 0.032f, 0.138f, 0.090f), new Vector3(0.024f, 0.007f, 0.012f), new Vector3(0, 0,  0.10f), browColor);

            // Focused dark eyes
            AddFacetedBox(st, new Vector3(-0.030f, 0.122f, 0.086f), new Vector3(0.018f, 0.006f, 0.008f), Vector3.Zero, browColor);
            AddFacetedBox(st, new Vector3( 0.030f, 0.122f, 0.086f), new Vector3(0.018f, 0.006f, 0.008f), Vector3.Zero, browColor);

            // Sculpted ears
            AddFacetedBox(st, new Vector3(-0.076f, 0.105f, -0.005f), new Vector3(0.012f, 0.038f, 0.022f), new Vector3(0, -0.15f, 0), skinColor.Darkened(0.04f));
            AddFacetedBox(st, new Vector3( 0.076f, 0.105f, -0.005f), new Vector3(0.012f, 0.038f, 0.022f), new Vector3(0,  0.15f, 0), skinColor.Darkened(0.04f));

            st.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.78f, useVertexColors: true));
            return st.Commit();
        }

        private static ArrayMesh CreateTousledHairMesh(Color hairColor)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            // Base cranial hair cap covering crown, sides, and tapered nape
            var capRings = new[]
            {
                new RingSpec(new Vector3(0f, 0.055f, -0.052f), 0.062f, 0.045f, 0f, 1.0f, 0.004f), // Tapered nape
                new RingSpec(new Vector3(0f, 0.115f, -0.025f), 0.079f, 0.078f, 0.06f, 1.04f, 0.005f),
                new RingSpec(new Vector3(0f, 0.168f, -0.005f), 0.084f, 0.098f, -0.05f, 1.05f, 0.006f),
                new RingSpec(new Vector3(0f, 0.215f,  0.002f), 0.072f, 0.088f, 0.08f, 1.04f, 0.006f),
                new RingSpec(new Vector3(0f, 0.236f, -0.006f), 0.028f, 0.036f, 0f, 1.0f, 0.002f)
            };
            AppendLoftRingsToSurfaceTool(st, capRings, hairColor, sides: 9);

            // 16 layered low-poly wedge locks (including 3 front hanging locks parted slightly off-center + crown & side swept locks)
            (Vector3 root, Vector3 mid, Vector3 tip, float width, float thick, Color shade)[] locks = new[]
            {
                // 3 hanging front forehead locks (parted off-center to left)
                (new Vector3(-0.018f, 0.215f, 0.065f), new Vector3(-0.038f, 0.190f, 0.098f), new Vector3(-0.048f, 0.145f, 0.102f), 0.024f, 0.014f, hairColor.Lightened(0.06f)),
                (new Vector3( 0.008f, 0.220f, 0.068f), new Vector3(-0.008f, 0.192f, 0.102f), new Vector3(-0.016f, 0.150f, 0.104f), 0.022f, 0.013f, hairColor.Lightened(0.03f)),
                (new Vector3( 0.028f, 0.214f, 0.064f), new Vector3( 0.046f, 0.188f, 0.095f), new Vector3( 0.055f, 0.152f, 0.096f), 0.024f, 0.014f, hairColor),

                // Upper crown tousled spikes/wedges
                (new Vector3(-0.015f, 0.228f, 0.015f), new Vector3(-0.045f, 0.242f, 0.030f), new Vector3(-0.072f, 0.218f, 0.035f), 0.030f, 0.016f, hairColor.Lightened(0.08f)),
                (new Vector3( 0.015f, 0.230f, 0.010f), new Vector3( 0.048f, 0.244f, 0.022f), new Vector3( 0.076f, 0.220f, 0.025f), 0.030f, 0.016f, hairColor.Lightened(0.05f)),
                (new Vector3( 0.000f, 0.234f, -0.020f), new Vector3(-0.022f, 0.248f, -0.045f), new Vector3(-0.038f, 0.225f, -0.075f), 0.032f, 0.016f, hairColor.Lightened(0.07f)),
                (new Vector3( 0.022f, 0.228f, -0.025f), new Vector3( 0.045f, 0.238f, -0.055f), new Vector3( 0.058f, 0.205f, -0.082f), 0.028f, 0.015f, hairColor),

                // Left & Right temple/sideburn swept locks
                (new Vector3(-0.062f, 0.195f, 0.035f), new Vector3(-0.084f, 0.172f, 0.045f), new Vector3(-0.082f, 0.135f, 0.032f), 0.022f, 0.014f, hairColor.Darkened(0.04f)),
                (new Vector3( 0.062f, 0.195f, 0.035f), new Vector3( 0.084f, 0.172f, 0.045f), new Vector3( 0.082f, 0.135f, 0.032f), 0.022f, 0.014f, hairColor.Darkened(0.04f)),
                (new Vector3(-0.072f, 0.182f, -0.010f), new Vector3(-0.088f, 0.155f, -0.025f), new Vector3(-0.080f, 0.118f, -0.035f), 0.025f, 0.015f, hairColor.Darkened(0.06f)),
                (new Vector3( 0.072f, 0.182f, -0.010f), new Vector3( 0.088f, 0.155f, -0.025f), new Vector3( 0.080f, 0.118f, -0.035f), 0.025f, 0.015f, hairColor.Darkened(0.06f)),

                // Back / Occipital & Nape layered wedges
                (new Vector3(-0.035f, 0.195f, -0.072f), new Vector3(-0.045f, 0.160f, -0.095f), new Vector3(-0.038f, 0.115f, -0.090f), 0.030f, 0.016f, hairColor),
                (new Vector3( 0.035f, 0.195f, -0.072f), new Vector3( 0.045f, 0.160f, -0.095f), new Vector3( 0.038f, 0.115f, -0.090f), 0.030f, 0.016f, hairColor.Darkened(0.03f)),
                (new Vector3( 0.000f, 0.185f, -0.082f), new Vector3( 0.000f, 0.142f, -0.100f), new Vector3( 0.000f, 0.095f, -0.088f), 0.032f, 0.016f, hairColor.Lightened(0.04f)),
                (new Vector3(-0.024f, 0.125f, -0.082f), new Vector3(-0.026f, 0.088f, -0.085f), new Vector3(-0.018f, 0.048f, -0.072f), 0.024f, 0.012f, hairColor.Darkened(0.08f)),
                (new Vector3( 0.024f, 0.125f, -0.082f), new Vector3( 0.026f, 0.088f, -0.085f), new Vector3( 0.018f, 0.048f, -0.072f), 0.024f, 0.012f, hairColor.Darkened(0.08f))
            };

            foreach (var w in locks)
            {
                AddHairWedgeLock(st, w.root, w.mid, w.tip, w.width, w.thick, w.shade);
            }

            st.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.88f, useVertexColors: true));
            return st.Commit();
        }

        private static void BuildShoulderAndUpperArm(Node3D shoulderNode, Node3D upperArmNode, Color shirtColor, bool isLeft)
        {
            float sign = isLeft ? -1f : 1f;
            // Sculpted Henley shoulder seam cap
            var shoulderRings = new[]
            {
                new RingSpec(new Vector3(-sign * 0.02f, -0.04f, 0f), 0.062f, 0.064f, 0f, 1.0f, 0.002f),
                new RingSpec(new Vector3( sign * 0.02f,  0.01f, 0f), 0.068f, 0.068f, 0f, 1.02f, 0.003f),
                new RingSpec(new Vector3( sign * 0.045f, 0.035f, 0f), 0.042f, 0.045f, 0f, 1.0f, 0.002f)
            };
            AddMeshChild(shoulderNode, "ShoulderCap", CreateFacetedLoftMesh(shoulderRings, shirtColor, sides: 8, roughness: 0.85f));

            // UpperArm Henley sleeve (0.32m bone length) terminating in a chunky 2-ring folded/rolled cuff (radius 0.064m) at the elbow!
            var sleeveRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.02f, 0f), 0.060f, 0.062f,  0.04f, 1.02f, 0.003f),
                new RingSpec(new Vector3(0f, -0.10f, 0f), 0.056f, 0.058f, -0.06f, 1.03f, 0.004f),
                new RingSpec(new Vector3(0f, -0.21f, 0f), 0.053f, 0.055f,  0.05f, 1.03f, 0.004f),
                // Chunky 2-ring rolled/folded cuff just above/at elbow joint
                new RingSpec(new Vector3(0f, -0.255f, 0f), 0.064f, 0.064f, -0.04f, 1.05f, 0.004f),
                new RingSpec(new Vector3(0f, -0.315f, 0f), 0.063f, 0.063f,  0.04f, 1.05f, 0.004f),
                new RingSpec(new Vector3(0f, -0.328f, 0f), 0.048f, 0.048f,  0.00f, 1.00f, 0.002f)
            };
            AddMeshChild(upperArmNode, "UpperSleeveAndRolledCuff", CreateFacetedLoftMesh(sleeveRings, shirtColor, sides: 8, roughness: 0.86f));
        }

        private static void BuildBareForearm(Node3D lowerArmNode, Color skinColor, bool isLeft)
        {
            // 6-sided muscular forearm (radius 0.048m at brachioradialis ridge tapering cleanly to 0.033m at bare wrist)
            float twist = isLeft ? 0.08f : -0.08f;
            var forearmRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.01f, 0f), 0.046f, 0.046f, 0f, 1.04f, 0.002f),
                new RingSpec(new Vector3(0f, -0.07f, 0.003f), 0.049f, 0.047f, twist, 1.08f, 0.003f), // Brachioradialis muscle ridge
                new RingSpec(new Vector3(0f, -0.18f, 0.001f), 0.040f, 0.039f, twist * 0.5f, 1.05f, 0.002f),
                new RingSpec(new Vector3(0f, -0.29f, 0f), 0.033f, 0.031f, 0f, 1.02f, 0.001f)          // Bare sinewy wrist
            };
            AddMeshChild(lowerArmNode, "BareForearm", CreateFacetedLoftMesh(forearmRings, skinColor, sides: 6, roughness: 0.78f));
        }

        private static (MeshInstance3D clenched, MeshInstance3D relaxed) BuildHandMeshes(Node3D fistNode, Color skinColor, bool isLeft)
        {
            float sign = isLeft ? -1f : 1f;

            // 1. Clenched Fist for GUARD and combat (0.085m x 0.11m x 0.055m with distinct thumb plane)
            var stClenched = new SurfaceTool();
            stClenched.Begin(Mesh.PrimitiveType.Triangles);
            var fistRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.00f, 0.00f), 0.034f, 0.026f, 0f, 1.02f),
                new RingSpec(new Vector3(0f, -0.05f, 0.006f), 0.044f, 0.030f, 0f, 1.06f),
                new RingSpec(new Vector3(0f, -0.095f, 0.008f), 0.042f, 0.028f, 0f, 1.05f),
                new RingSpec(new Vector3(0f, -0.110f, 0.004f), 0.026f, 0.018f, 0f, 1.00f)
            };
            AppendLoftRingsToSurfaceTool(stClenched, fistRings, skinColor, sides: 6);
            // Distinct tucked thumb plane across front of fist
            AddFacetedBox(
                stClenched,
                new Vector3(-sign * 0.030f, -0.055f, 0.024f),
                new Vector3(0.022f, 0.046f, 0.022f),
                new Vector3(0.15f, 0f, sign * 0.25f),
                skinColor.Lightened(0.04f));
            stClenched.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.78f, useVertexColors: true));
            var clenchedInst = AddMeshChild(fistNode, "ClenchedFist", stClenched.Commit());

            // 2. Relaxed Open Hand for Turnaround FRONT / SIDE / BACK views
            var stRelaxed = new SurfaceTool();
            stRelaxed.Begin(Mesh.PrimitiveType.Triangles);
            var palmRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.00f, 0f), 0.033f, 0.024f, 0f, 1.02f),
                new RingSpec(new Vector3(0f, -0.065f, 0.003f), 0.043f, 0.023f, 0f, 1.04f),
                new RingSpec(new Vector3(0f, -0.130f, -0.004f), 0.036f, 0.016f, 0f, 1.02f),
                new RingSpec(new Vector3(0f, -0.150f, -0.008f), 0.020f, 0.010f, 0f, 1.00f)
            };
            AppendLoftRingsToSurfaceTool(stRelaxed, palmRings, skinColor, sides: 6);
            // Relaxed extended thumb
            AddFacetedBox(
                stRelaxed,
                new Vector3(-sign * 0.038f, -0.065f, 0.022f),
                new Vector3(0.018f, 0.056f, 0.018f),
                new Vector3(0.25f, 0f, -sign * 0.22f),
                skinColor.Lightened(0.04f));
            stRelaxed.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.78f, useVertexColors: true));
            var relaxedInst = AddMeshChild(fistNode, "RelaxedHand", stRelaxed.Commit());
            relaxedInst.Visible = false;

            return (clenchedInst, relaxedInst);
        }

        private static void BuildTailoredThigh(Node3D upperLegNode, Color slacksColor, bool isLeft)
        {
            // 0.46m bone length, diamond-octagonal cross-section with front (+Z) & back (-Z) tailored trouser crease ridge!
            var thighRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.02f, 0f), 0.086f, 0.088f, 0.00f, 1.14f, 0.003f),
                new RingSpec(new Vector3(0f, -0.14f, 0.004f), 0.082f, 0.084f, 0.03f, 1.16f, 0.004f),
                new RingSpec(new Vector3(0f, -0.32f, 0.002f), 0.070f, 0.074f, -0.02f, 1.16f, 0.003f),
                new RingSpec(new Vector3(0f, -0.46f, 0f), 0.062f, 0.066f, 0.00f, 1.14f, 0.002f)
            };
            AddMeshChild(upperLegNode, "TailoredThigh", CreateFacetedLoftMesh(thighRings, slacksColor, sides: 8, roughness: 0.88f));
        }

        private static void BuildCroppedCalfAndAnkle(Node3D lowerLegNode, Color slacksColor, Color skinColor, bool isLeft)
        {
            // LowerLeg bone is 0.42m long (world Y = 0.48m down to 0.06m).
            // Stop the trouser cuff at 82% of shin length (local Y = -0.345m, world Y = 0.11m above floor)
            // with crisp front/back tailored crease, and loft the remaining 18% down to the ankle as bare skin (#b68a67)!
            var calfTrouserRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.01f, 0f), 0.061f, 0.065f, 0.00f, 1.14f, 0.002f),
                new RingSpec(new Vector3(0f, -0.12f, -0.004f), 0.059f, 0.064f, 0.02f, 1.15f, 0.003f),
                new RingSpec(new Vector3(0f, -0.26f, -0.002f), 0.049f, 0.054f, -0.02f, 1.14f, 0.002f),
                new RingSpec(new Vector3(0f, -0.345f, 0f), 0.045f, 0.049f, 0.00f, 1.12f, 0.002f), // Cropped trouser hem at Y = 0.11m
                new RingSpec(new Vector3(0f, -0.350f, 0f), 0.036f, 0.038f, 0.00f, 1.02f, 0.001f)
            };
            AddMeshChild(lowerLegNode, "CroppedTrouserCalf", CreateFacetedLoftMesh(calfTrouserRings, slacksColor, sides: 8, roughness: 0.88f));

            // Exposed bare ankle skin (local Y = -0.345m down to -0.43m, world Y = 0.11m down to 0.03m inside shoe collar)
            var bareAnkleRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.342f, 0f), 0.037f, 0.039f, 0f, 1.04f, 0.001f),
                new RingSpec(new Vector3(0f, -0.390f, 0f), 0.035f, 0.037f, 0f, 1.04f, 0.001f),
                new RingSpec(new Vector3(0f, -0.430f, 0f), 0.036f, 0.040f, 0f, 1.02f, 0.001f)
            };
            AddMeshChild(lowerLegNode, "ExposedBareAnkle", CreateFacetedLoftMesh(bareAnkleRings, skinColor, sides: 8, roughness: 0.78f));
        }

        private static void BuildSlipOnLoafer(Node3D footNode, Color upperColor, Color soleColor, bool isLeft)
        {
            // Foot bone is at local Y = -0.42m, Z = +0.05m relative to LowerLeg (world Y ≈ 0.06m).
            // Build low-profile matte charcoal slip-on street shoe (0.095m wide x 0.255m long, reaching floor at local Y = -0.06m)
            // with contrasting #4a433c flat rubber sole strip!
            var stUpper = new SurfaceTool();
            stUpper.Begin(Mesh.PrimitiveType.Triangles);

            var loaferUpperRings = new[]
            {
                new RingSpec(new Vector3(0f,  0.005f, -0.025f), 0.042f, 0.068f, 0f, 1.06f, 0.002f), // Low-cut ankle collar exposing malleolus
                new RingSpec(new Vector3(0f, -0.022f,  0.022f), 0.048f, 0.118f, 0f, 1.08f, 0.002f), // Vamp & instep
                new RingSpec(new Vector3(0f, -0.044f,  0.028f), 0.049f, 0.126f, 0f, 1.06f, 0.001f)  // Upper-to-sole welt
            };
            AppendLoftRingsToSurfaceTool(stUpper, loaferUpperRings, upperColor, sides: 8);
            stUpper.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.86f, useVertexColors: true));
            AddMeshChild(footNode, "LoaferUpper", stUpper.Commit());

            // Contrasting flat rubber sole (#4a433c) from local Y = -0.044m down to -0.060m (floor contact)
            var stSole = new SurfaceTool();
            stSole.Begin(Mesh.PrimitiveType.Triangles);
            var soleRings = new[]
            {
                new RingSpec(new Vector3(0f, -0.043f, 0.028f), 0.050f, 0.128f, 0f, 1.06f),
                new RingSpec(new Vector3(0f, -0.060f, 0.028f), 0.050f, 0.128f, 0f, 1.06f),
                new RingSpec(new Vector3(0f, -0.061f, 0.028f), 0.010f, 0.020f, 0f, 1.00f)
            };
            AppendLoftRingsToSurfaceTool(stSole, soleRings, soleColor, sides: 8);
            stSole.SetMaterial(CreateFlatFacetMaterial(Colors.White, 0.92f, useVertexColors: true));
            AddMeshChild(footNode, "LoaferRubberSole", stSole.Commit());
        }

        /// <summary>
        /// Connects polygonal cross-section rings using unindexed triangles with per-triangle flat normals
        /// so every polygonal facet catches directional studio light crisply like reference_first_fighter.jpg.
        /// </summary>
        public static ArrayMesh CreateFacetedLoftMesh(
            RingSpec[] rings,
            Color baseColor,
            int sides = 8,
            float roughness = 0.85f)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            AppendLoftRingsToSurfaceTool(st, rings, baseColor, sides);
            st.SetMaterial(CreateFlatFacetMaterial(Colors.White, roughness, useVertexColors: true));
            return st.Commit();
        }

        private static void AppendLoftRingsToSurfaceTool(
            SurfaceTool st,
            RingSpec[] rings,
            Color baseColor,
            int sides)
        {
            if (rings == null || rings.Length < 2 || sides < 3)
            {
                return;
            }

            Vector3[][] ringVerts = new Vector3[rings.Length][];
            for (int r = 0; r < rings.Length; r++)
            {
                ringVerts[r] = new Vector3[sides];
                RingSpec spec = rings[r];
                for (int s = 0; s < sides; s++)
                {
                    float angle = (Mathf.Tau * s) / sides + spec.TwistRad;
                    float cosA = Mathf.Cos(angle);
                    float sinA = Mathf.Sin(angle);

                    // Project front (+Z) and back (-Z) vertices outward when FrontBackCreaseFactor > 1
                    // to sculpt the sharp vertical tailored trouser crease & anatomical ridges!
                    float zCrease = Mathf.Abs(sinA) > 0.82f ? spec.FrontBackCreaseFactor : 1.0f;

                    // Deterministic organic facet offset for triangulated cloth creases
                    float hash = Mathf.Sin((r * 7 + s * 13) * 1.618f);
                    float jitter = spec.FacetJitter * hash;

                    float vx = spec.Center.X + cosA * (spec.RadiusX + jitter);
                    float vy = spec.Center.Y + jitter * 0.45f;
                    float vz = spec.Center.Z + sinA * (spec.RadiusZ * zCrease + jitter);

                    ringVerts[r][s] = new Vector3(vx, vy, vz);
                }
            }

            // Connect adjacent rings with unindexed flat-shaded triangles
            for (int r = 0; r < rings.Length - 1; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int sNext = (s + 1) % sides;
                    Vector3 v00 = ringVerts[r][s];
                    Vector3 v01 = ringVerts[r][sNext];
                    Vector3 v10 = ringVerts[r + 1][s];
                    Vector3 v11 = ringVerts[r + 1][sNext];

                    // Subtle per-triangle facet value modulation matching reference_first_fighter.jpg
                    float facetShiftA = (((r + s) & 1) == 0) ? 0.032f : -0.028f;
                    float facetShiftB = (((r + s) & 1) == 0) ? -0.022f : 0.036f;

                    Color colA = ModulateFacetColor(baseColor, facetShiftA);
                    Color colB = ModulateFacetColor(baseColor, facetShiftB);

                    // Alternate diagonal triangulation for authentic Sifu low-poly cloth diamond facets
                    if (((r + s) & 1) == 0)
                    {
                        AddFlatTriangle(st, v00, v10, v11, colA);
                        AddFlatTriangle(st, v00, v11, v01, colB);
                    }
                    else
                    {
                        AddFlatTriangle(st, v00, v10, v01, colA);
                        AddFlatTriangle(st, v01, v10, v11, colB);
                    }
                }
            }

            // Cap top and bottom rings cleanly
            Vector3 botCenter = rings[0].Center;
            for (int s = 0; s < sides; s++)
            {
                int sNext = (s + 1) % sides;
                AddFlatTriangle(st, botCenter, ringVerts[0][sNext], ringVerts[0][s], baseColor.Darkened(0.06f));
            }

            int last = rings.Length - 1;
            Vector3 topCenter = rings[last].Center;
            for (int s = 0; s < sides; s++)
            {
                int sNext = (s + 1) % sides;
                AddFlatTriangle(st, topCenter, ringVerts[last][s], ringVerts[last][sNext], baseColor.Lightened(0.04f));
            }
        }

        private static void AddHairWedgeLock(
            SurfaceTool st,
            Vector3 root,
            Vector3 mid,
            Vector3 tip,
            float width,
            float thickness,
            Color color)
        {
            Vector3 dir = (tip - root).Normalized();
            Vector3 side = dir.Cross(Vector3.Up);
            if (side.LengthSquared() < 0.001f)
            {
                side = Vector3.Right;
            }
            side = side.Normalized() * (width * 0.5f);
            Vector3 outN = side.Cross(dir).Normalized() * (thickness * 0.5f);

            Vector3 rL = root - side;
            Vector3 rR = root + side;
            Vector3 mL = mid - side * 0.85f + outN;
            Vector3 mR = mid + side * 0.85f + outN;
            Vector3 mTop = mid + outN * 1.6f;

            AddFlatTriangle(st, rL, mTop, rR, color.Lightened(0.05f));
            AddFlatTriangle(st, rL, mL, mTop, color);
            AddFlatTriangle(st, mTop, mR, rR, color.Darkened(0.04f));
            AddFlatTriangle(st, mL, tip, mTop, color.Lightened(0.03f));
            AddFlatTriangle(st, mTop, tip, mR, color.Darkened(0.05f));
        }

        private static void AddFacetedBox(
            SurfaceTool st,
            Vector3 center,
            Vector3 size,
            Vector3 eulerRad,
            Color color)
        {
            Basis b = Basis.FromEuler(eulerRad);
            Vector3 hx = b.X * (size.X * 0.5f);
            Vector3 hy = b.Y * (size.Y * 0.5f);
            Vector3 hz = b.Z * (size.Z * 0.5f);

            Vector3 p000 = center - hx - hy - hz;
            Vector3 p001 = center - hx - hy + hz;
            Vector3 p010 = center - hx + hy - hz;
            Vector3 p011 = center - hx + hy + hz;
            Vector3 p100 = center + hx - hy - hz;
            Vector3 p101 = center + hx - hy + hz;
            Vector3 p110 = center + hx + hy - hz;
            Vector3 p111 = center + hx + hy + hz;

            // Front (+Z)
            AddFlatTriangle(st, p011, p001, p101, color.Lightened(0.04f));
            AddFlatTriangle(st, p011, p101, p111, color);
            // Back (-Z)
            AddFlatTriangle(st, p110, p100, p000, color.Darkened(0.06f));
            AddFlatTriangle(st, p110, p000, p010, color.Darkened(0.04f));
            // Top (+Y)
            AddFlatTriangle(st, p010, p011, p111, color.Lightened(0.06f));
            AddFlatTriangle(st, p010, p111, p110, color.Lightened(0.04f));
            // Bottom (-Y)
            AddFlatTriangle(st, p001, p000, p100, color.Darkened(0.08f));
            AddFlatTriangle(st, p001, p100, p101, color.Darkened(0.08f));
            // Left (-X)
            AddFlatTriangle(st, p010, p000, p001, color.Darkened(0.03f));
            AddFlatTriangle(st, p010, p001, p011, color);
            // Right (+X)
            AddFlatTriangle(st, p111, p101, p100, color);
            AddFlatTriangle(st, p111, p100, p110, color.Darkened(0.03f));
        }

        private static void AddFlatTriangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 normal = (b - a).Cross(c - a).Normalized();
            if (normal.LengthSquared() < 0.0001f)
            {
                normal = Vector3.Up;
            }
            st.SetNormal(normal);
            st.SetColor(color);
            st.AddVertex(a);

            st.SetNormal(normal);
            st.SetColor(color);
            st.AddVertex(b);

            st.SetNormal(normal);
            st.SetColor(color);
            st.AddVertex(c);
        }

        private static Color ModulateFacetColor(Color c, float delta)
        {
            return delta >= 0f ? c.Lightened(delta) : c.Darkened(-delta);
        }

        public static StandardMaterial3D CreateFlatFacetMaterial(Color albedo, float roughness = 0.85f, bool useVertexColors = false)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = albedo,
                Roughness = roughness,
                Metallic = 0.02f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
                VertexColorUseAsAlbedo = useVertexColors,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
        }

        private static MeshInstance3D AddMeshChild(Node3D parent, string name, Mesh mesh)
        {
            var inst = new MeshInstance3D
            {
                Name = name,
                Mesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            parent.AddChild(inst);
            return inst;
        }
    }
}
