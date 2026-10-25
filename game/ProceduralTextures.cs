using System;
using System.Collections.Generic;
using Godot;

namespace FirstFighter.Game
{
    /// <summary>
    /// Surface families used by the procedural character builder. Each kind maps to a
    /// generated detail texture set so a body part stops reading as painted plastic.
    /// </summary>
    public enum SurfaceKind
    {
        None,
        Fabric,
        Trouser,
        Skin,
        Hair,
        Leather,
        Rubber
    }

    /// <summary>
    /// Deterministic, code-generated surface textures. No external image assets: every
    /// texture is synthesised from seeded noise at startup, cached, and reused.
    ///
    /// Stage textures are authored 1:1 against the mesh extents (no tiling), so the baked
    /// metre grid can never drift out of alignment or z-fight with geometry.
    /// Character textures are high-frequency and tile with mirror repeat, which has no
    /// visible seam at this grain size.
    ///
    /// Presentation-only: nothing here touches the deterministic simulation.
    /// </summary>
    public static class ProceduralTextures
    {
        // Stage floor is a 24m x 16m plane; wall is 28m x 12m. Texture pixels per metre
        // follow from these extents so UV 0..1 maps exactly onto the surface.
        private const int FloorPixelsW = 1024;
        private const int FloorPixelsH = 683;
        private const int WallPixelsW = 1024;
        private const int WallPixelsH = 439;
        private const int DetailSize = 256;

        /// <summary>
        /// Mean value of every albedo multiplier texture. Detail textures are sampled as sRGB and
        /// multiplied into albedo, so they are generated with a mean below 1.0 and headroom above
        /// it; materials divide their AlbedoColor by this constant to put the authored palette value
        /// back exactly. Without that pairing, clipped highlights in the texture silently darken
        /// every garment.
        /// </summary>
        public const float DetailMean = 0.96f;

        /// <summary>
        /// Stage surfaces carry deeper variation than garment detail, so they use their own mean and
        /// a wider headroom. Their materials divide AlbedoColor by the matching constant.
        /// </summary>
        public const float FloorMean = 0.74f;

        public const float WallMean = 0.86f;

        private static readonly Dictionary<string, ImageTexture> Cache = new();

        // ------------------------------------------------------------------
        // Stage
        // ------------------------------------------------------------------

        /// <summary>
        /// Matte studio concrete: broad tonal mottle, fine aggregate grain, faint stains,
        /// and a 1m printed reference grid baked into the albedo so it filters and mips
        /// like real floor marking instead of aliasing as thin geometry.
        /// </summary>
        public static ImageTexture StageFloorAlbedo() => Cached("floor_albedo", BuildFloorAlbedo);

        /// <summary>Tangent-space normal detail for the concrete aggregate.</summary>
        public static ImageTexture StageFloorNormal() => Cached("floor_normal", BuildFloorNormal);

        /// <summary>Roughness variation so the floor is not a uniform matte plate.</summary>
        public static ImageTexture StageFloorRoughness() => Cached("floor_rough", BuildFloorRoughness);

        /// <summary>Plaster grain plus the baked grounding fade for the cyclorama wall.</summary>
        public static ImageTexture StageWallAlbedo() => Cached("wall_albedo", BuildWallAlbedo);

        // ------------------------------------------------------------------
        // Character
        // ------------------------------------------------------------------

        public static ImageTexture DetailAlbedo(SurfaceKind kind) => kind switch
        {
            SurfaceKind.Fabric => Cached("fabric_albedo", () => BuildClothAlbedo(4011, weaveStrength: 0.008f, slubStrength: 0.008f)),
            SurfaceKind.Trouser => Cached("trouser_albedo", () => BuildClothAlbedo(4027, weaveStrength: 0.007f, slubStrength: 0.010f)),
            SurfaceKind.Skin => Cached("skin_albedo", BuildSkinAlbedo),
            SurfaceKind.Hair => Cached("hair_albedo", BuildHairAlbedo),
            SurfaceKind.Leather => Cached("leather_albedo", BuildLeatherAlbedo),
            SurfaceKind.Rubber => Cached("rubber_albedo", BuildRubberAlbedo),
            _ => null!
        };

        public static ImageTexture DetailNormal(SurfaceKind kind) => kind switch
        {
            SurfaceKind.Fabric => Cached("fabric_normal", () => BuildClothNormal(4011, strength: 5.0f)),
            SurfaceKind.Trouser => Cached("trouser_normal", () => BuildClothNormal(4027, strength: 4.5f)),
            SurfaceKind.Skin => Cached("skin_normal", () => BuildSkinNormal()),
            SurfaceKind.Hair => Cached("hair_normal", BuildHairNormal),
            SurfaceKind.Leather => Cached("leather_normal", BuildLeatherNormal),
            SurfaceKind.Rubber => Cached("rubber_normal", BuildRubberNormal),
            _ => null!
        };

        /// <summary>Normal-map intensity per surface family; skin barely perturbs.</summary>
        public static float DetailNormalScale(SurfaceKind kind) => kind switch
        {
            SurfaceKind.Fabric => 0.55f,
            SurfaceKind.Trouser => 0.40f,
            SurfaceKind.Skin => 0.28f,
            SurfaceKind.Hair => 0.55f,
            SurfaceKind.Leather => 0.70f,
            SurfaceKind.Rubber => 0.80f,
            _ => 0f
        };

        /// <summary>Texture repeats per metre for character surfaces (metres-based UVs).</summary>
        public static float DetailRepeat(SurfaceKind kind) => kind switch
        {
            // Repeats per metre. Kept low on purpose: a real cotton weave is sub-pixel at gameplay
            // distance and mipmaps flatten it to nothing, so these are authored to read as slub and
            // grain at roughly 1-3cm features instead of true thread spacing.
            SurfaceKind.Fabric => 2.4f,
            SurfaceKind.Trouser => 3.0f,
            SurfaceKind.Skin => 5.0f,
            SurfaceKind.Hair => 3.5f,
            SurfaceKind.Leather => 6.0f,
            SurfaceKind.Rubber => 8.0f,
            _ => 0f
        };

        // ------------------------------------------------------------------
        // Builders
        // ------------------------------------------------------------------

        private static Image BuildFloorAlbedo()
        {
            float ppmX = FloorPixelsW / 24f;
            float ppmY = FloorPixelsH / 16f;

            float[] broad = Fbm(FloorPixelsW, FloorPixelsH, 1103, 0.0042f, 4, 0.55f);
            float[] medium = Fbm(FloorPixelsW, FloorPixelsH, 1201, 0.018f, 3, 0.50f);
            float[] grit = Fbm(FloorPixelsW, FloorPixelsH, 1307, 0.55f, 3, 0.45f);
            float[] stains = Fbm(FloorPixelsW, FloorPixelsH, 1409, 0.0016f, 3, 0.60f);
            float[] speckle = Fbm(FloorPixelsW, FloorPixelsH, 1511, 0.85f, 1, 0.50f);

            var values = new float[FloorPixelsW * FloorPixelsH];

            for (int y = 0; y < FloorPixelsH; y++)
            {
                for (int x = 0; x < FloorPixelsW; x++)
                {
                    int i = y * FloorPixelsW + x;

                    // Base matte concrete with layered tonal drift.
                    float v = 1.0f
                              + (broad[i] - 0.5f) * 0.120f
                              + (medium[i] - 0.5f) * 0.075f
                              + (grit[i] - 0.5f) * 0.030f;

                    // Wide soft staining, only where the noise crosses a threshold.
                    float stain = Mathf.Clamp((stains[i] - 0.545f) * 2.6f, 0f, 1f);
                    v -= stain * 0.070f;

                    // Sparse aggregate pits.
                    if (speckle[i] > 0.900f)
                    {
                        v -= (speckle[i] - 0.900f) * 1.8f;
                    }

                    // 1m printed reference grid, with a stronger line every 4m.
                    v *= GridShade(x / ppmX, y / ppmY, 1f, 4f, 0.800f, 0.700f);

                    values[i] = v;
                }
            }

            // Mean 0.90 leaves 10% of headroom, so the grid and stains survive at full strength
            // without any channel clipping; the floor material divides its AlbedoColor by the same
            // constant to keep the authored #8e8178 exactly.
            return WriteDetailField(values, FloorPixelsW, FloorPixelsH, FloorMean);
        }

        private static Image BuildFloorNormal()
        {
            float[] height = Fbm(FloorPixelsW, FloorPixelsH, 2203, 0.16f, 3, 0.48f);
            return HeightToNormal(FloorPixelsW, FloorPixelsH, height, strength: 3.2f, wrap: false);
        }

        private static Image BuildFloorRoughness()
        {
            float ppmX = FloorPixelsW / 24f;
            float ppmY = FloorPixelsH / 16f;

            float[] grain = Fbm(FloorPixelsW, FloorPixelsH, 2309, 0.09f, 3, 0.50f);
            var data = new byte[FloorPixelsW * FloorPixelsH * 3];

            for (int y = 0; y < FloorPixelsH; y++)
            {
                for (int x = 0; x < FloorPixelsW; x++)
                {
                    int i = y * FloorPixelsW + x;

                    // Mostly rough matte floor; slightly polished where feet polish it.
                    float r = 0.90f + (grain[i] - 0.5f) * 0.10f;

                    // Printed marking is a touch glossier than the concrete around it.
                    float grid = GridShade(x / ppmX, y / ppmY, 1f, 4f, 0.800f, 0.700f);
                    if (grid < 0.99f)
                    {
                        r -= (1f - grid) * 0.55f;
                    }

                    byte b = ToByte(r);
                    data[i * 3] = b;
                    data[i * 3 + 1] = b;
                    data[i * 3 + 2] = b;
                }
            }

            return Finish(Image.CreateFromData(FloorPixelsW, FloorPixelsH, false, Image.Format.Rgb8, data));
        }

        private static Image BuildWallAlbedo()
        {
            float[] broad = Fbm(WallPixelsW, WallPixelsH, 3109, 0.010f, 4, 0.55f);
            float[] fine = Fbm(WallPixelsW, WallPixelsH, 3203, 0.40f, 2, 0.45f);
            var values = new float[WallPixelsW * WallPixelsH];

            for (int y = 0; y < WallPixelsH; y++)
            {
                // PlaneMesh runs v = 0 at the top edge after the wall's +90 deg X rotation, so the
                // grounding fade has to darken toward v = 1 (the floor line).
                float v = y / (float)(WallPixelsH - 1);
                float grounding = 1f - 0.42f * Mathf.Pow(1f - v, 2.5f);
                float vignette = 1f - 0.05f * Mathf.Pow(Mathf.Abs(v - 0.5f) * 2f, 4f);

                for (int x = 0; x < WallPixelsW; x++)
                {
                    int i = y * WallPixelsW + x;

                    // Plaster, not stone: very low contrast at two scales.
                    float grain = (broad[i] - 0.5f) * 0.55f + (fine[i] - 0.5f) * 0.25f;
                    values[i] = (1f + grain) * grounding * vignette;
                }
            }

            return WriteDetailField(values, WallPixelsW, WallPixelsH, WallMean);
        }

        private static Image BuildClothAlbedo(int seed, float weaveStrength, float slubStrength)
        {
            float[] weave = ClothHeightField(DetailSize, seed, period: 26f, diagonal: 0.35f);
            float[] slub = Fbm(DetailSize, DetailSize, seed + 77, 0.030f, 2, 0.55f);
            var values = new float[DetailSize * DetailSize];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1f + (weave[i] - 0.5f) * 2f * weaveStrength + (slub[i] - 0.5f) * 2f * slubStrength;
            }

            return WriteDetailField(values, DetailSize, DetailSize);
        }

        private static Image BuildClothNormal(int seed, float strength)
        {
            float[] height = ClothHeightField(DetailSize, seed, period: 26f, diagonal: 0.35f);
            return HeightToNormal(DetailSize, DetailSize, height, strength, wrap: true);
        }

        private static Image BuildSkinAlbedo()
        {
            float[] mottle = Fbm(DetailSize, DetailSize, 5501, 0.035f, 3, 0.50f);
            float[] pore = Fbm(DetailSize, DetailSize, 5609, 0.55f, 2, 0.45f);
            var values = new float[DetailSize * DetailSize];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1f + (mottle[i] - 0.5f) * 0.055f + (pore[i] - 0.5f) * 0.040f;
            }

            return WriteDetailField(values, DetailSize, DetailSize);
        }

        private static Image BuildSkinNormal()
        {
            float[] height = Fbm(DetailSize, DetailSize, 5701, 0.40f, 2, 0.45f);
            return HeightToNormal(DetailSize, DetailSize, height, strength: 0.5f, wrap: true);
        }

        private static Image BuildHairAlbedo()
        {
            // Anisotropic streaks: noise stretched along the strand direction.
            float[] strands = FbmAnisotropic(DetailSize, DetailSize, 6101, freq: 0.10f, octaves: 3, stretchY: 7f);
            var values = new float[DetailSize * DetailSize];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1f + (strands[i] - 0.5f) * 0.190f;
            }

            return WriteDetailField(values, DetailSize, DetailSize);
        }

        private static Image BuildHairNormal()
        {
            float[] strands = FbmAnisotropic(DetailSize, DetailSize, 6101, freq: 0.10f, octaves: 3, stretchY: 7f);
            return HeightToNormal(DetailSize, DetailSize, strands, strength: 3.0f, wrap: true);
        }

        private static Image BuildLeatherAlbedo()
        {
            float[] pebble = Fbm(DetailSize, DetailSize, 7103, 0.30f, 3, 0.50f);
            var values = new float[DetailSize * DetailSize];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1f + (pebble[i] - 0.5f) * 0.135f;
            }

            return WriteDetailField(values, DetailSize, DetailSize);
        }

        private static Image BuildLeatherNormal()
        {
            float[] pebble = Fbm(DetailSize, DetailSize, 7103, 0.30f, 3, 0.50f);
            return HeightToNormal(DetailSize, DetailSize, pebble, strength: 4.0f, wrap: true);
        }

        private static Image BuildRubberAlbedo()
        {
            float[] grain = Fbm(DetailSize, DetailSize, 8101, 0.45f, 2, 0.50f);
            var values = new float[DetailSize * DetailSize];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1f + (grain[i] - 0.5f) * 0.095f;
            }

            return WriteDetailField(values, DetailSize, DetailSize);
        }

        private static Image BuildRubberNormal()
        {
            float[] grain = Fbm(DetailSize, DetailSize, 8101, 0.45f, 2, 0.50f);
            return HeightToNormal(DetailSize, DetailSize, grain, strength: 1.4f, wrap: true);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static ImageTexture Cached(string key, Func<Image> build)
        {
            if (Cache.TryGetValue(key, out ImageTexture? existing) && GodotObject.IsInstanceValid(existing))
            {
                return existing;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(build());
            Cache[key] = texture;
            return texture;
        }

        /// <summary>Mipmaps stop the grid and grain from shimmering at distance.</summary>
        private static Image Finish(Image image)
        {
            image.GenerateMipmaps();
            return image;
        }

        /// <summary>
        /// Encode a detail field as an albedo multiplier with an exact mean of DetailMean. Deviation
        /// is scaled so the brightest sample lands exactly on 1.0, which means nothing is ever
        /// clamped and the mean stays true after sRGB encoding.
        /// </summary>
        private static Image WriteDetailField(float[] values, int width, int height, float mean = DetailMean)
        {
            double sum = 0.0;
            float maxDeviation = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }

            float fieldMean = (float)(sum / values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                float d = Mathf.Abs(values[i] - fieldMean);
                if (d > maxDeviation)
                {
                    maxDeviation = d;
                }
            }

            float headroom = 1f - mean;
            float scale = maxDeviation > 1e-6f ? Mathf.Min(1f, headroom / maxDeviation) : 1f;

            var data = new byte[width * height * 3];
            for (int i = 0; i < values.Length; i++)
            {
                byte b = ToByte(mean + (values[i] - fieldMean) * scale);
                data[i * 3] = b;
                data[i * 3 + 1] = b;
                data[i * 3 + 2] = b;
            }

            return Finish(Image.CreateFromData(width, height, false, Image.Format.Rgb8, data));
        }

        private static byte ToByte(float linear)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(linear * 255f), 0, 255);
        }

        /// <summary>
        /// Darkness multiplier for a printed floor grid line passing through
        /// (metresX, metresY). Lines are soft-edged so they filter down cleanly.
        /// </summary>
        private static float GridShade(
            float metresX,
            float metresY,
            float spacing,
            float majorSpacing,
            float minorShade,
            float majorShade)
        {
            float dx = DistanceToLine(metresX, spacing);
            float dy = DistanceToLine(metresY, spacing);
            float d = Mathf.Min(dx, dy);

            // ~6cm printed line with a 2cm feathered edge.
            float line = 1f - Mathf.Clamp((d - 0.030f) / 0.020f, 0f, 1f);
            if (line <= 0f)
            {
                return 1f;
            }

            bool major = DistanceToLine(metresX, majorSpacing) < 0.030f ||
                         DistanceToLine(metresY, majorSpacing) < 0.030f;

            float shade = Mathf.Lerp(minorShade, majorShade, major ? 1f : 0f);
            return Mathf.Lerp(1f, shade, line);
        }

        private static float DistanceToLine(float metres, float spacing)
        {
            float m = metres / spacing;
            return Mathf.Abs(m - Mathf.Round(m)) * spacing;
        }

        /// <summary>
        /// Cloth relief field: a single coherent diagonal rib (twill) whose amplitude is modulated
        /// slowly, so it reads as directional fabric grain instead of isotropic speckle. An earlier
        /// crossed warp/weft version generated a diagonal checkerboard of threads, which at this
        /// camera distance rendered as random high-contrast mottling rather than cloth.
        /// </summary>
        private static float[] ClothHeightField(int size, int seed, float period, float diagonal)
        {
            float[] amplitude = Fbm(size, size, seed + 211, 0.012f, 2, 0.55f);
            var f = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;

                    float u = (x + y * diagonal) / period;
                    float rib = Mathf.Sin(u * Mathf.Tau);
                    // Second harmonic rounds the thread profile so it does not read as a hard sine.
                    rib = rib * 0.78f + Mathf.Sin(u * Mathf.Tau * 0.5f) * 0.22f;

                    float amp = 0.62f + 0.38f * amplitude[i];
                    f[i] = 0.5f + 0.5f * rib * amp;
                }
            }

            return f;
        }

        private static float[] Fbm(int w, int h, int seed, float freq, int octaves, float gain)
        {
            var noise = new FastNoiseLite
            {
                Seed = seed,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                Frequency = freq,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = octaves,
                FractalGain = gain,
                FractalLacunarity = 2f
            };

            var f = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    f[y * w + x] = Mathf.Clamp(noise.GetNoise2D(x, y) * 0.5f + 0.5f, 0f, 1f);
                }
            }

            return f;
        }

        private static float[] FbmAnisotropic(int w, int h, int seed, float freq, int octaves, float stretchY)
        {
            var noise = new FastNoiseLite
            {
                Seed = seed,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                Frequency = freq,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = octaves,
                FractalGain = 0.5f,
                FractalLacunarity = 2f
            };

            var f = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    f[y * w + x] = Mathf.Clamp(noise.GetNoise2D(x, y / stretchY) * 0.5f + 0.5f, 0f, 1f);
                }
            }

            return f;
        }

        /// <summary>
        /// Sobel a height field into a tangent-space normal map (Godot, green-up).
        /// wrap=true mirrors samples so tiled character textures stay continuous.
        /// </summary>
        private static Image HeightToNormal(int w, int h, float[] height, float strength, bool wrap)
        {
            var data = new byte[w * h * 3];

            for (int y = 0; y < h; y++)
            {
                int yUp = y + 1;
                int yDn = y - 1;
                if (wrap)
                {
                    yUp = (y + 1) % h;
                    yDn = (y - 1 + h) % h;
                }
                else
                {
                    yUp = Mathf.Clamp(y + 1, 0, h - 1);
                    yDn = Mathf.Clamp(y - 1, 0, h - 1);
                }

                for (int x = 0; x < w; x++)
                {
                    int xR = wrap ? (x + 1) % w : Mathf.Clamp(x + 1, 0, w - 1);
                    int xL = wrap ? (x - 1 + w) % w : Mathf.Clamp(x - 1, 0, w - 1);

                    float hl = height[y * w + xL];
                    float hr = height[y * w + xR];
                    float hu = height[yUp * w + x];
                    float hd = height[yDn * w + x];

                    float nx = (hl - hr) * strength;
                    float ny = (hd - hu) * strength;
                    float nz = 1f;

                    float len = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len < 0.00001f)
                    {
                        len = 1f;
                    }

                    int i = (y * w + x) * 3;
                    data[i] = (byte)Mathf.Clamp(Mathf.RoundToInt((nx / len * 0.5f + 0.5f) * 255f), 0, 255);
                    data[i + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt((ny / len * 0.5f + 0.5f) * 255f), 0, 255);
                    data[i + 2] = (byte)Mathf.Clamp(Mathf.RoundToInt((nz / len * 0.5f + 0.5f) * 255f), 0, 255);
                }
            }

            return Finish(Image.CreateFromData(w, h, false, Image.Format.Rgb8, data));
        }
    }
}
