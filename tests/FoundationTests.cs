using System;
using System.IO;
using System.Reflection;
using Godot;
using FirstFighter.Sim;
using FirstFighter.Game;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FactAttribute : Attribute { }

    public static class Assert
    {
        public static void True(bool condition, string? message = null)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message ?? "Assert.True failed.");
            }
        }

        public static void False(bool condition, string? message = null)
        {
            if (condition)
            {
                throw new InvalidOperationException(message ?? "Assert.False failed.");
            }
        }

        public static void Equal<T>(T expected, T actual, string? message = null)
        {
            if (! Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message ?? $"Assert.Equal failed. Expected: <{expected}>, Actual: <{actual}>.");
            }
        }

        public static void NotEqual<T>(T notExpected, T actual, string? message = null)
        {
            if (Equals(notExpected, actual))
            {
                throw new InvalidOperationException(
                    message ?? $"Assert.NotEqual failed. Both were: <{actual}>.");
            }
        }
    }
}

namespace FirstFighter.Tests
{
    using Xunit;

    /// <summary>
    /// Headless verification suite for FIRST FIGHTER covering Andrea Jens Parts 1-8 and the 11-Hit Golden Combo.
    /// </summary>
    public sealed class FoundationTests
    {
        [Fact]
        public void Determinism_SnapshotAndRollbackHashesMatchAcross500Ticks()
        {
            var stateA = new MatchState(0xCAFEBABEu);
            var stateB = new MatchState(0xCAFEBABEu);

            byte[]? snapshotAt180 = null;
            ulong hashAt180 = 0;
            ulong hashAt360 = 0;

            for (int tick = 0; tick < 500; tick++)
            {
                FighterButtons p1 = GetSyntheticScriptInput(tick, 0);
                FighterButtons p2 = GetSyntheticScriptInput(tick, 1);

                MatchSim.Step(stateA, p1, p2);
                MatchSim.Step(stateB, p1, p2);

                Assert.Equal(stateA.ComputeStateHash(), stateB.ComputeStateHash(),
                    $"State hashes diverged at tick {tick + 1}.");

                if (tick == 180)
                {
                    snapshotAt180 = stateA.SaveBinarySnapshot();
                    hashAt180 = stateA.ComputeStateHash();
                }
                if (tick == 360)
                {
                    hashAt360 = stateA.ComputeStateHash();
                }
            }

            // Roll back stateB to tick 180 and re-simulate to tick 360: must match bit-for-bit!
            Assert.True(snapshotAt180 != null, "Snapshot at tick 180 should be captured.");
            stateB.LoadBinarySnapshot(snapshotAt180!);
            Assert.Equal(hashAt180, stateB.ComputeStateHash(), "Restored snapshot hash at tick 180 must match.");

            for (int tick = 181; tick <= 360; tick++)
            {
                FighterButtons p1 = GetSyntheticScriptInput(tick, 0);
                FighterButtons p2 = GetSyntheticScriptInput(tick, 1);
                MatchSim.Step(stateB, p1, p2);
            }

            Assert.Equal(hashAt360, stateB.ComputeStateHash(),
                "Rollback re-simulation from tick 180 to 360 must produce identical state hash.");
        }

        [Fact]
        public void Part4_And_8_StandingJabWhiffsOnCrouchingOpponent()
        {
            var state = new MatchState();
            // Place fighters at point-blank range
            state.Fighters[0].X = Fix.FromMilli(-210);
            state.Fighters[1].X = Fix.FromMilli(210);

            // Verify geometry specification: 5L hitbox Y is 840..1040 mu, Crouch hurtbox Y is 0..780 mu
            var move5L = FighterCatalog.FirstFighter.GetMove(MoveId.StandL);
            Assert.Equal(840, move5L.Hitbox.OffsetY);
            Assert.Equal(200, move5L.Hitbox.Height);
            Assert.Equal(780, FighterCatalog.FirstFighter.CrouchingHurtboxes[0].Height);

            // P2 holds Down (crouching), P1 inputs 5L (L)
            MatchSim.Step(state, FighterButtons.None, FighterButtons.Down);
            Assert.True(state.Fighters[1].IsCrouching, "P2 should be crouching.");

            int p2InitialHealth = state.Fighters[1].Health;
            MatchSim.Step(state, FighterButtons.L, FighterButtons.Down);
            Assert.Equal(MoveId.StandL, state.Fighters[0].CurrentMoveId);

            // Run through entire 5L duration (4 startup + 2 active + 7 recovery)
            for (int i = 0; i < 15; i++)
            {
                MatchSim.Step(state, FighterButtons.None, FighterButtons.Down);
            }

            Assert.Equal(p2InitialHealth, state.Fighters[1].Health,
                "Standing 5L (High, Y: 840..1040 mu) must whiff cleanly over crouching P2 (hurtbox Y: 0..780 mu).");
            Assert.Equal(0, state.Fighters[1].ComboHits, "Crouching P2 should not be hit by 5L.");
        }

        [Fact]
        public void Part8_LowSweepBeatsStandGuard_And_HoppingElbowOverheadBeatsCrouchGuard()
        {
            // Case A: 2M (Tailored Low Sweep, Low) beats Stand Guard (holding Back = Right for P2)
            var stateLow = new MatchState();
            stateLow.Fighters[0].X = Fix.FromMilli(-220);
            stateLow.Fighters[1].X = Fix.FromMilli(220);

            int initialHpA = stateLow.Fighters[1].Health;
            MatchSim.Step(stateLow, FighterButtons.Down | FighterButtons.M, FighterButtons.Right);
            Assert.Equal(MoveId.CrouchM, stateLow.Fighters[0].CurrentMoveId);

            for (int i = 0; i < 12; i++)
            {
                MatchSim.Step(stateLow, FighterButtons.Down, FighterButtons.Right);
            }

            Assert.True(stateLow.Fighters[1].Health < initialHpA,
                "2M (Low Sweep) must break through P2's standing guard.");
            Assert.Equal(1, stateLow.Fighters[1].ComboHits);

            // Case B: 6H (Hopping Downward Elbow, Overhead) crushes Crouch Guard (holding Down-Back = Down|Right for P2)
            var stateOver = new MatchState();
            stateOver.Fighters[0].X = Fix.FromMilli(-240);
            stateOver.Fighters[1].X = Fix.FromMilli(240);

            int initialHpB = stateOver.Fighters[1].Health;
            MatchSim.Step(stateOver, FighterButtons.Right | FighterButtons.H, FighterButtons.Down | FighterButtons.Right);
            Assert.Equal(MoveId.ForwardH, stateOver.Fighters[0].CurrentMoveId);

            for (int i = 0; i < 20; i++)
            {
                MatchSim.Step(stateOver, FighterButtons.None, FighterButtons.Down | FighterButtons.Right);
            }

            Assert.True(stateOver.Fighters[1].Health < initialHpB,
                "6H (Hopping Downward Elbow Overhead) must crush P2's crouching guard.");
            Assert.Equal(1, stateOver.Fighters[1].ComboHits);

            // Case C: Verify Sifu Forearm Deflect Parry (214S / 4S) parries Mid attack and auto-launches!
            var stateParry = new MatchState();
            stateParry.Fighters[0].X = Fix.FromMilli(-220);
            stateParry.Fighters[1].X = Fix.FromMilli(220);

            // P1 starts 5H (8f startup), P2 triggers 4+S Parry so ticks 2..10 overlap P1's active frame (t=8)
            MatchSim.Step(stateParry, FighterButtons.H, FighterButtons.None);
            for (int i = 0; i < 3; i++)
            {
                MatchSim.Step(stateParry, FighterButtons.None, FighterButtons.None);
            }
            // On P1 MoveTick=4, P2 presses Back+S (Right|S for P2 facing left)
            MatchSim.Step(stateParry, FighterButtons.None, FighterButtons.Right | FighterButtons.S);
            Assert.Equal(MoveId.ParryDeflect, stateParry.Fighters[1].CurrentMoveId);

            for (int i = 0; i < 25; i++)
            {
                MatchSim.Step(stateParry, FighterButtons.None, FighterButtons.None);
            }

            Assert.Equal(1000, stateParry.Fighters[1].Health, "P2 must take zero damage when parrying 5H.");
            Assert.True(stateParry.Fighters[1].Meter >= 35, "Successful parry must grant +35 meter.");
            Assert.True(stateParry.Fighters[0].Health < 1000, "Parry auto-counter must strike and launch P1.");
        }

        [Fact]
        public void Part6_PriorityParser_DpBeatsQcf_And_2TickKaraThrowWorks()
        {
            // Part A: Overlapping 6 -> 2 -> 3 -> 6 + H must resolve to 623H (DP, Priority 4) over 236H (QCF, Priority 3)
            var stateDp = new MatchState();
            MatchSim.Step(stateDp, FighterButtons.Right, FighterButtons.None);               // 6
            MatchSim.Step(stateDp, FighterButtons.Down, FighterButtons.None);                // 2
            MatchSim.Step(stateDp, FighterButtons.Down | FighterButtons.Right, FighterButtons.None); // 3
            MatchSim.Step(stateDp, FighterButtons.Right | FighterButtons.H, FighterButtons.None);    // 6 + H

            Assert.Equal(MoveId.DpUpper, stateDp.Fighters[0].CurrentMoveId,
                "6 -> 2 -> 3 -> 6 + H must select 623H (Dragon Rising Uppercut, Priority 4) over 236H (Priority 3).");

            // Part B: 2-Tick Kara-Cancel Throw (press L on tick T, then M on tick T+1 -> cancels 5L startup into Throw!)
            var stateKara = new MatchState();
            stateKara.Fighters[0].X = Fix.FromMilli(-210);
            stateKara.Fighters[1].X = Fix.FromMilli(210);

            // Tick T: press L -> starts 5L (StandL)
            MatchSim.Step(stateKara, FighterButtons.L, FighterButtons.None);
            Assert.Equal(MoveId.StandL, stateKara.Fighters[0].CurrentMoveId);

            // Tick T+1 (1 tick later, within 2-tick kara window): press L + M -> kara-cancels into Throw!
            MatchSim.Step(stateKara, FighterButtons.L | FighterButtons.M, FighterButtons.None);
            Assert.Equal(MoveId.Throw, stateKara.Fighters[0].CurrentMoveId,
                "Pressing M 1 tick after L must kara-cancel 5L startup into Clinch Takedown Throw (L+M).");
        }

        [Fact]
        public void Part7_PushbackScalingStopsInfiniteJabAfter3Hits()
        {
            var state = new MatchState();
            // Point-blank start
            state.Fighters[0].X = Fix.FromMilli(-210);
            state.Fighters[1].X = Fix.FromMilli(210);

            int maxComboReached = 0;

            // Attempt 4 consecutive 5L jabs in a row
            for (int jabAttempt = 0; jabAttempt < 4; jabAttempt++)
            {
                // Press L to trigger 5L
                MatchSim.Step(state, FighterButtons.L, FighterButtons.None);
                // Step through startup + active + hitstop until 5L can cancel or recover
                for (int t = 0; t < 11; t++)
                {
                    MatchSim.Step(state, FighterButtons.None, FighterButtons.None);
                    if (state.Fighters[1].ComboHits > maxComboReached)
                    {
                        maxComboReached = state.Fighters[1].ComboHits;
                    }
                }
            }

            // Let remaining frames settle
            for (int t = 0; t < 20; t++)
            {
                MatchSim.Step(state, FighterButtons.None, FighterButtons.None);
            }

            Assert.Equal(3, maxComboReached,
                "Combo pushback scaling (+15% per hit) must allow 3 hits of 5L -> 5L -> 5L and push out of range before Hit 4.");
        }

        [Fact]
        public void GoldenCombo_11Hit_GroundToAirToGroundBounceToDpToSuper_ConnectsDeterministically()
        {
            var state = new MatchState();
            var p1 = state.Fighters[0];
            var p2 = state.Fighters[1];

            // Start in close neutral with 100 Super Meter ready for the finisher
            p1.X = Fix.FromMilli(-210);
            p2.X = Fix.FromMilli(210);
            p1.Meter = 100;

            // Helper to step until P1's current attack connects (and hitstop finishes)
            static void StepUntilMoveConnectsAndHitstopEnds(MatchState s, MoveId expectedMove, int maxTicks = 45)
            {
                var f = s.Fighters[0];
                bool sawConnect = false;
                for (int i = 0; i < maxTicks; i++)
                {
                    MatchSim.Step(s, FighterButtons.None, FighterButtons.None);
                    if (f.CurrentMoveId == expectedMove && f.MoveConnected)
                    {
                        sawConnect = true;
                        if (f.Hitstop == 0)
                        {
                            return;
                        }
                    }
                }
                Assert.True(sawConnect, $"Expected move {expectedMove} to connect within {maxTicks} ticks.");
            }

            // 1. 5L (Snap Lead Jab)
            MatchSim.Step(state, FighterButtons.L, FighterButtons.None);
            Assert.Equal(MoveId.StandL, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.StandL);
            Assert.Equal(1, p2.ComboHits);

            // 2. Cancel into 2M (Tailored Low Sweep)
            MatchSim.Step(state, FighterButtons.Down | FighterButtons.M, FighterButtons.None);
            Assert.Equal(MoveId.CrouchM, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.CrouchM);
            Assert.Equal(2, p2.ComboHits);

            // 3. Cancel into 5H (Step-In Rear Cross)
            MatchSim.Step(state, FighterButtons.H, FighterButtons.None);
            Assert.Equal(MoveId.StandH, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.StandH);
            Assert.Equal(3, p2.ComboHits);

            // 4. Cancel into 5S (Rising Knee & Sky Uppercut Launcher)
            MatchSim.Step(state, FighterButtons.S, FighterButtons.None);
            Assert.Equal(MoveId.StandS, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.StandS);
            Assert.Equal(4, p2.ComboHits);
            Assert.True(p2.IsAirborne, "5S must launch P2 airborne.");

            // 5. Jump-Cancel on hit (Up + Forward = 9)
            MatchSim.Step(state, FighterButtons.Up | FighterButtons.Right, FighterButtons.None);
            Assert.Equal(FighterStateKind.JumpStart, p1.State);
            // Complete 3-tick JumpStart + 2 ticks of ascent
            for (int i = 0; i < 5; i++)
            {
                MatchSim.Step(state, FighterButtons.Right, FighterButtons.None);
            }
            Assert.True(p1.IsAirborne, "P1 must be airborne after jump-canceling 5S.");

            // 6. j.L (Air Snap Backfist)
            MatchSim.Step(state, FighterButtons.L, FighterButtons.None);
            Assert.Equal(MoveId.AirL, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.AirL);
            Assert.Equal(5, p2.ComboHits);

            // 7. Chain into j.M (Air Roundhouse Kick)
            MatchSim.Step(state, FighterButtons.M, FighterButtons.None);
            Assert.Equal(MoveId.AirM, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.AirM);
            Assert.Equal(6, p2.ComboHits);

            // 8. Chain into j.H (Air Spinning Hook Kick)
            MatchSim.Step(state, FighterButtons.H, FighterButtons.None);
            Assert.Equal(MoveId.AirH, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.AirH);
            Assert.Equal(7, p2.ComboHits);

            // 9. Chain into j.S (Axe-Heel Meteor Drop -> Ground Bounce!)
            MatchSim.Step(state, FighterButtons.S, FighterButtons.None);
            Assert.Equal(MoveId.AirS, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.AirS);
            Assert.Equal(8, p2.ComboHits);
            Assert.True(p2.WillGroundBounce, "j.S must arm WillGroundBounce on airborne opponent.");

            // Wait for P2 to slam into the floor (ground bounce) and P1 to land
            for (int i = 0; i < 35; i++)
            {
                MatchSim.Step(state, FighterButtons.None, FighterButtons.None);
                if (p2.UsedGroundBounce && !p1.IsAirborne && p1.State == FighterStateKind.Idle)
                {
                    break;
                }
            }
            Assert.True(p2.UsedGroundBounce, "P2 must have triggered Ground Bounce upon hitting Y <= 0.");
            Assert.False(p1.IsAirborne, "P1 must have landed to follow up with 623H.");

            // 10. Land -> 623H (Dragon Rising Uppercut) pickup!
            MatchSim.Step(state, FighterButtons.Right, FighterButtons.None);                        // 6
            MatchSim.Step(state, FighterButtons.Down, FighterButtons.None);                         // 2
            MatchSim.Step(state, FighterButtons.Down | FighterButtons.Right | FighterButtons.H, FighterButtons.None); // 3 + H
            Assert.Equal(MoveId.DpUpper, p1.CurrentMoveId);
            StepUntilMoveConnectsAndHitstopEnds(state, MoveId.DpUpper);
            Assert.Equal(9, p2.ComboHits);

            // 11. Super-Cancel 623H into 236H+S (SUPER: FIVE-POINT STRUCTURE BREAK)!
            p1.Meter = 100; // Ensure full meter for Super Art
            MatchSim.Step(state, FighterButtons.Down, FighterButtons.None);                         // 2
            MatchSim.Step(state, FighterButtons.Down | FighterButtons.Right, FighterButtons.None);  // 3
            MatchSim.Step(state, FighterButtons.Right | FighterButtons.H | FighterButtons.S, FighterButtons.None); // 6 + H+S
            Assert.Equal(MoveId.SuperArt, p1.CurrentMoveId, "623H must Super-Cancel on hit into 236H+S.");

            // Run through the 6-hit Super Art cinema
            int peakComboHits = p2.ComboHits;
            for (int i = 0; i < 65; i++)
            {
                MatchSim.Step(state, FighterButtons.None, FighterButtons.None);
                if (p2.ComboHits > peakComboHits)
                {
                    peakComboHits = p2.ComboHits;
                }
            }

            Assert.True(peakComboHits >= 11,
                $"Golden Combo must reach at least 11 hits (actual peak hits: {peakComboHits}).");
            Assert.True(p2.UsedGroundBounce, "Golden Combo must have used Ground Bounce.");
        }

        [Fact]
        public void SimPurity_ZeroFloatingPointTokensAndZeroGodotReferencesInSimFolder()
        {
            string simDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sim"));
            if (!Directory.Exists(simDir))
            {
                simDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "sim"));
            }
            Assert.True(Directory.Exists(simDir), $"sim/ directory must exist at {simDir}");

            foreach (string file in Directory.GetFiles(simDir, "*.cs"))
            {
                string src = File.ReadAllText(file);
                Assert.False(src.Contains("Godot"), $"sim file {Path.GetFileName(file)} must not reference Godot.");
                Assert.False(src.Contains("float"), $"sim file {Path.GetFileName(file)} must not use non-deterministic single precision.");
                Assert.False(src.Contains("double"), $"sim file {Path.GetFileName(file)} must not use non-deterministic double precision.");
            }
        }

        [Fact]
        public void ModelAndPose_19BoneSkeletonAndGuardStanceAndNonLinearKeyPoseEngine()
        {
            // Verify 19-bone skeleton rest offsets & proportions (Hips 0.96m + Abdomen 0.14m + Torso 0.22m + Neck 0.21m + Head 0.09m = 1.62m head base)
            Assert.Equal(19, FirstFighterModelBuilder.BoneCount);
            float headBaseY =
                FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Hips].Y +
                FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Abdomen].Y +
                FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Torso].Y +
                FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Neck].Y +
                FirstFighterModelBuilder.RestOffsets[(int)FighterBone.Head].Y;
            Assert.True(Math.Abs(headBaseY - 1.62f) < 0.001f, $"Head base Y should be 1.62m (was {headBaseY}).");

            // Verify exact GUARD pose angles (Panel 4 of reference_first_fighter.jpg)
            var guard = FighterPoseEngine.GuardPose;
            Assert.Equal(38f, guard.BoneEulerDegrees[(int)FighterBone.Hips].Y);
            Assert.Equal(116f, guard.BoneEulerDegrees[(int)FighterBone.LowerArmL].X);
            Assert.Equal(126f, guard.BoneEulerDegrees[(int)FighterBone.LowerArmR].X);

            // Verify Non-Linear Key-Pose Curve: Active frame locks 1.22x axial stretch, Block vs Whiff branches recovery
            var active5L = FighterPoseEngine.EvalAttackPose(MoveId.StandL, moveTick: 4, inHitstop: false, moveConnected: false, moveBlocked: false);
            Assert.True(Math.Abs(active5L.BoneScale[(int)FighterBone.LowerArmL].Y - 1.22f) < 0.001f,
                "Active frame must apply 1.22f axial bone stretch to striking limb.");

            var recBlocked = FighterPoseEngine.EvalAttackPose(MoveId.StandL, moveTick: 7, inHitstop: false, moveConnected: false, moveBlocked: true);
            var recWhiffed = FighterPoseEngine.EvalAttackPose(MoveId.StandL, moveTick: 7, inHitstop: false, moveConnected: false, moveBlocked: false);
            Assert.True(recWhiffed.BoneEulerDegrees[(int)FighterBone.Torso].X > recBlocked.BoneEulerDegrees[(int)FighterBone.Torso].X,
                "Branched recovery must over-extend torso forward on whiff (+14 deg) and recoil backward on block (-12 deg).");
        }

        [Fact]
        public void MeshyGlb_LoadsAndExtractsAll4PanelsAndBindsTo19BoneRig()
        {
            // Verify asset exists in assets/ directory
            string assetPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "first_fighter_meshy.glb");
            if (!File.Exists(assetPath))
            {
                assetPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "first_fighter_meshy.glb"));
            }
            Assert.True(File.Exists(assetPath), $"Meshy GLB file must exist at {assetPath}");

            // Extract all 4 turnaround panels via pure binary parser
            var panels = MeshyGlbLoader.LoadOrGetTurnaroundPanels(assetPath);
            Assert.True(panels.FrontVertices.Length > 10000, "Panel 1 (FRONT) must have >10,000 vertices.");
            Assert.True(panels.FrontIndices.Length > 20000, "Panel 1 (FRONT) must have >20,000 triangle indices.");
            Assert.True(panels.Raw.Side.Vertices.Length > 10000, "Panel 2 (SIDE) must have >10,000 vertices.");
            Assert.True(panels.Raw.Back.Vertices.Length > 10000, "Panel 3 (BACK) must have >10,000 vertices.");
            Assert.True(panels.Raw.Guard.Vertices.Length > 10000, "Panel 4 (GUARD) must have >10,000 vertices.");

            // Verify normalization: height must be 1.85m tall [0.00 .. 1.85]
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < panels.FrontVertices.Length; i++)
            {
                float y = panels.FrontVertices[i].Y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            Assert.True(Math.Abs(minY - 0.00f) < 0.001f, $"Panel 1 minY must be 0.00m (was {minY})");
            Assert.True(Math.Abs(maxY - 1.85f) < 0.001f, $"Panel 1 maxY must be 1.85m (was {maxY})");

            // Verify bone classification logic maps Panel 1 triangles across all 19 anatomical bones
            var boneCounts = new int[FirstFighterModelBuilder.BoneCount];
            var verts = panels.FrontVertices;
            var indices = panels.FrontIndices;
            for (int t = 0; t < indices.Length / 3; t++)
            {
                Vector3 v0 = verts[indices[t * 3 + 0]];
                Vector3 v1 = verts[indices[t * 3 + 1]];
                Vector3 v2 = verts[indices[t * 3 + 2]];
                Vector3 center = (v0 + v1 + v2) / 3f;
                FighterBone bone = MeshyGlbLoader.ClassifyCentroidToBone(center);
                boneCounts[(int)bone]++;
            }

            for (int b = 0; b < FirstFighterModelBuilder.BoneCount; b++)
            {
                var bone = (FighterBone)b;
                Assert.True(boneCounts[b] > 0, $"Bone {bone} must have assigned triangles (found {boneCounts[b]}).");
            }

            // Verify bone hierarchy rest transforms are defined for all 19 bones
            var boneWorldPositions = new Vector3[FirstFighterModelBuilder.BoneCount];
            MeshyGlbLoader.ComputeBoneWorldPositions(boneWorldPositions);
            Assert.True(boneWorldPositions[(int)FighterBone.Head].Y > 1.60f, "Head world Y must be above 1.60m.");
            Assert.True(boneWorldPositions[(int)FighterBone.Hips].Y > 0.90f, "Hips world Y must be at waist level (~0.96m).");
        }

        private static FighterButtons GetSyntheticScriptInput(int tick, int playerIndex)
        {
            int phase = (tick + playerIndex * 19) % 37;
            return phase switch
            {
                0 or 1 or 2 => playerIndex == 0 ? FighterButtons.Right : FighterButtons.Left,
                3 => FighterButtons.L,
                8 => FighterButtons.Down | FighterButtons.M,
                14 => FighterButtons.H,
                20 => FighterButtons.S,
                25 or 26 => FighterButtons.Down | (playerIndex == 0 ? FighterButtons.Left : FighterButtons.Right),
                31 => FighterButtons.Up | (playerIndex == 0 ? FighterButtons.Right : FighterButtons.Left),
                34 => FighterButtons.M,
                _ => FighterButtons.None
            };
        }

        public static int Main()
        {
            var suite = new FoundationTests();
            var methods = typeof(FoundationTests).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            int passed = 0;
            int failed = 0;

            Console.WriteLine("================================================================================");
            Console.WriteLine(" FIRST FIGHTER — Headless Deterministic Foundation Verification Suite");
            Console.WriteLine("================================================================================");

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<FactAttribute>() == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(suite, null);
                    passed++;
                    Console.WriteLine($"  [PASS] {method.Name}");
                }
                catch (TargetInvocationException tie)
                {
                    failed++;
                    Console.WriteLine($"  [FAIL] {method.Name}: {tie.InnerException?.Message ?? tie.Message}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"  [FAIL] {method.Name}: {ex.Message}");
                }
            }

            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine($" Passed: {passed}, Failed: {failed}, Total: {passed + failed}");
            Console.WriteLine("================================================================================");

            return failed == 0 ? 0 : 1;
        }
    }
}
