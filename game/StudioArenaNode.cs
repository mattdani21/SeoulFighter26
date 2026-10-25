using System;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    /// <summary>
    /// Sloclap (Sifu / Absolver) Studio Arena environment matching reference_first_fighter.jpg:
    /// #867970 warm taupe-grey backdrop, #8e8178 matte studio floor, 3-point directional lighting
    /// for crisp flat-shaded facets, and dynamic 2.5D + Super zoom camera.
    ///
    /// Surfaces are textured from code (see <see cref="ProceduralTextures"/>): the floor carries
    /// concrete albedo, aggregate normal and roughness variation with the 1m reference grid baked
    /// straight into the albedo, and the cyclorama wall carries plaster grain plus a world-space
    /// grounding fade. Baking the grid into the albedo removes the thin-line geometry that used to
    /// shimmer and z-fight against the floor plane.
    /// </summary>
    public partial class StudioArenaNode : Node3D
    {
        public static readonly Color BackdropColor = new Color("867970");
        public static readonly Color FloorColor = new Color("8e8178");

        // The floor plane is 24m x 16m and its texture is authored 1:1 against those extents, so
        // the baked metre grid stays exactly 1m regardless of camera or UV scale.
        private const float FloorWidth = 24f;
        private const float FloorDepth = 16f;
        private const float WallWidth = 28f;
        private const float WallHeight = 12f;
        private const float WallBottomY = -0.5f;

        public Camera3D ArenaCamera { get; private set; } = null!;
        private DirectionalLight3D _keyLight = null!;
        private DirectionalLight3D _fillLight = null!;
        private DirectionalLight3D _rimLight = null!;
        private WorldEnvironment _worldEnv = null!;

        public override void _Ready()
        {
            BuildEnvironmentAndLighting();
            BuildStudioFloorAndGrid();
            BuildCameraRig();
        }

        private void BuildEnvironmentAndLighting()
        {
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = BackdropColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("9c9088"),
                AmbientLightEnergy = 0.58f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                TonemapExposure = 1.0f
            };
            _worldEnv = new WorldEnvironment
            {
                Name = "StudioWorldEnvironment",
                Environment = env
            };
            AddChild(_worldEnv);

            // Key Light: warm studio top-front-left directional light that brings out flat polygon facets without blowouts
            _keyLight = new DirectionalLight3D
            {
                Name = "KeyLight3D",
                LightColor = new Color("fff7ed"),
                LightEnergy = 0.92f,
                ShadowEnabled = true,
                ShadowOpacity = 0.42f,
                RotationDegrees = new Vector3(-46f, -32f, 0f)
            };
            AddChild(_keyLight);

            // Fill Light: soft cool-taupe opposite fill so shadow facets match #9c8c7b / #221f1c
            _fillLight = new DirectionalLight3D
            {
                Name = "FillLight3D",
                LightColor = new Color("d8cfc8"),
                LightEnergy = 0.36f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-28f, 142f, 0f)
            };
            AddChild(_fillLight);

            // Rim Light: subtle back-top separation rim
            _rimLight = new DirectionalLight3D
            {
                Name = "RimLight3D",
                LightColor = new Color("efe6dc"),
                LightEnergy = 0.28f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-62f, 180f, 0f)
            };
            AddChild(_rimLight);
        }

        private void BuildStudioFloorAndGrid()
        {
            // Matte studio floor: concrete albedo (metre grid baked in), aggregate normal map and
            // roughness variation. The texture is authored 1:1 against the plane extents, so no UV
            // scaling is applied and the grid can never drift out of alignment or alias against
            // geometry.
            var floorMesh = new PlaneMesh
            {
                Size = new Vector2(FloorWidth, FloorDepth)
            };
            var floorMat = new StandardMaterial3D
            {
                // Divided by the texture's mean so the authored palette value lands exactly.
                AlbedoColor = FloorColor * (1f / ProceduralTextures.FloorMean),
                AlbedoTexture = ProceduralTextures.StageFloorAlbedo(),
                Roughness = 0.94f,
                Metallic = 0.0f,
                NormalEnabled = true,
                NormalTexture = ProceduralTextures.StageFloorNormal(),
                NormalScale = 0.35f,
                RoughnessTexture = ProceduralTextures.StageFloorRoughness(),
                RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            var floorInst = new MeshInstance3D
            {
                Name = "StudioFloor",
                Mesh = floorMesh,
                MaterialOverride = floorMat
            };
            AddChild(floorInst);

            BuildBackdropWall();
        }

        private void BuildBackdropWall()
        {
            var wallMesh = new PlaneMesh
            {
                Size = new Vector2(WallWidth, WallHeight)
            };

            // Unshaded, like a lit cyclorama rather than a lit object. The plaster grain and the
            // grounding fade are baked into the albedo texture (which runs v = 0 at the top edge for
            // this +90 deg X rotation), so the wall needs no custom shader and its colour is exact:
            // AlbedoColor is divided by the detail mean, exactly as the character materials are.
            var wallMat = new StandardMaterial3D
            {
                AlbedoColor = BackdropColor * (1f / ProceduralTextures.WallMean),
                AlbedoTexture = ProceduralTextures.StageWallAlbedo(),
                Roughness = 0.96f,
                Metallic = 0.0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };

            var wallInst = new MeshInstance3D
            {
                Name = "StudioBackdropWall",
                Mesh = wallMesh,
                MaterialOverride = wallMat,
                Position = new Vector3(0f, WallHeight * 0.5f + WallBottomY, -3.8f),
                RotationDegrees = new Vector3(90f, 0f, 0f)
            };
            AddChild(wallInst);
        }

        private void BuildCameraRig()
        {
            ArenaCamera = new Camera3D
            {
                Name = "ArenaCamera3D",
                Fov = 36f,
                Position = new Vector3(0f, 1.18f, 3.85f),
                Current = true
            };
            ArenaCamera.LookAtFromPosition(ArenaCamera.Position, new Vector3(0f, 1.02f, 0f), Vector3.Up);
            AddChild(ArenaCamera);
        }

        public void UpdateCameraForMatch(MatchState state, bool turnaroundInspectorActive, float deltaSec)
        {
            if (turnaroundInspectorActive)
            {
                // Frame the 4-panel turnaround lineup + active move inspector cleanly
                Vector3 inspectorPos = new Vector3(0f, 1.06f, 3.65f);
                ArenaCamera.Position = ArenaCamera.Position.Lerp(inspectorPos, Mathf.Clamp(deltaSec * 10f, 0f, 1f));
                ArenaCamera.LookAt(new Vector3(0f, 0.98f, 0f), Vector3.Up);
                _keyLight.LightEnergy = 0.92f;
                return;
            }

            float p1X = state.Fighters[0].X.ToMilli() * 0.001f;
            float p2X = state.Fighters[1].X.ToMilli() * 0.001f;
            float p1Y = state.Fighters[0].Y.ToMilli() * 0.001f;
            float p2Y = state.Fighters[1].Y.ToMilli() * 0.001f;

            float midX = (p1X + p2X) * 0.5f;
            float maxAirY = Mathf.Max(p1Y, p2Y);
            float spanX = Mathf.Abs(p1X - p2X);

            // Dramatic 24-tick studio camera zoom & spotlight dim during 236H+S Super Art!
            bool superCinema = state.SuperFlashTicks > 0 ||
                               state.Fighters[0].CurrentMoveId == MoveId.SuperArt ||
                               state.Fighters[1].CurrentMoveId == MoveId.SuperArt;

            float targetZ = Mathf.Clamp(3.15f + spanX * 0.45f, 3.15f, 4.85f);
            float targetY = 1.12f + maxAirY * 0.45f;

            if (superCinema)
            {
                targetZ = 2.55f;
                targetY = 1.18f;
                _keyLight.LightEnergy = 1.15f;
                _worldEnv.Environment.BackgroundColor = BackdropColor.Darkened(0.28f);
            }
            else
            {
                _keyLight.LightEnergy = 0.92f;
                _worldEnv.Environment.BackgroundColor = BackdropColor;
            }

            Vector3 desiredCam = new Vector3(midX, targetY, targetZ);
            ArenaCamera.Position = ArenaCamera.Position.Lerp(desiredCam, Mathf.Clamp(deltaSec * 12f, 0f, 1f));
            ArenaCamera.LookAt(new Vector3(midX, 0.98f + maxAirY * 0.35f, 0f), Vector3.Up);
        }
    }
}
