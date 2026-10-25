using System;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    /// <summary>
    /// Binds Sim FighterState to the 19-bone faceted 3D rig, renders smear arcs, hitstop micro-shake,
    /// contact shadow decals, hit/parry sparks, and F1 3D Pushbox/Hurtbox/Hitbox wireframes.
    /// </summary>
    public partial class FighterVisualNode : Node3D
    {
        public BuiltFighterModel Model { get; private set; } = null!;
        public Node3D ModelPivot { get; private set; } = null!;

        private MeshInstance3D _smearMeshInstance = null!;
        private ImmediateMesh _smearImmediate = null!;
        private StandardMaterial3D _smearMaterial = null!;

        private MeshInstance3D _debugBoxInstance = null!;
        private ImmediateMesh _debugBoxImmediate = null!;
        private StandardMaterial3D _debugBoxMaterial = null!;

        private MeshInstance3D _contactShadowLeft = null!;
        private MeshInstance3D _contactShadowRight = null!;

        private MeshInstance3D _sparkBurstInstance = null!;
        private float _sparkTimer;
        private Color _sparkColor = new Color("fff0d6");
        private int _prevComboHits;
        private int _prevStructure;
        private int _prevMeter;

        public FighterMeshMode CurrentMeshMode { get; set; } = FighterMeshMode.ProceduralRig;

        public void Initialize(bool isPlayerTwoPalette)
        {
            ModelPivot = new Node3D { Name = "ModelPivot" };
            AddChild(ModelPivot);

            Model = FirstFighterModelBuilder.BuildCharacterRig(isPlayerTwoPalette);
            ModelPivot.AddChild(Model.Skeleton);

            // Translucent warm-sand Smear Arc mesh in ModelPivot space
            _smearImmediate = new ImmediateMesh();
            _smearMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            _smearMeshInstance = new MeshInstance3D
            {
                Name = "SmearArcMesh",
                Mesh = _smearImmediate,
                MaterialOverride = _smearMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            ModelPivot.AddChild(_smearMeshInstance);

            // 3D Debug Collision Wireframes (Pushbox = Green, Hurtbox = Blue, Hitbox = Red)
            _debugBoxImmediate = new ImmediateMesh();
            _debugBoxMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            _debugBoxInstance = new MeshInstance3D
            {
                Name = "DebugCollisionBoxes",
                Mesh = _debugBoxImmediate,
                MaterialOverride = _debugBoxMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_debugBoxInstance);

            // Soft contact shadow decals under each foot
            _contactShadowLeft = CreateContactShadowDisc("FootContactShadowL");
            _contactShadowRight = CreateContactShadowDisc("FootContactShadowR");
            AddChild(_contactShadowLeft);
            AddChild(_contactShadowRight);

            // Impact spark burst
            _sparkBurstInstance = CreateSparkBurstMesh();
            _sparkBurstInstance.Visible = false;
            AddChild(_sparkBurstInstance);
        }

        public void SyncFromSimState(FighterState state, int matchTick, bool showDebugBoxes, float deltaSec)
        {
            float worldX = state.X.ToMilli() * 0.001f;
            float worldY = state.Y.ToMilli() * 0.001f;

            // Hitstop micro-shake when receiving hit/block freeze
            float shakeX = 0f;
            if (state.Hitstop > 0 && (state.State == FighterStateKind.Hitstun || state.State == FighterStateKind.Blockstun))
            {
                shakeX = ((state.Hitstop & 1) == 0 ? 1f : -1f) * 0.018f;
            }

            Position = new Vector3(worldX + shakeX, worldY, 0f);

            // Orient model along the 2.5D X axis (+90 deg Y faces +X, -90 deg Y faces -X)
            ModelPivot.RotationDegrees = new Vector3(0f, state.FacingRight ? 90f : -90f, 0f);

            SkeletonPose pose = FighterPoseEngine.EvaluateFighterPose(state, matchTick);
            FighterPoseEngine.ApplyPoseToModel(Model, pose);

            bool isNeutralIdle = state.State == FighterStateKind.Idle && state.CurrentMoveId == MoveId.None;
            Model.SetMeshMode(CurrentMeshMode, inNeutralIdle: isNeutralIdle);

            UpdateSmearArc(pose);
            UpdateContactShadows();
            UpdateImpactSparks(state, deltaSec);
            UpdateDebugBoxes(state, showDebugBoxes);
        }

        public void SyncForTurnaroundInspector(
            Vector3 worldPos,
            float yawDegrees,
            SkeletonPose pose,
            FighterState? optionalBoxState,
            bool showDebugBoxes,
            FighterMeshMode? overrideMeshMode = null)
        {
            Position = worldPos;
            ModelPivot.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
            FighterPoseEngine.ApplyPoseToModel(Model, pose);

            var mode = overrideMeshMode ?? CurrentMeshMode;
            Model.SetMeshMode(mode, inNeutralIdle: false);

            UpdateSmearArc(pose);
            UpdateContactShadows();
            _sparkBurstInstance.Visible = false;

            if (optionalBoxState != null && showDebugBoxes)
            {
                UpdateDebugBoxes(optionalBoxState, true);
            }
            else
            {
                _debugBoxImmediate.ClearSurfaces();
            }
        }

        private void UpdateSmearArc(SkeletonPose pose)
        {
            _smearImmediate.ClearSurfaces();
            if (!pose.ShowSmearArc)
            {
                return;
            }

            _smearImmediate.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            Vector3 p0 = pose.SmearStartLocal;
            Vector3 p1 = pose.SmearMidLocal;
            Vector3 p2 = pose.SmearEndLocal;
            Vector3 innerOffset = new Vector3(0f, -0.09f, -0.14f);

            Color tailColor = new Color(0.95f, 0.90f, 0.82f, 0.12f);
            Color midColor = new Color(0.98f, 0.94f, 0.86f, 0.42f);
            Color tipColor = new Color(1.00f, 0.97f, 0.90f, 0.68f);

            // Quad 1: Windup -> Apex
            _smearImmediate.SurfaceSetColor(tailColor);
            _smearImmediate.SurfaceAddVertex(p0);
            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1);
            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1 + innerOffset);

            _smearImmediate.SurfaceSetColor(tailColor);
            _smearImmediate.SurfaceAddVertex(p0);
            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1 + innerOffset);
            _smearImmediate.SurfaceSetColor(tailColor);
            _smearImmediate.SurfaceAddVertex(p0 + innerOffset);

            // Quad 2: Apex -> Active
            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1);
            _smearImmediate.SurfaceSetColor(tipColor);
            _smearImmediate.SurfaceAddVertex(p2);
            _smearImmediate.SurfaceSetColor(tipColor);
            _smearImmediate.SurfaceAddVertex(p2 + innerOffset);

            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1);
            _smearImmediate.SurfaceSetColor(tipColor);
            _smearImmediate.SurfaceAddVertex(p2 + innerOffset);
            _smearImmediate.SurfaceSetColor(midColor);
            _smearImmediate.SurfaceAddVertex(p1 + innerOffset);

            _smearImmediate.SurfaceEnd();
        }

        private void UpdateContactShadows()
        {
            Node3D footL = Model.BoneAttachmentNodes[(int)FighterBone.FootL];
            Node3D footR = Model.BoneAttachmentNodes[(int)FighterBone.FootR];

            Vector3 localL = ToLocal(footL.GlobalPosition);
            Vector3 localR = ToLocal(footR.GlobalPosition);

            float groundY = -Position.Y + 0.003f;
            _contactShadowLeft.Position = new Vector3(localL.X, groundY, localL.Z);
            _contactShadowRight.Position = new Vector3(localR.X, groundY, localR.Z);

            float scaleL = Mathf.Clamp(1.0f - localL.Y * 0.6f, 0.35f, 1.0f);
            float scaleR = Mathf.Clamp(1.0f - localR.Y * 0.6f, 0.35f, 1.0f);
            _contactShadowLeft.Scale = new Vector3(scaleL, 1f, scaleL);
            _contactShadowRight.Scale = new Vector3(scaleR, 1f, scaleR);
        }

        private void UpdateImpactSparks(FighterState state, float deltaSec)
        {
            if (state.ComboHits > _prevComboHits && state.ComboHits > 0)
            {
                _sparkTimer = 0.14f;
                _sparkColor = new Color(1.0f, 0.82f, 0.42f, 0.9f);
                _sparkBurstInstance.Position = new Vector3(state.FacingRight ? 0.18f : -0.18f, 1.18f, 0.15f);
            }
            else if (state.State == FighterStateKind.Blockstun && state.Structure > _prevStructure)
            {
                _sparkTimer = 0.11f;
                _sparkColor = new Color(0.55f, 0.82f, 1.0f, 0.85f);
                _sparkBurstInstance.Position = new Vector3(state.FacingRight ? 0.22f : -0.22f, 1.24f, 0.15f);
            }
            else if (state.CurrentMoveId == MoveId.ParryCounter && state.MoveTick <= 2 && state.Meter > _prevMeter)
            {
                _sparkTimer = 0.18f;
                _sparkColor = new Color(1.0f, 0.92f, 0.35f, 0.95f);
                _sparkBurstInstance.Position = new Vector3(state.FacingRight ? 0.28f : -0.28f, 1.42f, 0.18f);
            }

            _prevComboHits = state.ComboHits;
            _prevStructure = state.Structure;
            _prevMeter = state.Meter;

            if (_sparkTimer > 0f)
            {
                _sparkTimer = Math.Max(0f, _sparkTimer - deltaSec);
                _sparkBurstInstance.Visible = true;
                float s = 0.6f + (0.18f - _sparkTimer) * 4.5f;
                _sparkBurstInstance.Scale = new Vector3(s, s, s);
                if (_sparkBurstInstance.MaterialOverride is StandardMaterial3D mat)
                {
                    mat.AlbedoColor = _sparkColor;
                }
            }
            else
            {
                _sparkBurstInstance.Visible = false;
            }
        }

        private void UpdateDebugBoxes(FighterState state, bool showDebugBoxes)
        {
            _debugBoxImmediate.ClearSurfaces();
            if (!showDebugBoxes)
            {
                return;
            }

            var def = FighterCatalog.FirstFighter;
            _debugBoxImmediate.SurfaceBegin(Mesh.PrimitiveType.Lines);

            // 1. Pushbox (Green)
            DrawBoxWireframe(def.Pushbox, state.FacingRight, new Color(0.18f, 0.92f, 0.36f, 0.90f), zDepth: -0.02f);

            // 2. Hurtboxes (Blue, or Gold when Invulnerable / Parry Active)
            Color hurtColor = (state.IsInvulnerable || state.IsParryActive)
                ? new Color(1.00f, 0.84f, 0.20f, 0.95f)
                : new Color(0.24f, 0.62f, 1.00f, 0.90f);

            Span<BoxDef> hurtboxes = stackalloc BoxDef[5];
            int count = state.GetCurrentHurtboxes(def, hurtboxes);
            for (int i = 0; i < count; i++)
            {
                DrawBoxWireframe(hurtboxes[i], state.FacingRight, hurtColor, zDepth: 0.02f + i * 0.005f);
            }

            // 3. Active Hitbox (Red)
            if (state.TryGetActiveHitbox(def, out BoxDef hitbox))
            {
                DrawBoxWireframe(hitbox, state.FacingRight, new Color(1.00f, 0.20f, 0.22f, 0.98f), zDepth: 0.06f);
            }

            _debugBoxImmediate.SurfaceEnd();
        }

        private void DrawBoxWireframe(BoxDef box, bool facingRight, Color color, float zDepth)
        {
            box.GetWorldBounds(0, 0, facingRight, out int minXMu, out int maxXMu, out int minYMu, out int maxYMu);
            float x0 = minXMu * 0.001f;
            float x1 = maxXMu * 0.001f;
            float y0 = minYMu * 0.001f;
            float y1 = maxYMu * 0.001f;
            float z0 = -0.14f + zDepth;
            float z1 = 0.14f + zDepth;

            Vector3 p000 = new Vector3(x0, y0, z0);
            Vector3 p100 = new Vector3(x1, y0, z0);
            Vector3 p110 = new Vector3(x1, y1, z0);
            Vector3 p010 = new Vector3(x0, y1, z0);

            Vector3 p001 = new Vector3(x0, y0, z1);
            Vector3 p101 = new Vector3(x1, y0, z1);
            Vector3 p111 = new Vector3(x1, y1, z1);
            Vector3 p011 = new Vector3(x0, y1, z1);

            AddLine(p000, p100, color);
            AddLine(p100, p110, color);
            AddLine(p110, p010, color);
            AddLine(p010, p000, color);

            AddLine(p001, p101, color);
            AddLine(p101, p111, color);
            AddLine(p111, p011, color);
            AddLine(p011, p001, color);

            AddLine(p000, p001, color);
            AddLine(p100, p101, color);
            AddLine(p110, p111, color);
            AddLine(p010, p011, color);
        }

        private void AddLine(Vector3 a, Vector3 b, Color c)
        {
            _debugBoxImmediate.SurfaceSetColor(c);
            _debugBoxImmediate.SurfaceAddVertex(a);
            _debugBoxImmediate.SurfaceSetColor(c);
            _debugBoxImmediate.SurfaceAddVertex(b);
        }

        private static MeshInstance3D CreateContactShadowDisc(string name)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            int segs = 12;
            float rx = 0.14f;
            float rz = 0.18f;
            Color centerCol = new Color("5e544d", 0.52f);
            Color edgeCol = new Color("5e544d", 0.0f);

            for (int i = 0; i < segs; i++)
            {
                float a0 = (Mathf.Tau * i) / segs;
                float a1 = (Mathf.Tau * (i + 1)) / segs;
                Vector3 v0 = new Vector3(Mathf.Cos(a0) * rx, 0f, Mathf.Sin(a0) * rz);
                Vector3 v1 = new Vector3(Mathf.Cos(a1) * rx, 0f, Mathf.Sin(a1) * rz);

                st.SetColor(centerCol);
                st.AddVertex(Vector3.Zero);
                st.SetColor(edgeCol);
                st.AddVertex(v0);
                st.SetColor(edgeCol);
                st.AddVertex(v1);
            }

            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            return new MeshInstance3D
            {
                Name = name,
                Mesh = st.Commit(),
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
        }

        private static MeshInstance3D CreateSparkBurstMesh()
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            int rays = 8;
            for (int i = 0; i < rays; i++)
            {
                float a = (Mathf.Tau * i) / rays;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 perp = new Vector3(-dir.Y, dir.X, 0f) * 0.025f;
                st.AddVertex(Vector3.Zero + perp);
                st.AddVertex(Vector3.Zero - perp);
                st.AddVertex(dir * 0.22f);
            }
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(1f, 0.88f, 0.45f, 0.9f),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            return new MeshInstance3D
            {
                Name = "HitSparkBurst",
                Mesh = st.Commit(),
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
        }
    }
}
