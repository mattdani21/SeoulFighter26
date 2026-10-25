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
        public static readonly Color NightAmbientColor = new Color("243048");
        public static readonly Color NightKeyColor = new Color("ffe8cc");
        public static readonly Color NightFillColor = new Color("7a9ab8");
        public static readonly Color NightRimColor = new Color("5ac8d4");
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

        /// <summary>0..1 bias applied during K.O. finish for a slight dramatic zoom.</summary>
        public float KoCameraZoom { get; set; }

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
                AmbientLightEnergy = 0.52f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                TonemapExposure = 1.0f,
                GlowEnabled = true,
                GlowIntensity = 0.55f,
                GlowStrength = 0.85f,
                GlowBloom = 0.18f,
                GlowHdrThreshold = 0.72f,
                FogEnabled = true,
                FogLightColor = new Color("1a2438"),
                FogDensity = 0.0085f,
                FogAerialPerspective = 0.35f,
                FogSkyAffect = 0.15f
            };
            _worldEnv = new WorldEnvironment
            {
                Name = "ArenaWorldEnvironment",
                Environment = env
            };
            AddChild(_worldEnv);

            // Warm streetlamp key so oatmeal henley / skin stay readable against cool night fill.
            _keyLight = new DirectionalLight3D
            {
                Name = "KeyLight3D",
                LightColor = NightKeyColor,
                LightEnergy = 1.05f,
                ShadowEnabled = true,
                ShadowOpacity = 0.36f,
                RotationDegrees = new Vector3(-44f, -28f, 0f)
            };
            AddChild(_keyLight);

            // Cool Cheonggyecheon water-bounce fill
            _fillLight = new DirectionalLight3D
            {
                Name = "FillLight3D",
                LightColor = NightFillColor,
                LightEnergy = 0.42f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-22f, 148f, 0f)
            };
            AddChild(_fillLight);

            // Crisp cyan/amber city rim so faceted limbs catch neon
            _rimLight = new DirectionalLight3D
            {
                Name = "RimLight3D",
                LightColor = NightRimColor,
                LightEnergy = 0.42f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-55f, 175f, 0f)
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
            var env = _worldEnv.Environment;
            env.BackgroundColor = SeoulNightStage.SkyZenith;
            env.AmbientLightColor = NightAmbientColor;
            env.AmbientLightEnergy = 0.52f;
            env.GlowEnabled = true;
            env.FogEnabled = true;
            _keyLight.LightColor = NightKeyColor;
            _keyLight.LightEnergy = 1.05f;
            _fillLight.LightColor = NightFillColor;
            _fillLight.LightEnergy = 0.42f;
            _rimLight.LightColor = NightRimColor;
            _rimLight.LightEnergy = 0.42f;
            _seoulStage.SetSuperCinema(false);
        }

        private void ApplyStudioTurnaroundLook()
        {
            _inspectorMode = true;
            _studioRoot.Visible = true;
            _seoulStage.Visible = false;
            var env = _worldEnv.Environment;
            env.BackgroundColor = StudioBackdropColor;
            env.AmbientLightColor = StudioAmbientColor;
            env.AmbientLightEnergy = 0.58f;
            env.GlowEnabled = false;
            env.FogEnabled = false;
            _keyLight.LightColor = new Color("fff7ed");
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
                _keyLight.LightEnergy = 1.05f;
                _worldEnv.Environment.BackgroundColor = SeoulNightStage.SkyZenith;
                _seoulStage.SetSuperCinema(false);
                if (KoCameraZoom > 0.01f)
                {
                    targetZ = Mathf.Lerp(targetZ, 2.70f, Mathf.Clamp(KoCameraZoom, 0f, 1f));
                }
            }

            Vector3 desiredCam = new Vector3(midX, targetY, targetZ);
            ArenaCamera.Position = ArenaCamera.Position.Lerp(desiredCam, Mathf.Clamp(deltaSec * 12f, 0f, 1f));
            ArenaCamera.LookAt(new Vector3(midX, 0.98f + maxAirY * 0.35f, 0f), Vector3.Up);
        }
    }
}
