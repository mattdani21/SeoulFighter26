using System;
using Godot;

namespace FirstFighter.Game
{
    /// <summary>
    /// Cheonggyecheon night diorama for match play. Built entirely from code (no binary art).
    /// Geometry stays behind the ±3.2 m fighting lane: wet granite plaza, still canal band,
    /// far bank, pedestrian bridge silhouette, cool grey building masses, a low-contrast
    /// N Seoul Tower needle, and a few geometric emissive sign planes above head height.
    /// </summary>
    public partial class SeoulNightStage : Node3D
    {
        public static readonly Color PlazaColor = new Color("3a424c");
        public static readonly Color WaterColor = new Color("243848");
        public static readonly Color StoneColor = new Color("4a525c");
        public static readonly Color BuildingColor = new Color("343c48");
        public static readonly Color SkyZenith = new Color("101820");
        public static readonly Color NeonMagenta = new Color("d46aad");
        public static readonly Color NeonCyan = new Color("5ab8d4");
        public static readonly Color NeonAmber = new Color("d4a45a");

        private const float FloorWidth = 24f;
        private const float FloorDepth = 10f;

        private readonly System.Collections.Generic.List<StandardMaterial3D> _neonMats = new();
        private StandardMaterial3D _skyMat = null!;

        public override void _Ready()
        {
            BuildPlazaFloor();
            BuildCanalAndCoping();
            BuildFarBankAndBridge();
            BuildBuildingRow();
            BuildNSeoulTower();
            BuildNeonSigns();
            BuildNightSky();
        }

        /// <summary>
        /// Dim sky + neon during Super Art cinema; restore afterwards.
        /// </summary>
        public void SetSuperCinema(bool active)
        {
            float skyMul = active ? 0.62f : 1f;
            float neonMul = active ? 0.45f : 1f;
            _skyMat.AlbedoColor = SkyZenith * (1f / ProceduralTextures.NightSkyMean) * skyMul;
            foreach (var mat in _neonMats)
            {
                mat.EmissionEnergyMultiplier = 1.35f * neonMul;
            }
        }

        private void BuildPlazaFloor()
        {
            var floorMesh = new PlaneMesh { Size = new Vector2(FloorWidth, FloorDepth) };
            var floorMat = new StandardMaterial3D
            {
                AlbedoColor = PlazaColor * (1f / ProceduralTextures.NightGraniteMean),
                AlbedoTexture = ProceduralTextures.NightGraniteAlbedo(),
                Roughness = 0.48f,
                Metallic = 0.04f,
                NormalEnabled = true,
                NormalTexture = ProceduralTextures.NightGraniteNormal(),
                NormalScale = 0.28f,
                RoughnessTexture = ProceduralTextures.NightGraniteRoughness(),
                RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            AddChild(new MeshInstance3D
            {
                Name = "NightPlazaFloor",
                Mesh = floorMesh,
                MaterialOverride = floorMat,
                Position = new Vector3(0f, 0f, 1.2f)
            });
        }

        private void BuildCanalAndCoping()
        {
            // Still Cheonggyecheon channel as a perspective band behind the fighters (z ≈ −2.2 … −4.6).
            var waterMesh = new PlaneMesh { Size = new Vector2(26f, 2.5f) };
            var waterMat = new StandardMaterial3D
            {
                AlbedoColor = WaterColor * (1f / ProceduralTextures.NightWaterMean),
                AlbedoTexture = ProceduralTextures.NightWaterAlbedo(),
                Roughness = 0.18f,
                Metallic = 0.12f,
                EmissionEnabled = true,
                Emission = new Color("3a6080"),
                EmissionEnergyMultiplier = 0.22f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            AddChild(new MeshInstance3D
            {
                Name = "CheonggyecheonWater",
                Mesh = waterMesh,
                MaterialOverride = waterMat,
                Position = new Vector3(0f, -0.16f, -3.4f)
            });

            AddBox(
                "NearCoping",
                size: new Vector3(26f, 0.14f, 0.32f),
                position: new Vector3(0f, 0.04f, -2.05f),
                color: StoneColor);

            AddBox(
                "FarCoping",
                size: new Vector3(26f, 0.16f, 0.34f),
                position: new Vector3(0f, 0.02f, -4.72f),
                color: StoneColor);
        }

        private void BuildFarBankAndBridge()
        {
            AddBox(
                "FarBank",
                size: new Vector3(28f, 0.65f, 1.8f),
                position: new Vector3(0f, 0.22f, -5.7f),
                color: StoneColor * 0.95f);

            // Pedestrian bridge off to the side so piers never stack on fighter silhouettes.
            const float bridgeX = -6.4f;
            AddBox(
                "BridgeDeck",
                size: new Vector3(3.6f, 0.14f, 2.8f),
                position: new Vector3(bridgeX, 1.15f, -3.5f),
                color: new Color("3a424c"));

            AddBox(
                "BridgeRailL",
                size: new Vector3(3.6f, 0.34f, 0.06f),
                position: new Vector3(bridgeX, 1.40f, -2.2f),
                color: new Color("4a5460"));

            AddBox(
                "BridgeRailR",
                size: new Vector3(3.6f, 0.34f, 0.06f),
                position: new Vector3(bridgeX, 1.40f, -4.8f),
                color: new Color("4a5460"));

            AddBox("BridgePierL", new Vector3(0.16f, 1.15f, 0.16f), new Vector3(bridgeX - 1.1f, 0.40f, -3.5f), new Color("323840"));
            AddBox("BridgePierR", new Vector3(0.16f, 1.15f, 0.16f), new Vector3(bridgeX + 1.1f, 0.40f, -3.5f), new Color("323840"));
        }

        private void BuildBuildingRow()
        {
            float[] xs = { -9.5f, -5.2f, -1.0f, 3.4f, 7.8f };
            float[] widths = { 3.6f, 3.2f, 3.8f, 3.0f, 3.4f };
            float[] heights = { 4.2f, 5.6f, 4.8f, 6.2f, 5.0f };
            float[] depths = { 2.4f, 2.2f, 2.6f, 2.0f, 2.3f };

            for (int i = 0; i < xs.Length; i++)
            {
                float h = heights[i];
                float z = -7.6f - (i % 2) * 0.35f;
                AddBox(
                    $"Building_{i}",
                    size: new Vector3(widths[i], h, depths[i]),
                    position: new Vector3(xs[i], h * 0.5f - 0.05f, z),
                    color: BuildingColor.Darkened(0.03f * (i % 3)));

                var windowMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color("243040"),
                    EmissionEnabled = true,
                    Emission = new Color("7a8aa4"),
                    EmissionEnergyMultiplier = 0.32f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
                AddChild(new MeshInstance3D
                {
                    Name = $"BuildingWindows_{i}",
                    Mesh = new BoxMesh { Size = new Vector3(widths[i] * 0.72f, h * 0.55f, 0.04f) },
                    MaterialOverride = windowMat,
                    Position = new Vector3(xs[i], h * 0.48f, z + depths[i] * 0.5f + 0.02f)
                });
            }
        }

        private void BuildNSeoulTower()
        {
            var shaftMat = MakeUnshaded(new Color("2a323e"));
            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerShaft",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.07f,
                    BottomRadius = 0.16f,
                    Height = 4.8f,
                    RadialSegments = 8
                },
                MaterialOverride = shaftMat,
                Position = new Vector3(5.8f, 4.6f, -11.5f)
            });

            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerCabin",
                Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.35f, 0.55f) },
                MaterialOverride = MakeUnshaded(new Color("343c48")),
                Position = new Vector3(5.8f, 6.7f, -11.5f)
            });

            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerSpire",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.02f,
                    BottomRadius = 0.05f,
                    Height = 1.4f,
                    RadialSegments = 6
                },
                MaterialOverride = shaftMat,
                Position = new Vector3(5.8f, 7.55f, -11.5f)
            });
        }

        private void BuildNeonSigns()
        {
            // Keep signs above the 1.85 m fighter head but well inside the 36° match FOV
            // (high mounts clipped under the HUD). Prefer BoxMesh so facing cannot cull.
            PlaceNeonSign("NeonMagenta", NeonMagenta, new Vector3(-4.6f, 2.55f, -5.9f), new Vector3(1.9f, 0.48f, 0.08f));
            PlaceNeonSign("NeonCyan", NeonCyan, new Vector3(0.2f, 2.35f, -5.75f), new Vector3(1.6f, 0.42f, 0.08f));
            PlaceNeonSign("NeonAmber", NeonAmber, new Vector3(4.2f, 2.70f, -5.95f), new Vector3(1.35f, 0.38f, 0.08f));
        }

        private void PlaceNeonSign(string name, Color neon, Vector3 position, Vector3 size)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = neon,
                EmissionEnabled = true,
                Emission = neon,
                EmissionEnergyMultiplier = 1.35f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Roughness = 0.35f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            _neonMats.Add(mat);

            AddChild(new MeshInstance3D
            {
                Name = name,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = mat,
                Position = position
            });

            AddBox(
                name + "Frame",
                size: new Vector3(size.X + 0.10f, size.Y + 0.10f, size.Z + 0.04f),
                position: position + new Vector3(0f, 0f, -0.06f),
                color: new Color("14181e"));
        }

        private void BuildNightSky()
        {
            _skyMat = new StandardMaterial3D
            {
                AlbedoColor = SkyZenith * (1f / ProceduralTextures.NightSkyMean),
                AlbedoTexture = ProceduralTextures.NightSkyAlbedo(),
                Roughness = 1f,
                Metallic = 0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };

            AddChild(new MeshInstance3D
            {
                Name = "NightSkyWall",
                Mesh = new PlaneMesh { Size = new Vector2(36f, 16f) },
                MaterialOverride = _skyMat,
                Position = new Vector3(0f, 6.5f, -13.5f),
                RotationDegrees = new Vector3(90f, 0f, 0f)
            });
        }

        private void AddBox(string name, Vector3 size, Vector3 position, Color color)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = color * (1f / ProceduralTextures.NightStoneMean),
                AlbedoTexture = ProceduralTextures.NightStoneAlbedo(),
                Roughness = 0.88f,
                Metallic = 0.0f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            AddChild(new MeshInstance3D
            {
                Name = name,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = mat,
                Position = position
            });
        }

        private static StandardMaterial3D MakeUnshaded(Color color)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
        }
    }
}
