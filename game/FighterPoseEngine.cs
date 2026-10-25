using System;
using System.Collections.Generic;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    public sealed class SkeletonPose
    {
        public Vector3 HipsPositionOffset = Vector3.Zero;
        public readonly Vector3[] BoneEulerDegrees = new Vector3[FirstFighterModelBuilder.BoneCount];
        public readonly Vector3[] BoneScale = new Vector3[FirstFighterModelBuilder.BoneCount];

        public bool ShowSmearArc;
        public Vector3 SmearStartLocal;
        public Vector3 SmearMidLocal;
        public Vector3 SmearEndLocal;
        public bool UseRelaxedOpenHands;

        public SkeletonPose()
        {
            for (int i = 0; i < FirstFighterModelBuilder.BoneCount; i++)
            {
                BoneScale[i] = Vector3.One;
            }
        }

        public SkeletonPose Clone()
        {
            var copy = new SkeletonPose
            {
                HipsPositionOffset = HipsPositionOffset,
                ShowSmearArc = ShowSmearArc,
                SmearStartLocal = SmearStartLocal,
                SmearMidLocal = SmearMidLocal,
                SmearEndLocal = SmearEndLocal,
                UseRelaxedOpenHands = UseRelaxedOpenHands
            };
            Array.Copy(BoneEulerDegrees, copy.BoneEulerDegrees, FirstFighterModelBuilder.BoneCount);
            Array.Copy(BoneScale, copy.BoneScale, FirstFighterModelBuilder.BoneCount);
            return copy;
        }

        public void SetBoneDeg(FighterBone bone, float pitchDeg, float yawDeg, float rollDeg)
        {
            BoneEulerDegrees[(int)bone] = new Vector3(pitchDeg, yawDeg, rollDeg);
        }

        public static SkeletonPose Lerp(SkeletonPose a, SkeletonPose b, float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            var res = new SkeletonPose
            {
                HipsPositionOffset = a.HipsPositionOffset.Lerp(b.HipsPositionOffset, t),
                UseRelaxedOpenHands = t < 0.5f ? a.UseRelaxedOpenHands : b.UseRelaxedOpenHands
            };
            for (int i = 0; i < FirstFighterModelBuilder.BoneCount; i++)
            {
                res.BoneEulerDegrees[i] = a.BoneEulerDegrees[i].Lerp(b.BoneEulerDegrees[i], t);
                res.BoneScale[i] = a.BoneScale[i].Lerp(b.BoneScale[i], t);
            }
            return res;
        }
    }

    public sealed class MoveKeyPoseSet
    {
        public SkeletonPose Windup { get; init; } = null!;
        public SkeletonPose Active { get; init; } = null!;
        public SkeletonPose Recovery { get; init; } = null!;
        public FighterBone StrikingLimbBone { get; init; }
        public Vector3 SmearWindupTip { get; init; }
        public Vector3 SmearApexTip { get; init; }
        public Vector3 SmearActiveTip { get; init; }
    }

    /// <summary>
    /// Non-Linear 3-Keyframe (Windup -> Active -> Recovery) Pose Engine per Typhoon & Andrea Jens Part 1.
    /// Includes exact Panel 4 GUARD stance and Panels 1-3 Turnaround pose from reference_first_fighter.jpg.
    /// </summary>
    public static class FighterPoseEngine
    {
        public static readonly SkeletonPose TurnaroundNeutralPose = BuildTurnaroundNeutralPose();
        public static readonly SkeletonPose GuardPose = BuildGuardPose();
        private static readonly Dictionary<MoveId, MoveKeyPoseSet> MoveKeyPoses = BuildAllMoveKeyPoses();

        /// <summary>
        /// Panels 1-3 (FRONT, SIDE, BACK) of reference_first_fighter.jpg:
        /// Upright 1.85m athletic posture with relaxed open fingers at sides and subtle A-stance leg separation.
        /// </summary>
        public static SkeletonPose BuildTurnaroundNeutralPose()
        {
            var p = new SkeletonPose
            {
                HipsPositionOffset = Vector3.Zero,
                UseRelaxedOpenHands = true
            };
            p.SetBoneDeg(FighterBone.Hips, 0f, 0f, 0f);
            p.SetBoneDeg(FighterBone.Abdomen, 0f, 0f, 0f);
            p.SetBoneDeg(FighterBone.Torso, 1.5f, 0f, 0f);
            p.SetBoneDeg(FighterBone.Neck, -1.0f, 0f, 0f);
            p.SetBoneDeg(FighterBone.Head, 0f, 0f, 0f);

            p.SetBoneDeg(FighterBone.ShoulderL, 0f, 0f, -4f);
            p.SetBoneDeg(FighterBone.ShoulderR, 0f, 0f, 4f);
            p.SetBoneDeg(FighterBone.UpperArmL, 2f, 0f, -7f);
            p.SetBoneDeg(FighterBone.UpperArmR, 2f, 0f, 7f);
            p.SetBoneDeg(FighterBone.LowerArmL, 8f, -10f, 2f);
            p.SetBoneDeg(FighterBone.LowerArmR, 8f, 10f, -2f);
            p.SetBoneDeg(FighterBone.FistL, 0f, 0f, 0f);
            p.SetBoneDeg(FighterBone.FistR, 0f, 0f, 0f);

            p.SetBoneDeg(FighterBone.UpperLegL, 2f, -4f, -3.5f);
            p.SetBoneDeg(FighterBone.UpperLegR, 2f, 4f, 3.5f);
            p.SetBoneDeg(FighterBone.LowerLegL, -2f, 0f, 3.0f);
            p.SetBoneDeg(FighterBone.LowerLegR, -2f, 0f, -3.0f);
            p.SetBoneDeg(FighterBone.FootL, 0f, -6f, 0f);
            p.SetBoneDeg(FighterBone.FootR, 0f, 6f, 0f);
            return p;
        }

        /// <summary>
        /// Exact Panel 4 (GUARD) stance from Section 1.3 & Section 3.1 of the Master Brief:
        /// 3/4 bladed torso (+38 deg hips, +10 deg chest lean, -24 deg head counter-turn),
        /// high-tight Pak Mei / Peekaboo guard arms (+116 deg lead forearm, +126 deg rear forearm),
        /// and coiled athletic stance legs.
        /// </summary>
        public static SkeletonPose BuildGuardPose()
        {
            var p = new SkeletonPose
            {
                HipsPositionOffset = new Vector3(0f, -0.05f, 0f),
                UseRelaxedOpenHands = false
            };

            p.SetBoneDeg(FighterBone.Hips, -6f, 38f, 0f);
            p.SetBoneDeg(FighterBone.Abdomen, 6f, -6f, 0f);
            p.SetBoneDeg(FighterBone.Torso, 10f, -8f, 0f);
            p.SetBoneDeg(FighterBone.Neck, -5f, -12f, 2f);
            p.SetBoneDeg(FighterBone.Head, -5f, -12f, 2f); // Combined Neck+Head = (-10, -24, +4)

            p.SetBoneDeg(FighterBone.ShoulderL, 6f, 8f, 0f);
            p.SetBoneDeg(FighterBone.ShoulderR, 4f, -8f, 0f);

            // Lead Left Arm: UpperArmL (+42, +14, +12), LowerArmL (+116, 0, 0) -> bare left fist at chin height
            p.SetBoneDeg(FighterBone.UpperArmL, 42f, 14f, 12f);
            p.SetBoneDeg(FighterBone.LowerArmL, 116f, 0f, 0f);
            p.SetBoneDeg(FighterBone.FistL, 8f, 0f, 0f);

            // Rear Right Arm: UpperArmR (+24, -16, -12), LowerArmR (+126, 0, 0) -> bare right fist guarding right cheek/jaw
            p.SetBoneDeg(FighterBone.UpperArmR, 24f, -16f, -12f);
            p.SetBoneDeg(FighterBone.LowerArmR, 126f, 0f, 0f);
            p.SetBoneDeg(FighterBone.FistR, 10f, 0f, 0f);

            // Coiled Athletic Stance Legs
            p.SetBoneDeg(FighterBone.UpperLegL, 28f, -12f, -6f);
            p.SetBoneDeg(FighterBone.LowerLegL, -32f, 0f, 0f);
            p.SetBoneDeg(FighterBone.FootL, 4f, 12f, 0f);

            p.SetBoneDeg(FighterBone.UpperLegR, -22f, 28f, 8f);
            p.SetBoneDeg(FighterBone.LowerLegR, -28f, 0f, 0f);
            p.SetBoneDeg(FighterBone.FootR, 12f, -40f, 0f);

            return p;
        }

        public static SkeletonPose EvaluateFighterPose(FighterState f, int matchTick)
        {
            switch (f.State)
            {
                case FighterStateKind.Attack:
                case FighterStateKind.Parry:
                    return EvalAttackPose(
                        f.CurrentMoveId,
                        f.MoveTick,
                        f.Hitstop > 0,
                        f.MoveConnected,
                        f.MoveBlocked);

                case FighterStateKind.Crouch:
                    return BuildCrouchPose();

                case FighterStateKind.WalkFwd:
                    return BuildWalkPose(matchTick, forward: true);

                case FighterStateKind.WalkBack:
                    return BuildWalkPose(matchTick, forward: false);

                case FighterStateKind.DashFwd:
                case FighterStateKind.DashBack:
                    return BuildDashPose(f.State == FighterStateKind.DashFwd);

                case FighterStateKind.JumpStart:
                    return BuildCrouchPose();

                case FighterStateKind.Airborne:
                    return BuildAirbornePose(f.VY.ToMilli());

                case FighterStateKind.Blockstun:
                    return BuildBlockstunPose(f.IsCrouching);

                case FighterStateKind.Hitstun:
                    return BuildHitstunPose(f.IsAirborne, f.IsCrouching);

                case FighterStateKind.Knockdown:
                    return BuildKnockdownPose();

                case FighterStateKind.Wakeup:
                    return SkeletonPose.Lerp(BuildKnockdownPose(), GuardPose, 0.65f);

                default:
                    return BuildBreathingGuardPose(matchTick + f.PlayerIndex * 17);
            }
        }

        /// <summary>
        /// Non-linear 3-keyframe attack pose evaluation per Section 3.2 of the Master Brief:
        /// - First 65% of Startup: ease into Windup
        /// - Final 35% of Startup: snap from Windup to Active with Pow(u, 2.8) + Smear Arc
        /// - Active + Hitstop: lock 100% on Active with 1.22f axial bone stretch on striking limb
        /// - Branched Recovery: recoil -12 deg on Block; over-extend +14 deg on Whiff before settling into GUARD.
        /// </summary>
        public static SkeletonPose EvalAttackPose(
            MoveId moveId,
            int moveTick,
            bool inHitstop,
            bool moveConnected,
            bool moveBlocked)
        {
            if (!MoveKeyPoses.TryGetValue(moveId, out MoveKeyPoseSet? set))
            {
                return GuardPose.Clone();
            }

            MoveDef move = FighterCatalog.FirstFighter.GetMove(moveId);
            int startup = Math.Max(1, move.Startup);
            int activeEnd = startup + Math.Max(1, move.Active);
            int total = Math.Max(activeEnd + 1, move.TotalTicks);

            // Multi-hit moves (236P Rekka and 236H+S Super) cycle briskly between Windup and Active on each strike
            if (move.MultiHitTicks != null && move.MultiHitTicks.Length > 1 && moveTick >= startup && moveTick < activeEnd && !inHitstop)
            {
                int subPhase = (moveTick - startup) % 4;
                if (subPhase == 2)
                {
                    return SkeletonPose.Lerp(set.Active, set.Windup, 0.55f);
                }
            }

            // 1. Active frames OR Hitstop freeze: lock 100% on Active with 1.22f axial limb stretch!
            if (inHitstop || (moveTick >= startup && moveTick < activeEnd))
            {
                var activePose = set.Active.Clone();
                activePose.BoneScale[(int)set.StrikingLimbBone] = new Vector3(1.0f, 1.22f, 1.0f);
                activePose.ShowSmearArc = moveTick == startup;
                activePose.SmearStartLocal = set.SmearWindupTip;
                activePose.SmearMidLocal = set.SmearApexTip;
                activePose.SmearEndLocal = set.SmearActiveTip;
                return activePose;
            }

            // 2. Startup frames (0 .. Startup):
            // First 65% eases into Windup; final 35% snaps to Active with Pow(u, 2.8f) + Smear Arc!
            if (moveTick < startup)
            {
                float norm = (float)moveTick / startup;
                if (norm <= 0.65f)
                {
                    float ease = Mathf.SmoothStep(0f, 1f, norm / 0.65f);
                    return SkeletonPose.Lerp(GuardPose, set.Windup, ease);
                }
                else
                {
                    float u = (norm - 0.65f) / 0.35f;
                    float snapT = Mathf.Pow(Mathf.Clamp(u, 0f, 1f), 2.8f);
                    var pose = SkeletonPose.Lerp(set.Windup, set.Active, snapT);
                    pose.ShowSmearArc = true;
                    pose.SmearStartLocal = set.SmearWindupTip;
                    pose.SmearMidLocal = set.SmearApexTip;
                    pose.SmearEndLocal = set.SmearActiveTip;
                    return pose;
                }
            }

            // 3. Branched Recovery (> Startup + Active):
            float recNorm = Mathf.Clamp((float)(moveTick - activeEnd) / Math.Max(1, total - activeEnd), 0f, 1f);
            var recTarget = set.Recovery.Clone();

            float branchDecay = 1f - recNorm;
            if (moveBlocked)
            {
                // On Block: recoil torso -12 deg backward
                recTarget.BoneEulerDegrees[(int)FighterBone.Torso].X -= 12f * branchDecay;
            }
            else if (!moveConnected)
            {
                // On Whiff: over-extend torso +14 deg forward before settling back into GUARD
                recTarget.BoneEulerDegrees[(int)FighterBone.Torso].X += 14f * branchDecay;
            }

            if (recNorm < 0.45f)
            {
                return SkeletonPose.Lerp(set.Active, recTarget, recNorm / 0.45f);
            }
            return SkeletonPose.Lerp(recTarget, GuardPose, (recNorm - 0.45f) / 0.55f);
        }

        /// <summary>
        /// Applies a SkeletonPose to the BuiltFighterModel hierarchy.
        /// Properly maps positive forward pitch on spine (+Y bones) vs limbs (-Y bones) so limbs always swing forward!
        /// </summary>
        public static void ApplyPoseToModel(BuiltFighterModel model, SkeletonPose pose)
        {
            model.LeftFistClenchedMesh.Visible = !pose.UseRelaxedOpenHands;
            model.RightFistClenchedMesh.Visible = !pose.UseRelaxedOpenHands;
            model.LeftHandRelaxedMesh.Visible = pose.UseRelaxedOpenHands;
            model.RightHandRelaxedMesh.Visible = pose.UseRelaxedOpenHands;

            for (int i = 0; i < FirstFighterModelBuilder.BoneCount; i++)
            {
                Node3D node = model.BoneAttachmentNodes[i];
                Vector3 basePos = model.RestLocalPositions[i];
                if (i == (int)FighterBone.Hips)
                {
                    basePos += pose.HipsPositionOffset;
                }

                Vector3 deg = pose.BoneEulerDegrees[i];
                bool isDownwardLimb = i >= (int)FighterBone.UpperArmL;

                // Downward-hanging bones (-Y) swing forward (+Z) when rotated by -X in right-handed coordinates
                float rx = Mathf.DegToRad(isDownwardLimb ? -deg.X : deg.X);
                float ry = Mathf.DegToRad(deg.Y);
                float rz = Mathf.DegToRad(deg.Z);

                Basis rotBasis = Basis.FromEuler(new Vector3(rx, ry, rz));
                Vector3 scl = pose.BoneScale[i];

                node.Position = basePos;
                node.Basis = rotBasis.Scaled(scl);

                model.Skeleton.SetBonePosePosition(i, basePos);
                model.Skeleton.SetBonePoseRotation(i, rotBasis.GetRotationQuaternion());
                model.Skeleton.SetBonePoseScale(i, scl);
            }
        }

        private static SkeletonPose BuildBreathingGuardPose(int tick)
        {
            var p = GuardPose.Clone();
            float phase = tick * 0.10f;
            float breath = Mathf.Sin(phase);
            p.HipsPositionOffset.Y += breath * 0.008f;
            p.BoneEulerDegrees[(int)FighterBone.Torso].X += breath * 1.2f;
            p.BoneEulerDegrees[(int)FighterBone.UpperArmL].X += breath * 1.8f;
            p.BoneEulerDegrees[(int)FighterBone.UpperArmR].X -= breath * 1.4f;
            return p;
        }

        private static SkeletonPose BuildCrouchPose()
        {
            var p = GuardPose.Clone();
            p.HipsPositionOffset = new Vector3(0f, -0.40f, 0f);
            p.SetBoneDeg(FighterBone.Torso, 18f, -10f, 0f);
            p.SetBoneDeg(FighterBone.Head, -14f, -20f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegL, 68f, -14f, -6f);
            p.SetBoneDeg(FighterBone.LowerLegL, -82f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegR, 42f, 24f, 8f);
            p.SetBoneDeg(FighterBone.LowerLegR, -78f, 0f, 0f);
            return p;
        }

        private static SkeletonPose BuildWalkPose(int tick, bool forward)
        {
            var p = GuardPose.Clone();
            float dir = forward ? 1f : -1f;
            float cyc = Mathf.Sin(tick * 0.28f * dir);
            p.HipsPositionOffset = new Vector3(0f, -0.04f + Mathf.Abs(cyc) * 0.015f, 0f);
            p.BoneEulerDegrees[(int)FighterBone.UpperLegL].X += cyc * 16f;
            p.BoneEulerDegrees[(int)FighterBone.UpperLegR].X -= cyc * 16f;
            p.BoneEulerDegrees[(int)FighterBone.LowerLegL].X -= Mathf.Max(0f, -cyc) * 18f;
            p.BoneEulerDegrees[(int)FighterBone.LowerLegR].X -= Mathf.Max(0f, cyc) * 18f;
            return p;
        }

        private static SkeletonPose BuildDashPose(bool forward)
        {
            var p = GuardPose.Clone();
            p.HipsPositionOffset = new Vector3(0f, -0.12f, 0f);
            p.SetBoneDeg(FighterBone.Torso, forward ? 26f : -14f, 18f, 0f);
            p.SetBoneDeg(FighterBone.Head, forward ? -20f : 10f, -18f, 0f);
            return p;
        }

        private static SkeletonPose BuildAirbornePose(int vyMilli)
        {
            var p = GuardPose.Clone();
            float tuck = vyMilli >= 0 ? 1f : 0.4f;
            p.HipsPositionOffset = new Vector3(0f, 0.04f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegL, 54f * tuck + 18f, -10f, -4f);
            p.SetBoneDeg(FighterBone.LowerLegL, -72f * tuck - 24f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegR, 38f * tuck + 10f, 16f, 6f);
            p.SetBoneDeg(FighterBone.LowerLegR, -64f * tuck - 20f, 0f, 0f);
            return p;
        }

        private static SkeletonPose BuildBlockstunPose(bool crouching)
        {
            var p = crouching ? BuildCrouchPose() : GuardPose.Clone();
            p.SetBoneDeg(FighterBone.Torso, -8f, 22f, 0f);
            p.SetBoneDeg(FighterBone.UpperArmL, 62f, 28f, 18f);
            p.SetBoneDeg(FighterBone.LowerArmL, 128f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperArmR, 56f, -24f, -18f);
            p.SetBoneDeg(FighterBone.LowerArmR, 132f, 0f, 0f);
            return p;
        }

        private static SkeletonPose BuildHitstunPose(bool airborne, bool crouching)
        {
            var p = crouching ? BuildCrouchPose() : GuardPose.Clone();
            p.SetBoneDeg(FighterBone.Abdomen, -12f, 10f, 4f);
            p.SetBoneDeg(FighterBone.Torso, -18f, 18f, 6f);
            p.SetBoneDeg(FighterBone.Head, -22f, -15f, -8f);
            p.SetBoneDeg(FighterBone.UpperArmL, 12f, -20f, -24f);
            p.SetBoneDeg(FighterBone.LowerArmL, 48f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperArmR, 8f, 20f, 24f);
            p.SetBoneDeg(FighterBone.LowerArmR, 52f, 0f, 0f);
            if (airborne)
            {
                p.SetBoneDeg(FighterBone.Hips, -28f, 14f, 0f);
            }
            return p;
        }

        private static SkeletonPose BuildKnockdownPose()
        {
            var p = GuardPose.Clone();
            p.HipsPositionOffset = new Vector3(0f, -0.82f, -0.15f);
            p.SetBoneDeg(FighterBone.Hips, -78f, 12f, 0f);
            p.SetBoneDeg(FighterBone.Torso, 12f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperArmL, -25f, 0f, -35f);
            p.SetBoneDeg(FighterBone.LowerArmL, 35f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegL, 18f, 0f, -8f);
            p.SetBoneDeg(FighterBone.LowerLegL, -24f, 0f, 0f);
            p.SetBoneDeg(FighterBone.UpperLegR, 10f, 0f, 8f);
            p.SetBoneDeg(FighterBone.LowerLegR, -18f, 0f, 0f);
            return p;
        }

        private static Dictionary<MoveId, MoveKeyPoseSet> BuildAllMoveKeyPoses()
        {
            var dict = new Dictionary<MoveId, MoveKeyPoseSet>();

            // 1. 5L — Snap Lead Jab (razor-straight horizontal left fist at chin height Y: 0.94m, rear right fist glued to cheek)
            {
                var w = GuardPose.Clone();
                w.SetBoneDeg(FighterBone.Hips, -6f, 44f, 0f);
                w.SetBoneDeg(FighterBone.Torso, 8f, 12f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmL, 28f, 18f, 14f);
                w.SetBoneDeg(FighterBone.LowerArmL, 128f, 0f, 0f);

                var a = GuardPose.Clone();
                a.SetBoneDeg(FighterBone.Hips, -4f, 14f, 0f);
                a.SetBoneDeg(FighterBone.Torso, 14f, -24f, 0f);
                a.SetBoneDeg(FighterBone.Head, -10f, 10f, 0f);
                a.SetBoneDeg(FighterBone.ShoulderL, 14f, -18f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmL, 88f, -4f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmL, 4f, 0f, 0f); // Full razor-straight horizontal extension
                // Rear right fist stays glued to cheek
                a.SetBoneDeg(FighterBone.UpperArmR, 28f, -18f, -12f);
                a.SetBoneDeg(FighterBone.LowerArmR, 130f, 0f, 0f);

                var r = SkeletonPose.Lerp(a, GuardPose, 0.55f);
                dict[MoveId.StandL] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = r,
                    StrikingLimbBone = FighterBone.LowerArmL,
                    SmearWindupTip = new Vector3(-0.14f, 1.48f, 0.24f),
                    SmearApexTip = new Vector3(-0.08f, 1.52f, 0.52f),
                    SmearActiveTip = new Vector3(0.00f, 1.48f, 0.72f)
                };
            }

            // 2. 2L — Low Lead Hook-Check (Deep knee bend Hips -0.40m -> quick low oblique punch)
            {
                var w = BuildCrouchPose();
                w.SetBoneDeg(FighterBone.Torso, 22f, 18f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmL, 24f, 20f, 10f);
                w.SetBoneDeg(FighterBone.LowerArmL, 118f, 0f, 0f);

                var a = BuildCrouchPose();
                a.HipsPositionOffset = new Vector3(0f, -0.42f, 0.05f);
                a.SetBoneDeg(FighterBone.Torso, 28f, -26f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmL, 62f, -8f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmL, 12f, 0f, 0f);

                dict[MoveId.CrouchL] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildCrouchPose(),
                    StrikingLimbBone = FighterBone.LowerArmL,
                    SmearWindupTip = new Vector3(-0.14f, 0.95f, 0.22f),
                    SmearApexTip = new Vector3(-0.06f, 0.72f, 0.48f),
                    SmearActiveTip = new Vector3(0.00f, 0.52f, 0.68f)
                };
            }

            // 3. 5M — Side-Thrust Snap Kick (High knee chamber UpperLegR +105 deg -> full horizontal side-thrust kick UpperLegR +88 deg, LowerLegR 0 deg)
            {
                var w = GuardPose.Clone();
                w.SetBoneDeg(FighterBone.Hips, -8f, -24f, 8f);
                w.SetBoneDeg(FighterBone.Torso, -8f, -12f, -8f);
                w.SetBoneDeg(FighterBone.UpperLegR, 105f, 12f, 10f);
                w.SetBoneDeg(FighterBone.LowerLegR, -110f, 0f, 0f);

                var a = GuardPose.Clone();
                a.SetBoneDeg(FighterBone.Hips, -14f, -42f, 16f);
                a.SetBoneDeg(FighterBone.Torso, -18f, 18f, -14f);
                a.SetBoneDeg(FighterBone.UpperLegR, 88f, 0f, 8f);
                a.SetBoneDeg(FighterBone.LowerLegR, 0f, 0f, 0f); // Full horizontal side-thrust extension
                a.SetBoneDeg(FighterBone.FootR, -18f, 0f, 0f);

                dict[MoveId.StandM] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.6f),
                    StrikingLimbBone = FighterBone.LowerLegR,
                    SmearWindupTip = new Vector3(0.12f, 0.98f, 0.25f),
                    SmearApexTip = new Vector3(0.06f, 0.92f, 0.58f),
                    SmearActiveTip = new Vector3(0.00f, 0.88f, 0.88f)
                };
            }

            // 4. 2M — Tailored Low Sweep (Drops low Hips -0.48m, torso leaned back -30 deg -> whips long right leg across ankle height Y: 80..320 mu)
            {
                var w = BuildCrouchPose();
                w.HipsPositionOffset = new Vector3(0f, -0.46f, 0f);
                w.SetBoneDeg(FighterBone.Torso, -18f, 34f, 0f);
                w.SetBoneDeg(FighterBone.UpperLegR, 48f, 35f, 12f);
                w.SetBoneDeg(FighterBone.LowerLegR, -68f, 0f, 0f);

                var a = BuildCrouchPose();
                a.HipsPositionOffset = new Vector3(0f, -0.48f, 0.04f);
                a.SetBoneDeg(FighterBone.Hips, -12f, -36f, 0f);
                a.SetBoneDeg(FighterBone.Torso, -30f, 28f, -10f);
                a.SetBoneDeg(FighterBone.UpperArmL, -28f, 0f, -18f); // Left hand near floor for balance
                a.SetBoneDeg(FighterBone.LowerArmL, 12f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperLegR, 64f, -12f, 8f);
                a.SetBoneDeg(FighterBone.LowerLegR, -4f, 0f, 0f); // Extended low sweep at ankle height

                dict[MoveId.CrouchM] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildCrouchPose(),
                    StrikingLimbBone = FighterBone.LowerLegR,
                    SmearWindupTip = new Vector3(0.28f, 0.22f, 0.18f),
                    SmearApexTip = new Vector3(0.14f, 0.18f, 0.58f),
                    SmearActiveTip = new Vector3(-0.08f, 0.16f, 0.88f)
                };
            }

            // 5. 5H — Step-In Rear Cross & Elbow (Rear shoulder coils back -38 deg -> explosive rear straight cross Torso +46 deg, UpperArmR +90 deg, LowerArmR 0 deg)
            {
                var w = GuardPose.Clone();
                w.SetBoneDeg(FighterBone.Hips, -8f, 52f, 0f);
                w.SetBoneDeg(FighterBone.Torso, 6f, 38f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmR, -18f, -24f, -16f);
                w.SetBoneDeg(FighterBone.LowerArmR, 118f, 0f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.08f, 0.08f);
                a.SetBoneDeg(FighterBone.Hips, 8f, -28f, 0f);
                a.SetBoneDeg(FighterBone.Torso, 22f, -46f, 0f);
                a.SetBoneDeg(FighterBone.Head, -14f, 32f, 0f);
                a.SetBoneDeg(FighterBone.ShoulderR, 18f, 22f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 90f, 6f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmR, 0f, 0f, 0f);
                a.SetBoneDeg(FighterBone.FootR, 28f, 0f, 0f); // Rear heel pivoted up

                dict[MoveId.StandH] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.5f),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.22f, 1.38f, 0.10f),
                    SmearApexTip = new Vector3(0.10f, 1.44f, 0.52f),
                    SmearActiveTip = new Vector3(0.00f, 1.42f, 0.86f)
                };
            }

            // 6. 6H — Hopping Downward Elbow Overhead (16f hop-step raising rear elbow high UpperArmR +130 deg -> crashing downward vertical elbow strike)
            {
                var w = GuardPose.Clone();
                w.HipsPositionOffset = new Vector3(0f, 0.14f, 0.04f); // Hop-step airborne lift
                w.SetBoneDeg(FighterBone.Torso, -8f, 24f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmR, 130f, 0f, -12f);
                w.SetBoneDeg(FighterBone.LowerArmR, 125f, 0f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.14f, 0.10f);
                a.SetBoneDeg(FighterBone.Hips, 12f, -24f, 0f);
                a.SetBoneDeg(FighterBone.Torso, 34f, -38f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 68f, 18f, -10f);
                a.SetBoneDeg(FighterBone.LowerArmR, 132f, 0f, 0f);

                dict[MoveId.ForwardH] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.5f),
                    StrikingLimbBone = FighterBone.UpperArmR,
                    SmearWindupTip = new Vector3(0.16f, 1.78f, 0.24f),
                    SmearApexTip = new Vector3(0.08f, 1.48f, 0.58f),
                    SmearActiveTip = new Vector3(0.02f, 1.02f, 0.72f)
                };
            }

            // 7. 5S — Rising Knee & Sky Uppercut (Deep crouch load Hips -0.34m -> driving right knee + vertical right sky-uppercut UpperArmR +162 deg)
            {
                var w = GuardPose.Clone();
                w.HipsPositionOffset = new Vector3(0f, -0.34f, 0f);
                w.SetBoneDeg(FighterBone.Torso, 26f, 32f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmR, -12f, -18f, -10f);
                w.SetBoneDeg(FighterBone.LowerArmR, 110f, 0f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, 0.08f, 0.06f);
                a.SetBoneDeg(FighterBone.Hips, -6f, -28f, 0f);
                a.SetBoneDeg(FighterBone.Torso, -12f, -36f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 162f, 0f, -6f);
                a.SetBoneDeg(FighterBone.LowerArmR, 18f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperLegR, 92f, 0f, 0f); // Driving right knee
                a.SetBoneDeg(FighterBone.LowerLegR, -105f, 0f, 0f);

                dict[MoveId.StandS] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.55f),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.18f, 0.92f, 0.32f),
                    SmearApexTip = new Vector3(0.10f, 1.42f, 0.62f),
                    SmearActiveTip = new Vector3(0.04f, 1.92f, 0.48f)
                };
            }

            // 8. j.L — Air Snap Backfist
            {
                var w = BuildAirbornePose(200);
                w.SetBoneDeg(FighterBone.Torso, 10f, 28f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmL, 48f, 32f, 12f);
                w.SetBoneDeg(FighterBone.LowerArmL, 110f, 0f, 0f);

                var a = BuildAirbornePose(200);
                a.SetBoneDeg(FighterBone.Torso, 14f, -26f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmL, 84f, -8f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmL, 6f, 0f, 0f);

                dict[MoveId.AirL] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildAirbornePose(0),
                    StrikingLimbBone = FighterBone.LowerArmL,
                    SmearWindupTip = new Vector3(-0.18f, 1.38f, 0.22f),
                    SmearApexTip = new Vector3(-0.06f, 1.42f, 0.54f),
                    SmearActiveTip = new Vector3(0.02f, 1.36f, 0.76f)
                };
            }

            // 9. j.M — Air Roundhouse Kick (UpperLegR +86 deg)
            {
                var w = BuildAirbornePose(150);
                w.SetBoneDeg(FighterBone.UpperLegR, 96f, 24f, 16f);
                w.SetBoneDeg(FighterBone.LowerLegR, -108f, 0f, 0f);

                var a = BuildAirbornePose(150);
                a.SetBoneDeg(FighterBone.Hips, -10f, -38f, 14f);
                a.SetBoneDeg(FighterBone.Torso, -14f, 22f, -12f);
                a.SetBoneDeg(FighterBone.UpperLegR, 86f, -8f, 12f);
                a.SetBoneDeg(FighterBone.LowerLegR, 0f, 0f, 0f);

                dict[MoveId.AirM] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildAirbornePose(0),
                    StrikingLimbBone = FighterBone.LowerLegR,
                    SmearWindupTip = new Vector3(0.24f, 0.92f, 0.24f),
                    SmearApexTip = new Vector3(0.10f, 0.96f, 0.58f),
                    SmearActiveTip = new Vector3(-0.04f, 0.94f, 0.86f)
                };
            }

            // 10. j.H — Air Spinning Hook Kick
            {
                var w = BuildAirbornePose(100);
                w.SetBoneDeg(FighterBone.Hips, -8f, 48f, -10f);
                w.SetBoneDeg(FighterBone.UpperLegL, 92f, -20f, -14f);
                w.SetBoneDeg(FighterBone.LowerLegL, -96f, 0f, 0f);

                var a = BuildAirbornePose(100);
                a.SetBoneDeg(FighterBone.Hips, -16f, 42f, -18f);
                a.SetBoneDeg(FighterBone.Torso, -20f, -24f, 14f);
                a.SetBoneDeg(FighterBone.UpperLegL, 92f, 10f, -10f);
                a.SetBoneDeg(FighterBone.LowerLegL, 0f, 0f, 0f);

                dict[MoveId.AirH] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildAirbornePose(0),
                    StrikingLimbBone = FighterBone.LowerLegL,
                    SmearWindupTip = new Vector3(-0.24f, 0.96f, 0.22f),
                    SmearApexTip = new Vector3(-0.08f, 1.02f, 0.62f),
                    SmearActiveTip = new Vector3(0.08f, 0.98f, 0.88f)
                };
            }

            // 11. j.S — Axe-Heel Meteor Drop (High knee tuck -> violent downward axe-heel stomp)
            {
                var w = BuildAirbornePose(120);
                w.SetBoneDeg(FighterBone.Torso, -16f, -18f, 0f);
                w.SetBoneDeg(FighterBone.UpperLegR, 128f, 0f, 6f);
                w.SetBoneDeg(FighterBone.LowerLegR, -42f, 0f, 0f);

                var a = BuildAirbornePose(-260);
                a.SetBoneDeg(FighterBone.Torso, 24f, -24f, 0f);
                a.SetBoneDeg(FighterBone.UpperLegR, 52f, 0f, 4f);
                a.SetBoneDeg(FighterBone.LowerLegR, 0f, 0f, 0f);
                a.SetBoneDeg(FighterBone.FootR, -24f, 0f, 0f);

                dict[MoveId.AirS] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = BuildAirbornePose(-100),
                    StrikingLimbBone = FighterBone.LowerLegR,
                    SmearWindupTip = new Vector3(0.08f, 1.56f, 0.42f),
                    SmearApexTip = new Vector3(0.06f, 0.98f, 0.72f),
                    SmearActiveTip = new Vector3(0.04f, 0.24f, 0.64f)
                };
            }

            // 12. 236L/M/H — Pak Mei Chain Flurry (3-Hit Rekka Burst)
            {
                var w = GuardPose.Clone();
                w.HipsPositionOffset = new Vector3(0f, -0.10f, 0f);
                w.SetBoneDeg(FighterBone.Torso, 16f, 24f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmL, 62f, 12f, 8f);
                w.SetBoneDeg(FighterBone.LowerArmL, 84f, 0f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.12f, 0.08f);
                a.SetBoneDeg(FighterBone.Torso, 22f, -32f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 88f, 0f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmR, 4f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmL, 74f, 8f, 8f);
                a.SetBoneDeg(FighterBone.LowerArmL, 38f, 0f, 0f);

                dict[MoveId.QcfRekka] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.5f),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.14f, 1.34f, 0.28f),
                    SmearApexTip = new Vector3(0.04f, 1.40f, 0.62f),
                    SmearActiveTip = new Vector3(0.00f, 1.38f, 0.88f)
                };
            }

            // 13. 623H — Dragon Rising Uppercut (Invuln 1..7, Launch)
            {
                var w = GuardPose.Clone();
                w.HipsPositionOffset = new Vector3(0f, -0.36f, 0.04f);
                w.SetBoneDeg(FighterBone.Torso, 28f, 38f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmR, -16f, -20f, -12f);
                w.SetBoneDeg(FighterBone.LowerArmR, 116f, 0f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, 0.16f, 0.08f);
                a.SetBoneDeg(FighterBone.Hips, -8f, -32f, 0f);
                a.SetBoneDeg(FighterBone.Torso, -14f, -42f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 168f, 0f, -4f);
                a.SetBoneDeg(FighterBone.LowerArmR, 14f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperLegR, 96f, 0f, 0f);
                a.SetBoneDeg(FighterBone.LowerLegR, -112f, 0f, 0f);

                dict[MoveId.DpUpper] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = SkeletonPose.Lerp(a, GuardPose, 0.5f),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.16f, 0.88f, 0.28f),
                    SmearApexTip = new Vector3(0.08f, 1.44f, 0.64f),
                    SmearActiveTip = new Vector3(0.02f, 1.98f, 0.44f)
                };
            }

            // 14. 214S — Intercept Forearm Deflect (Parry)
            {
                var w = GuardPose.Clone();
                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.08f, 0f);
                a.SetBoneDeg(FighterBone.Hips, -6f, 24f, 0f);
                a.SetBoneDeg(FighterBone.Torso, 6f, -18f, 0f);
                // Tucks lead forearm across centerline
                a.SetBoneDeg(FighterBone.UpperArmL, 68f, 36f, 24f);
                a.SetBoneDeg(FighterBone.LowerArmL, 102f, 0f, 0f);

                dict[MoveId.ParryDeflect] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = GuardPose.Clone(),
                    StrikingLimbBone = FighterBone.LowerArmL,
                    SmearWindupTip = new Vector3(-0.16f, 1.38f, 0.28f),
                    SmearApexTip = new Vector3(0.00f, 1.44f, 0.36f),
                    SmearActiveTip = new Vector3(0.14f, 1.42f, 0.34f)
                };
            }

            // 15. ParryCounter — Auto-triggered launching throat-palm counter
            {
                var w = dict[MoveId.ParryDeflect].Active.Clone();
                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.06f, 0.10f);
                a.SetBoneDeg(FighterBone.Torso, 18f, -42f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 112f, 0f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmR, 6f, 0f, 0f);

                dict[MoveId.ParryCounter] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = GuardPose.Clone(),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.14f, 1.36f, 0.28f),
                    SmearApexTip = new Vector3(0.06f, 1.48f, 0.58f),
                    SmearActiveTip = new Vector3(0.00f, 1.62f, 0.82f)
                };
            }

            // 16. 236H+S — SUPER: FIVE-POINT STRUCTURE BREAK
            {
                var w = GuardPose.Clone();
                w.HipsPositionOffset = new Vector3(0f, -0.18f, -0.04f);
                w.SetBoneDeg(FighterBone.Torso, -8f, 36f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmL, 38f, -24f, -18f);
                w.SetBoneDeg(FighterBone.UpperArmR, 38f, 24f, 18f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.14f, 0.12f);
                a.SetBoneDeg(FighterBone.Hips, 8f, 0f, 0f);
                a.SetBoneDeg(FighterBone.Torso, 24f, 0f, 0f);
                // Twin-palm shockwave strike
                a.SetBoneDeg(FighterBone.UpperArmL, 92f, -10f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmL, 4f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 92f, 10f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmR, 4f, 0f, 0f);

                dict[MoveId.SuperArt] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = GuardPose.Clone(),
                    StrikingLimbBone = FighterBone.LowerArmR,
                    SmearWindupTip = new Vector3(0.00f, 1.34f, 0.22f),
                    SmearApexTip = new Vector3(0.00f, 1.40f, 0.58f),
                    SmearActiveTip = new Vector3(0.00f, 1.42f, 0.90f)
                };
            }

            // 17. L+M — Clinch Takedown Throw
            {
                var w = GuardPose.Clone();
                w.SetBoneDeg(FighterBone.UpperArmL, 78f, -12f, 0f);
                w.SetBoneDeg(FighterBone.UpperArmR, 78f, 12f, 0f);

                var a = GuardPose.Clone();
                a.HipsPositionOffset = new Vector3(0f, -0.22f, 0.08f);
                a.SetBoneDeg(FighterBone.Torso, 36f, -28f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmL, 64f, 18f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmL, 42f, 0f, 0f);
                a.SetBoneDeg(FighterBone.UpperArmR, 58f, -18f, 0f);
                a.SetBoneDeg(FighterBone.LowerArmR, 48f, 0f, 0f);

                dict[MoveId.Throw] = new MoveKeyPoseSet
                {
                    Windup = w,
                    Active = a,
                    Recovery = GuardPose.Clone(),
                    StrikingLimbBone = FighterBone.LowerArmL,
                    SmearWindupTip = new Vector3(0.00f, 1.44f, 0.28f),
                    SmearApexTip = new Vector3(0.00f, 1.32f, 0.56f),
                    SmearActiveTip = new Vector3(0.00f, 1.12f, 0.68f)
                };
            }

            return dict;
        }
    }
}
