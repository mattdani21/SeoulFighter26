using System;

namespace FirstFighter.Sim
{
    /// <summary>
    /// Strict Finite State Machine states per Andrea Jens Part 3.
    /// </summary>
    public enum FighterStateKind : byte
    {
        Idle = 0,
        WalkFwd = 1,
        WalkBack = 2,
        Crouch = 3,
        JumpStart = 4,   // 3 ticks pre-jump frames
        Airborne = 5,
        DashFwd = 6,
        DashBack = 7,
        Attack = 8,
        Parry = 9,
        Hitstun = 10,
        Blockstun = 11,
        ThrowTech = 12,
        Knockdown = 13,
        Wakeup = 14
    }

    public enum MoveId : byte
    {
        None = 0,
        StandL = 1,       // 5L: Snap Lead Jab (High)
        CrouchL = 2,      // 2L: Low Lead Hook-Check (Low)
        StandM = 3,       // 5M: Side-Thrust Snap Kick (Mid)
        CrouchM = 4,      // 2M: Tailored Low Sweep (Low)
        StandH = 5,       // 5H: Step-In Rear Cross & Elbow (Mid)
        ForwardH = 6,     // 6H: Hopping Downward Elbow (Overhead)
        StandS = 7,       // 5S: Rising Knee & Sky Uppercut (Mid Launcher)
        AirL = 8,         // j.L: Air Snap Backfist (Air Overhead)
        AirM = 9,         // j.M: Air Roundhouse Kick (Air Overhead)
        AirH = 10,        // j.H: Air Spinning Hook Kick (Air Overhead)
        AirS = 11,        // j.S: Axe-Heel Meteor Drop (Air Ground-Bounce)
        QcfRekka = 12,    // 236L/M/H: Pak Mei Chain Flurry (3-Hit Rekka Burst)
        DpUpper = 13,     // 623H: Dragon Rising Uppercut (Invuln 1..7, Launcher)
        ParryDeflect = 14,// 214S / 4S: Intercept Forearm Deflect (Parry)
        ParryCounter = 15,// Auto-triggered launching palm counter after successful Parry
        SuperArt = 16,    // 236H+S: SUPER: FIVE-POINT STRUCTURE BREAK (6-Hit Cinema)
        Throw = 17        // L+M: Clinch Takedown Throw
    }

    public enum AttackHeight : byte
    {
        High = 0,
        Mid = 1,
        Low = 2,
        Overhead = 3,
        Throw = 4,
        Parry = 5
    }

    public enum MoveStance : byte
    {
        Stand = 0,
        Crouch = 1,
        Air = 2
    }

    /// <summary>
    /// Axis-aligned bounding box in milli-units relative to fighter origin (X, Y).
    /// OffsetX is local forward (+X when facing right, mirrored when facing left).
    /// OffsetY is height above floor (0 = ground plane).
    /// </summary>
    public readonly struct BoxDef
    {
        public readonly int OffsetX;
        public readonly int OffsetY;
        public readonly int Width;
        public readonly int Height;

        public BoxDef(int offsetX, int offsetY, int width, int height)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = width;
            Height = height;
        }

        public void GetWorldBounds(int fighterX, int fighterY, bool facingRight, out int minX, out int maxX, out int minY, out int maxY)
        {
            if (facingRight)
            {
                minX = fighterX + OffsetX;
                maxX = minX + Width;
            }
            else
            {
                maxX = fighterX - OffsetX;
                minX = maxX - Width;
            }
            minY = fighterY + OffsetY;
            maxY = minY + Height;
        }

        public static bool OverlapsWorld(
            BoxDef a, int ax, int ay, bool aFacingRight,
            BoxDef b, int bx, int by, bool bFacingRight)
        {
            a.GetWorldBounds(ax, ay, aFacingRight, out int aMinX, out int aMaxX, out int aMinY, out int aMaxY);
            b.GetWorldBounds(bx, by, bFacingRight, out int bMinX, out int bMaxX, out int bMinY, out int bMaxY);

            return aMinX < bMaxX && aMaxX > bMinX && aMinY < bMaxY && aMaxY > bMinY;
        }
    }

    public readonly struct CancelSpec
    {
        public readonly bool CancelIntoTargetNormals;
        public readonly bool CancelIntoSpecials;
        public readonly bool CancelIntoSuper;
        public readonly bool JumpCancelOnHit;
        public readonly MoveId[] AllowedTargetNormals;

        public CancelSpec(
            bool cancelIntoTargetNormals,
            bool cancelIntoSpecials,
            bool cancelIntoSuper,
            bool jumpCancelOnHit,
            MoveId[] allowedTargetNormals)
        {
            CancelIntoTargetNormals = cancelIntoTargetNormals;
            CancelIntoSpecials = cancelIntoSpecials;
            CancelIntoSuper = cancelIntoSuper;
            JumpCancelOnHit = jumpCancelOnHit;
            AllowedTargetNormals = allowedTargetNormals;
        }

        public bool CanCancelIntoNormal(MoveId target)
        {
            if (!CancelIntoTargetNormals || AllowedTargetNormals == null)
            {
                return false;
            }
            for (int i = 0; i < AllowedTargetNormals.Length; i++)
            {
                if (AllowedTargetNormals[i] == target)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class MoveDef
    {
        public MoveId Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string InputNotation { get; init; } = string.Empty;
        public MoveStance Stance { get; init; }
        public AttackHeight Height { get; init; }
        public int Priority { get; init; }

        public int Startup { get; init; }
        public int Active { get; init; }
        public int Recovery { get; init; }
        public int TotalTicks => Startup + Active + Recovery;

        public int Damage { get; init; }
        public int StructureDamage { get; init; }
        public int MeterGainOnHit { get; init; }
        public int MeterGainOnBlock { get; init; }
        public int MeterCost { get; init; }

        public int Hitstun { get; init; }
        public int Blockstun { get; init; }
        public int Hitstop { get; init; }

        public int StepStartTick { get; init; }
        public int StepEndTick { get; init; }
        public int StepForwardMuPerTick { get; init; }

        public int KnockbackXMu { get; init; }
        public int BlockPushbackXMu { get; init; }

        public bool Launches { get; init; }
        public int LaunchVX { get; init; }
        public int LaunchVY { get; init; }

        public int AirPopVY { get; init; }
        public bool GroundBounce { get; init; }
        public bool CausesHardKnockdown { get; init; }

        public int InvulnStartTick { get; init; }
        public int InvulnEndTick { get; init; }

        public int ParryStartTick { get; init; }
        public int ParryEndTick { get; init; }

        public int[] MultiHitTicks { get; init; } = Array.Empty<int>();

        public BoxDef Hitbox { get; init; }
        public CancelSpec Cancels { get; init; }
        public string KeyPoseSummary { get; init; } = string.Empty;

        public bool IsActiveAtTick(int moveTick)
        {
            return moveTick >= Startup && moveTick < Startup + Active;
        }

        public bool ShouldRearmHitAtTick(int moveTick)
        {
            if (MultiHitTicks == null || MultiHitTicks.Length == 0)
            {
                return moveTick == Startup;
            }
            for (int i = 0; i < MultiHitTicks.Length; i++)
            {
                if (MultiHitTicks[i] == moveTick)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class FighterDef
    {
        public string Id { get; init; } = "first_fighter";
        public string DisplayName { get; init; } = "FIRST FIGHTER";
        public int MaxHealth { get; init; } = 1000;
        public int MaxStructure { get; init; } = 100;
        public int MaxMeter { get; init; } = 100;

        public int WalkForwardSpeedMu { get; init; } = 36;
        public int WalkBackSpeedMu { get; init; } = 28;
        public int DashForwardSpeedMu { get; init; } = 74;
        public int DashBackSpeedMu { get; init; } = 66;
        public int DashDurationTicks { get; init; } = 12;

        public int JumpStartTicks { get; init; } = 3;
        public int JumpForwardVX { get; init; } = 175;
        public int JumpBackVX { get; init; } = -155;
        public int JumpInitialVY { get; init; } = 520;
        public int GravityPerTick { get; init; } = 22;

        public BoxDef Pushbox { get; init; }
        public BoxDef[] StandingHurtboxes { get; init; } = Array.Empty<BoxDef>();
        public BoxDef[] CrouchingHurtboxes { get; init; } = Array.Empty<BoxDef>();
        public BoxDef[] AirborneHurtboxes { get; init; } = Array.Empty<BoxDef>();

        public MoveDef[] Moves { get; init; } = Array.Empty<MoveDef>();

        public MoveDef GetMove(MoveId id)
        {
            for (int i = 0; i < Moves.Length; i++)
            {
                if (Moves[i].Id == id)
                {
                    return Moves[i];
                }
            }
            return Moves[0];
        }
    }
}
