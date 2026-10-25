using System;
using Godot;

namespace FirstFighter.Game
{
    /// <summary>
    /// Cheonggyecheon night diorama for match play. Built entirely from code (no binary art).
    /// Stepped stone promenade, reflective canal, girder bridge, multi-bay mid-rises, framed neon,
    /// promenade bollards, and N Seoul Tower — all behind the ±3.2 m fighting lane.
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
        public static readonly Color WarmLed = new Color("ffc878");

        private const float FloorWidth = 24f;
        private const float FloorDepth = 10f;

        private readonly System.Collections.Generic.List<StandardMaterial3D> _neonMats = new();
        private readonly System.Collections.Generic.List<OmniLight3D> _warmLights = new();
        private StandardMaterial3D _skyMat = null!;

        public override void _Ready()
        {
            BuildPlazaFloor();
            BuildSteppedPromenadeAndCanal();
            BuildBridgeAndBollards();
            BuildBuildingRow();
            BuildNSeoulTower();
            BuildFramedNeonSigns();
            BuildNightSky();
        }

        public void SetSuperCinema(bool active)
        {
            float skyMul = active ? 0.62f : 1f;
            float neonMul = active ? 0.45f : 1f;
            float warmMul = active ? 0.55f : 1f;
            _skyMat.AlbedoColor = SkyZenith * (1f / ProceduralTextures.NightSkyMean) * skyMul;
            foreach (var mat in _neonMats)
            {
                mat.EmissionEnergyMultiplier = 1.35f * neonMul;
            }
            foreach (var light in _warmLights)
            {
                light.LightEnergy = light.GetMeta("base_energy", 0.55f).AsSingle() * warmMul;
            }
        }

        private void BuildPlazaFloor()
        {
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
                Mesh = new PlaneMesh { Size = new Vector2(FloorWidth, FloorDepth) },
                MaterialOverride = floorMat,
                Position = new Vector3(0f, 0f, 1.2f)
            });
        }

        private void BuildSteppedPromenadeAndCanal()
        {
            // Low near terrace so the canal band stays visible behind fighter feet.
            AddBox("NearTerrace", new Vector3(26f, 0.10f, 0.85f), new Vector3(0f, -0.02f, -1.55f), StoneColor);
            AddBox("NearCoping", new Vector3(26f, 0.08f, 0.18f), new Vector3(0f, 0.04f, -2.00f), StoneColor.Lightened(0.04f));

            // Recessed warm LED under the near ledge
            AddLedStrip("NearLedStrip", new Vector3(22f, 0.03f, 0.04f), new Vector3(0f, 0.01f, -1.95f));

            // Canal water — raised slightly and deepened in Z so it reads from the 36° camera.
            var waterMat = new StandardMaterial3D
            {
                AlbedoColor = WaterColor * (1f / ProceduralTextures.NightWaterMean),
                AlbedoTexture = ProceduralTextures.NightWaterAlbedo(),
                Roughness = 0.14f,
                Metallic = 0.15f,
                NormalEnabled = true,
                NormalTexture = ProceduralTextures.NightWaterNormal(),
                NormalScale = 0.55f,
                EmissionEnabled = true,
                Emission = new Color("3a6080"),
                EmissionEnergyMultiplier = 0.28f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            AddChild(new MeshInstance3D
            {
                Name = "CheonggyecheonWater",
                Mesh = new PlaneMesh { Size = new Vector2(26f, 2.8f) },
                MaterialOverride = waterMat,
                Position = new Vector3(0f, -0.10f, -3.45f)
            });

            // Stepping stones across the channel
            float[] stoneXs = { -4.2f, -1.4f, 1.3f, 4.0f };
            for (int i = 0; i < stoneXs.Length; i++)
            {
                AddBox($"SteppingStone_{i}", new Vector3(0.55f, 0.08f, 0.42f),
                    new Vector3(stoneXs[i], -0.05f, -3.35f - (i % 2) * 0.25f), StoneColor.Lightened(0.06f));
            }

            // Far bank terrace steps
            AddBox("FarCoping", new Vector3(26f, 0.10f, 0.22f), new Vector3(0f, 0.02f, -4.85f), StoneColor);
            AddLedStrip("FarLedStrip", new Vector3(22f, 0.03f, 0.04f), new Vector3(0f, 0.00f, -4.78f));
            AddBox("FarBankLower", new Vector3(28f, 0.35f, 1.1f), new Vector3(0f, 0.12f, -5.55f), StoneColor * 0.95f);
            AddBox("FarBankUpper", new Vector3(28f, 0.45f, 1.4f), new Vector3(0f, 0.42f, -6.35f), StoneColor * 0.92f);
        }

        private void BuildBridgeAndBollards()
        {
            // Spanned girder deck off to the side so piers never stack on fighter silhouettes.
            const float bridgeX = -6.8f;
            AddBox("BridgeDeck", new Vector3(4.4f, 0.12f, 3.0f), new Vector3(bridgeX, 1.22f, -3.5f), new Color("3a424c"));
            AddBox("BridgeGirderL", new Vector3(4.4f, 0.22f, 0.08f), new Vector3(bridgeX, 1.10f, -2.15f), new Color("2e363e"));
            AddBox("BridgeGirderR", new Vector3(4.4f, 0.22f, 0.08f), new Vector3(bridgeX, 1.10f, -4.85f), new Color("2e363e"));

            for (int i = 0; i < 7; i++)
            {
                float x = bridgeX - 1.8f + i * 0.60f;
                AddBox($"BridgeBaluster_{i}A", new Vector3(0.06f, 0.42f, 0.06f), new Vector3(x, 1.48f, -2.15f), new Color("4a5460"));
                AddBox($"BridgeBaluster_{i}B", new Vector3(0.06f, 0.42f, 0.06f), new Vector3(x, 1.48f, -4.85f), new Color("4a5460"));
            }

            AddBox("BridgeRailTopL", new Vector3(4.4f, 0.05f, 0.08f), new Vector3(bridgeX, 1.70f, -2.15f), new Color("5a6470"));
            AddBox("BridgeRailTopR", new Vector3(4.4f, 0.05f, 0.08f), new Vector3(bridgeX, 1.70f, -4.85f), new Color("5a6470"));
            AddBox("BridgePierL", new Vector3(0.28f, 1.25f, 0.36f), new Vector3(bridgeX - 1.5f, 0.45f, -3.5f), new Color("323840"));
            AddBox("BridgePierR", new Vector3(0.28f, 1.25f, 0.36f), new Vector3(bridgeX + 1.5f, 0.45f, -3.5f), new Color("323840"));

            // Warm walkway bollards along the far bank
            float[] bollardXs = { -10f, -7f, -4f, -1f, 2f, 5f, 8f, 11f };
            for (int i = 0; i < bollardXs.Length; i++)
            {
                AddBox($"Bollard_{i}", new Vector3(0.12f, 0.55f, 0.12f), new Vector3(bollardXs[i], 0.85f, -6.05f), new Color("2e343c"));
                AddOmni("BollardLamp_" + i, new Vector3(bollardXs[i], 1.15f, -6.05f), WarmLed, 0.45f, 3.2f);
            }
        }

        private void BuildBuildingRow()
        {
            float[] xs = { -10.2f, -5.6f, -1.0f, 3.6f, 8.4f };
            float[] widths = { 4.0f, 3.6f, 4.2f, 3.4f, 3.8f };
            float[] heights = { 5.0f, 6.4f, 5.6f, 7.0f, 5.8f };
            float[] depths = { 2.6f, 2.4f, 2.8f, 2.2f, 2.5f };

            for (int i = 0; i < xs.Length; i++)
            {
                float h = heights[i];
                float z = -8.0f - (i % 2) * 0.35f;
                float w = widths[i];

                var facadeMat = new StandardMaterial3D
                {
                    AlbedoColor = BuildingColor.Darkened(0.03f * (i % 3)) * (1f / ProceduralTextures.NightStoneMean),
                    AlbedoTexture = ProceduralTextures.NightFacadeAlbedo(),
                    Roughness = 0.90f,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
                };
                AddChild(new MeshInstance3D
                {
                    Name = $"Building_{i}",
                    Mesh = new BoxMesh { Size = new Vector3(w, h, depths[i]) },
                    MaterialOverride = facadeMat,
                    Position = new Vector3(xs[i], h * 0.5f - 0.05f, z)
                });

                // Recessed window grid with dark mullions
                int cols = 3 + (i % 2);
                int rows = 4 + (i % 3);
                float gridW = w * 0.78f;
                float gridH = h * 0.62f;
                float cellW = gridW / cols;
                float cellH = gridH / rows;
                float gridOriginX = xs[i] - gridW * 0.5f + cellW * 0.5f;
                float gridOriginY = 0.55f + cellH * 0.5f;
                float faceZ = z + depths[i] * 0.5f + 0.03f;

                // Dark mullion plate behind the lit cells
                AddBox($"MullionPlate_{i}", new Vector3(gridW + 0.08f, gridH + 0.08f, 0.04f),
                    new Vector3(xs[i], gridOriginY + (rows - 1) * cellH * 0.5f, faceZ - 0.02f), new Color("1a2028"));

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        bool lit = ((r * 3 + c + i) % 5) != 0;
                        Color winCol = lit
                            ? (((r + c + i) % 2 == 0) ? new Color("c8b48a") : new Color("7a9ab8"))
                            : new Color("121820");
                        float energy = lit ? 0.38f + ((r + c) % 3) * 0.08f : 0.04f;
                        var winMat = new StandardMaterial3D
                        {
                            AlbedoColor = winCol.Darkened(0.35f),
                            EmissionEnabled = true,
                            Emission = winCol,
                            EmissionEnergyMultiplier = energy,
                            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                        };
                        AddChild(new MeshInstance3D
                        {
                            Name = $"Win_{i}_{r}_{c}",
                            Mesh = new BoxMesh { Size = new Vector3(cellW * 0.72f, cellH * 0.70f, 0.03f) },
                            MaterialOverride = winMat,
                            Position = new Vector3(gridOriginX + c * cellW, gridOriginY + r * cellH, faceZ)
                        });
                    }
                }

                // Rooftop mechanical penthouse
                AddBox($"Penthouse_{i}", new Vector3(w * 0.35f, 0.45f, depths[i] * 0.40f),
                    new Vector3(xs[i] + w * 0.15f, h + 0.15f, z - 0.1f), new Color("2a3038"));
            }
        }

        private void BuildNSeoulTower()
        {
            var shaftMat = MakeUnshaded(new Color("2a323e"));
            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerShaft",
                Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.16f, Height = 5.2f, RadialSegments = 8 },
                MaterialOverride = shaftMat,
                Position = new Vector3(6.2f, 5.0f, -12.2f)
            });

            var cabinMat = new StandardMaterial3D
            {
                AlbedoColor = new Color("343c48"),
                EmissionEnabled = true,
                Emission = new Color("c8a878"),
                EmissionEnergyMultiplier = 0.55f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerCabin",
                Mesh = new BoxMesh { Size = new Vector3(0.62f, 0.38f, 0.62f) },
                MaterialOverride = cabinMat,
                Position = new Vector3(6.2f, 7.2f, -12.2f)
            });

            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerSpire",
                Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.05f, Height = 1.6f, RadialSegments = 6 },
                MaterialOverride = shaftMat,
                Position = new Vector3(6.2f, 8.15f, -12.2f)
            });

            // Aviation beacon
            var beaconMat = new StandardMaterial3D
            {
                AlbedoColor = new Color("ff6060"),
                EmissionEnabled = true,
                Emission = new Color("ff4040"),
                EmissionEnergyMultiplier = 1.4f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            AddChild(new MeshInstance3D
            {
                Name = "NSeoulTowerBeacon",
                Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f },
                MaterialOverride = beaconMat,
                Position = new Vector3(6.2f, 8.95f, -12.2f)
            });
        }

        private void BuildFramedNeonSigns()
        {
            // Narrow vertical signboards mounted to building corners — above head height, inside FOV.
            PlaceVerticalSign("NeonMagenta", NeonMagenta, new Vector3(-5.4f, 2.65f, -6.55f), new Vector3(0.28f, 1.6f, 0.08f));
            PlaceVerticalSign("NeonCyan", NeonCyan, new Vector3(0.15f, 2.45f, -6.40f), new Vector3(0.26f, 1.35f, 0.08f));
            PlaceVerticalSign("NeonAmber", NeonAmber, new Vector3(4.0f, 2.80f, -6.60f), new Vector3(0.24f, 1.5f, 0.08f));
        }

        private void PlaceVerticalSign(string name, Color neon, Vector3 position, Vector3 size)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = neon,
                EmissionEnabled = true,
                Emission = neon,
                EmissionEnergyMultiplier = 1.35f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
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

            AddBox(name + "Frame", new Vector3(size.X + 0.10f, size.Y + 0.10f, size.Z + 0.05f),
                position + new Vector3(0f, 0f, -0.06f), new Color("14181e"));
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
                Mesh = new PlaneMesh { Size = new Vector2(40f, 18f) },
                MaterialOverride = _skyMat,
                Position = new Vector3(0f, 7.0f, -14.5f),
                RotationDegrees = new Vector3(90f, 0f, 0f)
            });
        }

        private void AddLedStrip(string name, Vector3 size, Vector3 position)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = WarmLed,
                EmissionEnabled = true,
                Emission = WarmLed,
                EmissionEnergyMultiplier = 1.6f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            AddChild(new MeshInstance3D
            {
                Name = name,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = mat,
                Position = position
            });
            AddOmni(name + "Omni", position + new Vector3(0f, 0.08f, 0.15f), WarmLed, 0.65f, 5.5f);
        }

        private void AddOmni(string name, Vector3 position, Color color, float energy, float range)
        {
            var light = new OmniLight3D
            {
                Name = name,
                Position = position,
                LightColor = color,
                LightEnergy = energy,
                OmniRange = range,
                ShadowEnabled = false
            };
            light.SetMeta("base_energy", energy);
            _warmLights.Add(light);
            AddChild(light);
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
