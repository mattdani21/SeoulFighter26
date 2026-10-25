using System;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    /// <summary>
    /// Arena host: Cheonggyecheon night for match play, Sloclap taupe cyclorama for F9 turnaround.
    /// Owns the 3-point light rig and dynamic 2.5D + Super zoom camera.
    /// </summary>
    public partial class StudioArenaNode : Node3D
    {
        public static readonly Color StudioBackdropColor = new Color("867970");
        public static readonly Color StudioFloorColor = new Color("8e8178");
        public static readonly Color NightAmbientColor = new Color("2a3348");
        public static readonly Color NightFillColor = new Color("a8b8d0");
        public static readonly Color NightRimColor = new Color("c8d4e4");
        public static readonly Color StudioAmbientColor = new Color("9c9088");
        public static readonly Color StudioFillColor = new Color("d8cfc8");
        public static readonly Color StudioRimColor = new Color("efe6dc");

        // Kept for callers / evidence docs that still name the studio palette.
        public static readonly Color BackdropColor = StudioBackdropColor;
        public static readonly Color FloorColor = StudioFloorColor;

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
        private Node3D _studioRoot = null!;
        private SeoulNightStage _seoulStage = null!;
        private bool _inspectorMode;

        public override void _Ready()
        {
            BuildEnvironmentAndLighting();
            BuildStudioRoot();
            BuildSeoulStage();
            BuildCameraRig();
            ApplyMatchNightLook();
        }

        private void BuildEnvironmentAndLighting()
        {
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = SeoulNightStage.SkyZenith,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = NightAmbientColor,
                AmbientLightEnergy = 0.58f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                TonemapExposure = 1.0f
            };
            _worldEnv = new WorldEnvironment
            {
                Name = "ArenaWorldEnvironment",
                Environment = env
            };
            AddChild(_worldEnv);

            // Warm key preserved so the oatmeal henley and skin still separate from cool night fill.
            _keyLight = new DirectionalLight3D
            {
                Name = "KeyLight3D",
                LightColor = new Color("fff7ed"),
                LightEnergy = 0.98f,
                ShadowEnabled = true,
                ShadowOpacity = 0.38f,
                RotationDegrees = new Vector3(-46f, -32f, 0f)
            };
            AddChild(_keyLight);

            _fillLight = new DirectionalLight3D
            {
                Name = "FillLight3D",
                LightColor = NightFillColor,
                LightEnergy = 0.38f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-28f, 142f, 0f)
            };
            AddChild(_fillLight);

            _rimLight = new DirectionalLight3D
            {
                Name = "RimLight3D",
                LightColor = NightRimColor,
                LightEnergy = 0.30f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-62f, 180f, 0f)
            };
            AddChild(_rimLight);
        }

        private void BuildStudioRoot()
        {
            _studioRoot = new Node3D { Name = "StudioCyclorama", Visible = false };
            AddChild(_studioRoot);

            var floorMesh = new PlaneMesh { Size = new Vector2(FloorWidth, FloorDepth) };
            var floorMat = new StandardMaterial3D
            {
                AlbedoColor = StudioFloorColor * (1f / ProceduralTextures.FloorMean),
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
            _studioRoot.AddChild(new MeshInstance3D
            {
                Name = "StudioFloor",
                Mesh = floorMesh,
                MaterialOverride = floorMat
            });

            var wallMat = new StandardMaterial3D
            {
                AlbedoColor = StudioBackdropColor * (1f / ProceduralTextures.WallMean),
                AlbedoTexture = ProceduralTextures.StageWallAlbedo(),
                Roughness = 0.96f,
                Metallic = 0.0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            _studioRoot.AddChild(new MeshInstance3D
            {
                Name = "StudioBackdropWall",
                Mesh = new PlaneMesh { Size = new Vector2(WallWidth, WallHeight) },
                MaterialOverride = wallMat,
                Position = new Vector3(0f, WallHeight * 0.5f + WallBottomY, -3.8f),
                RotationDegrees = new Vector3(90f, 0f, 0f)
            });
        }

        private void BuildSeoulStage()
        {
            _seoulStage = new SeoulNightStage { Name = "SeoulNightStage" };
            AddChild(_seoulStage);
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

        private void ApplyMatchNightLook()
        {
            _inspectorMode = false;
            _studioRoot.Visible = false;
            _seoulStage.Visible = true;
            _worldEnv.Environment.BackgroundColor = SeoulNightStage.SkyZenith;
            _worldEnv.Environment.AmbientLightColor = NightAmbientColor;
            _worldEnv.Environment.AmbientLightEnergy = 0.58f;
            _keyLight.LightEnergy = 0.98f;
            _fillLight.LightColor = NightFillColor;
            _fillLight.LightEnergy = 0.38f;
            _rimLight.LightColor = NightRimColor;
            _rimLight.LightEnergy = 0.30f;
            _seoulStage.SetSuperCinema(false);
        }

        private void ApplyStudioTurnaroundLook()
        {
            _inspectorMode = true;
            _studioRoot.Visible = true;
            _seoulStage.Visible = false;
            _worldEnv.Environment.BackgroundColor = StudioBackdropColor;
            _worldEnv.Environment.AmbientLightColor = StudioAmbientColor;
            _worldEnv.Environment.AmbientLightEnergy = 0.58f;
            _keyLight.LightEnergy = 0.92f;
            _fillLight.LightColor = StudioFillColor;
            _fillLight.LightEnergy = 0.36f;
            _rimLight.LightColor = StudioRimColor;
            _rimLight.LightEnergy = 0.28f;
        }

        public void UpdateCameraForMatch(MatchState state, bool turnaroundInspectorActive, float deltaSec)
        {
            if (turnaroundInspectorActive)
            {
                if (!_inspectorMode)
                {
                    ApplyStudioTurnaroundLook();
                }

                Vector3 inspectorPos = new Vector3(0f, 1.06f, 3.65f);
                ArenaCamera.Position = ArenaCamera.Position.Lerp(inspectorPos, Mathf.Clamp(deltaSec * 10f, 0f, 1f));
                ArenaCamera.LookAt(new Vector3(0f, 0.98f, 0f), Vector3.Up);
                _keyLight.LightEnergy = 0.92f;
                return;
            }

            if (_inspectorMode)
            {
                ApplyMatchNightLook();
            }

            float p1X = state.Fighters[0].X.ToMilli() * 0.001f;
            float p2X = state.Fighters[1].X.ToMilli() * 0.001f;
            float p1Y = state.Fighters[0].Y.ToMilli() * 0.001f;
            float p2Y = state.Fighters[1].Y.ToMilli() * 0.001f;

            float midX = (p1X + p2X) * 0.5f;
            float maxAirY = Mathf.Max(p1Y, p2Y);
            float spanX = Mathf.Abs(p1X - p2X);

            bool superCinema = state.SuperFlashTicks > 0 ||
                               state.Fighters[0].CurrentMoveId == MoveId.SuperArt ||
                               state.Fighters[1].CurrentMoveId == MoveId.SuperArt;

            float targetZ = Mathf.Clamp(3.15f + spanX * 0.45f, 3.15f, 4.85f);
            float targetY = 1.12f + maxAirY * 0.45f;

            if (superCinema)
            {
                targetZ = 2.55f;
                targetY = 1.18f;
                _keyLight.LightEnergy = 1.18f;
                _worldEnv.Environment.BackgroundColor = SeoulNightStage.SkyZenith.Darkened(0.35f);
                _seoulStage.SetSuperCinema(true);
            }
            else
            {
                _keyLight.LightEnergy = 0.98f;
                _worldEnv.Environment.BackgroundColor = SeoulNightStage.SkyZenith;
                _seoulStage.SetSuperCinema(false);
            }

            Vector3 desiredCam = new Vector3(midX, targetY, targetZ);
            ArenaCamera.Position = ArenaCamera.Position.Lerp(desiredCam, Mathf.Clamp(deltaSec * 12f, 0f, 1f));
            ArenaCamera.LookAt(new Vector3(midX, 0.98f + maxAirY * 0.35f, 0f), Vector3.Up);
        }
    }
}
