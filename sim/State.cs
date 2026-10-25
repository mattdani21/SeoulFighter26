using System;
using System.IO;

namespace FirstFighter.Sim
{
    /// <summary>
    /// Color-coded 60-tick Frame Meter cell categories for HudAndFrameStudio.
    /// </summary>
    public enum FrameTimelineCellKind : byte
    {
        Neutral = 0,
        Movement = 1,
        Startup = 2,      // Cyan
        Active = 3,       // Red
        Recovery = 4,     // Blue
        Hitstun = 5,      // Yellow
        Blockstun = 6,    // Orange-Yellow
        ParryOrInvuln = 7 // Gold
    }

    public sealed class FighterState
    {
        public const int FrameMeterWindow = 60;

        public int PlayerIndex;
        public FighterStateKind State;
        public int StateTimer;

        public Fix X;
        public Fix Y;
        public Fix VX;
        public Fix VY;
        public bool FacingRight;

        public int Health;
        public int Structure;
        public int Meter;

        public int Hitstop;

        public MoveId CurrentMoveId;
        public int MoveTick;
        public bool MoveHitArmed;
        public bool MoveConnected;
        public bool MoveBlocked;

        public int ComboHits;
        public int ComboDamage;
        public int LastScalingPercent = 100;

        public bool WillGroundBounce;
        public bool UsedGroundBounce;

        public int LastFrameAdvantage;
        public bool LastOutcomeWasBlock;
        public bool HasFrameAdvantageSample;

        public readonly InputRingBuffer InputBuffer = new InputRingBuffer();
        public readonly FrameTimelineCellKind[] FrameTimeline = new FrameTimelineCellKind[FrameMeterWindow];
        public int FrameTimelineCursor;

        public bool IsAirborne => Y > Fix.Zero || State == FighterStateKind.Airborne;

        public bool IsCrouching
        {
            get
            {
                if (IsAirborne) return false;
                if (State == FighterStateKind.Crouch) return true;
                if (State == FighterStateKind.Attack)
                {
                    var move = FighterCatalog.FirstFighter.GetMove(CurrentMoveId);
                    return move.Stance == MoveStance.Crouch;
                }
                if (State == FighterStateKind.Blockstun || State == FighterStateKind.Hitstun)
                {
                    int dir = InputBuffer.GetNumpadDirectionAgo(0, FacingRight);
                    return dir == 1 || dir == 2 || dir == 3;
                }
                return false;
            }
        }

        public bool IsInvulnerable
        {
            get
            {
                if (State == FighterStateKind.Knockdown || State == FighterStateKind.Wakeup)
                {
                    return true;
                }
                if (State == FighterStateKind.Attack)
                {
                    var move = FighterCatalog.FirstFighter.GetMove(CurrentMoveId);
                    if (move.InvulnEndTick > 0 && MoveTick >= move.InvulnStartTick && MoveTick <= move.InvulnEndTick)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsParryActive
        {
            get
            {
                if (State == FighterStateKind.Parry || (State == FighterStateKind.Attack && CurrentMoveId == MoveId.ParryDeflect))
                {
                    var move = FighterCatalog.FirstFighter.GetMove(MoveId.ParryDeflect);
                    return MoveTick >= move.ParryStartTick && MoveTick <= move.ParryEndTick;
                }
                return false;
            }
        }

        public void ResetForMatch(int playerIndex, int startXMilli, bool facingRight, FighterDef def)
        {
            PlayerIndex = playerIndex;
            State = FighterStateKind.Idle;
            StateTimer = 0;
            X = Fix.FromMilli(startXMilli);
            Y = Fix.Zero;
            VX = Fix.Zero;
            VY = Fix.Zero;
            FacingRight = facingRight;
            Health = def.MaxHealth;
            Structure = def.MaxStructure;
            Meter = 0;
            Hitstop = 0;
            CurrentMoveId = MoveId.None;
            MoveTick = 0;
            MoveHitArmed = false;
            MoveConnected = false;
            MoveBlocked = false;
            ComboHits = 0;
            ComboDamage = 0;
            LastScalingPercent = 100;
            WillGroundBounce = false;
            UsedGroundBounce = false;
            LastFrameAdvantage = 0;
            LastOutcomeWasBlock = false;
            HasFrameAdvantageSample = false;
            InputBuffer.Clear();
            Array.Clear(FrameTimeline, 0, FrameMeterWindow);
            FrameTimelineCursor = 0;
        }

        public void CopyFrom(FighterState other)
        {
            PlayerIndex = other.PlayerIndex;
            State = other.State;
            StateTimer = other.StateTimer;
            X = other.X;
            Y = other.Y;
            VX = other.VX;
            VY = other.VY;
            FacingRight = other.FacingRight;
            Health = other.Health;
            Structure = other.Structure;
            Meter = other.Meter;
            Hitstop = other.Hitstop;
            CurrentMoveId = other.CurrentMoveId;
            MoveTick = other.MoveTick;
            MoveHitArmed = other.MoveHitArmed;
            MoveConnected = other.MoveConnected;
            MoveBlocked = other.MoveBlocked;
            ComboHits = other.ComboHits;
            ComboDamage = other.ComboDamage;
            LastScalingPercent = other.LastScalingPercent;
            WillGroundBounce = other.WillGroundBounce;
            UsedGroundBounce = other.UsedGroundBounce;
            LastFrameAdvantage = other.LastFrameAdvantage;
            LastOutcomeWasBlock = other.LastOutcomeWasBlock;
            HasFrameAdvantageSample = other.HasFrameAdvantageSample;
            InputBuffer.CopyFrom(other.InputBuffer);
            Array.Copy(other.FrameTimeline, FrameTimeline, FrameMeterWindow);
            FrameTimelineCursor = other.FrameTimelineCursor;
        }

        /// <summary>
        /// Populates active hurtboxes including the Part 4 Extended Limb Hurtbox (75% of active hitbox)
        /// during Active and early Recovery frames.
        /// </summary>
        public int GetCurrentHurtboxes(FighterDef def, Span<BoxDef> outBoxes)
        {
            BoxDef[] baseBoxes;
            if (IsAirborne)
            {
                baseBoxes = def.AirborneHurtboxes;
            }
            else if (IsCrouching)
            {
                baseBoxes = def.CrouchingHurtboxes;
            }
            else
            {
                baseBoxes = def.StandingHurtboxes;
            }

            int count = 0;
            for (int i = 0; i < baseBoxes.Length && count < outBoxes.Length; i++)
            {
                outBoxes[count++] = baseBoxes[i];
            }

            // Extended Limb Hurtbox: during Active and early Recovery (first 4 recovery ticks),
            // add a hurtbox covering 75% of the attack hitbox so whiffed limbs can be whiff-punished.
            if (State == FighterStateKind.Attack && CurrentMoveId != MoveId.None && count < outBoxes.Length)
            {
                var move = def.GetMove(CurrentMoveId);
                if (move.Hitbox.Width > 0 &&
                    MoveTick >= move.Startup &&
                    MoveTick < move.Startup + move.Active + 4)
                {
                    int extWidth = (move.Hitbox.Width * 75) / 100;
                    int extHeight = (move.Hitbox.Height * 75) / 100;
                    int extY = move.Hitbox.OffsetY + (move.Hitbox.Height - extHeight) / 2;
                    outBoxes[count++] = new BoxDef(move.Hitbox.OffsetX, extY, extWidth, extHeight);
                }
            }

            return count;
        }

        public bool TryGetActiveHitbox(FighterDef def, out BoxDef hitbox)
        {
            if (State == FighterStateKind.Attack && CurrentMoveId != MoveId.None)
            {
                var move = def.GetMove(CurrentMoveId);
                if (move.Hitbox.Width > 0 && move.IsActiveAtTick(MoveTick))
                {
                    hitbox = move.Hitbox;
                    return true;
                }
            }
            hitbox = default;
            return false;
        }

        public FrameTimelineCellKind ClassifyCurrentFrame(FighterDef def)
        {
            if (IsParryActive || IsInvulnerable)
            {
                return FrameTimelineCellKind.ParryOrInvuln;
            }
            switch (State)
            {
                case FighterStateKind.Hitstun:
                case FighterStateKind.Knockdown:
                    return FrameTimelineCellKind.Hitstun;
                case FighterStateKind.Blockstun:
                    return FrameTimelineCellKind.Blockstun;
                case FighterStateKind.Attack:
                case FighterStateKind.Parry:
                {
                    var move = def.GetMove(CurrentMoveId);
                    if (MoveTick < move.Startup) return FrameTimelineCellKind.Startup;
                    if (MoveTick < move.Startup + move.Active) return FrameTimelineCellKind.Active;
                    return FrameTimelineCellKind.Recovery;
                }
                case FighterStateKind.WalkFwd:
                case FighterStateKind.WalkBack:
                case FighterStateKind.DashFwd:
                case FighterStateKind.DashBack:
                case FighterStateKind.JumpStart:
                case FighterStateKind.Airborne:
                    return FrameTimelineCellKind.Movement;
                default:
                    return FrameTimelineCellKind.Neutral;
            }
        }

        public void RecordFrameTimelineCell(FighterDef def)
        {
            FrameTimeline[FrameTimelineCursor] = ClassifyCurrentFrame(def);
            FrameTimelineCursor = (FrameTimelineCursor + 1) % FrameMeterWindow;
        }
    }

    public sealed class MatchState
    {
        public const int ArenaHalfWidthMilli = 3200; // -3.2m .. +3.2m

        public int Tick;
        public uint RngState;
        public int SuperFlashTicks;
        public readonly FighterState[] Fighters = new[]
        {
            new FighterState(),
            new FighterState()
        };

        public MatchState(uint seed = 0x5E012026u)
        {
            Reset(seed);
        }

        public void Reset(uint seed = 0x5E012026u)
        {
            Tick = 0;
            RngState = seed == 0 ? 1u : seed;
            SuperFlashTicks = 0;
            var def = FighterCatalog.FirstFighter;
            Fighters[0].ResetForMatch(0, -450, true, def);
            Fighters[1].ResetForMatch(1, 450, false, def);
        }

        public uint NextRandom()
        {
            // Deterministic xorshift32
            uint x = RngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            RngState = x;
            return x;
        }

        public MatchState Clone()
        {
            var copy = new MatchState(RngState)
            {
                Tick = Tick,
                RngState = RngState,
                SuperFlashTicks = SuperFlashTicks
            };
            copy.Fighters[0].CopyFrom(Fighters[0]);
            copy.Fighters[1].CopyFrom(Fighters[1]);
            return copy;
        }

        public void CopyFrom(MatchState other)
        {
            Tick = other.Tick;
            RngState = other.RngState;
            SuperFlashTicks = other.SuperFlashTicks;
            Fighters[0].CopyFrom(other.Fighters[0]);
            Fighters[1].CopyFrom(other.Fighters[1]);
        }

        /// <summary>
        /// Deterministic 64-bit FNV-1a hash of the complete simulation state and input ring buffers.
        /// </summary>
        public ulong ComputeStateHash()
        {
            ulong hash = 14695981039346656037UL;

            static void MixInt(ref ulong h, int v)
            {
                unchecked
                {
                    uint u = (uint)v;
                    for (int i = 0; i < 4; i++)
                    {
                        h ^= (byte)(u & 0xFF);
                        h *= 1099511628211UL;
                        u >>= 8;
                    }
                }
            }

            MixInt(ref hash, Tick);
            MixInt(ref hash, (int)RngState);
            MixInt(ref hash, SuperFlashTicks);

            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                MixInt(ref hash, f.PlayerIndex);
                MixInt(ref hash, (int)f.State);
                MixInt(ref hash, f.StateTimer);
                MixInt(ref hash, f.X.Raw);
                MixInt(ref hash, f.Y.Raw);
                MixInt(ref hash, f.VX.Raw);
                MixInt(ref hash, f.VY.Raw);
                MixInt(ref hash, f.FacingRight ? 1 : 0);
                MixInt(ref hash, f.Health);
                MixInt(ref hash, f.Structure);
                MixInt(ref hash, f.Meter);
                MixInt(ref hash, f.Hitstop);
                MixInt(ref hash, (int)f.CurrentMoveId);
                MixInt(ref hash, f.MoveTick);
                MixInt(ref hash, f.MoveHitArmed ? 1 : 0);
                MixInt(ref hash, f.MoveConnected ? 1 : 0);
                MixInt(ref hash, f.MoveBlocked ? 1 : 0);
                MixInt(ref hash, f.ComboHits);
                MixInt(ref hash, f.ComboDamage);
                MixInt(ref hash, f.LastScalingPercent);
                MixInt(ref hash, f.WillGroundBounce ? 1 : 0);
                MixInt(ref hash, f.UsedGroundBounce ? 1 : 0);
                MixInt(ref hash, f.LastFrameAdvantage);
                MixInt(ref hash, f.LastOutcomeWasBlock ? 1 : 0);
                MixInt(ref hash, f.HasFrameAdvantageSample ? 1 : 0);
                MixInt(ref hash, f.InputBuffer.HeadIndex);
                MixInt(ref hash, f.InputBuffer.Count);
                for (int k = 0; k < InputRingBuffer.Capacity; k++)
                {
                    MixInt(ref hash, (int)f.InputBuffer.PressedHistory[k]);
                    MixInt(ref hash, (int)f.InputBuffer.JustPressedHistory[k]);
                }
            }

            return hash;
        }

        public byte[] SaveBinarySnapshot()
        {
            using var ms = new MemoryStream(512);
            using var bw = new BinaryWriter(ms);
            bw.Write(Tick);
            bw.Write(RngState);
            bw.Write(SuperFlashTicks);
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                bw.Write(f.PlayerIndex);
                bw.Write((byte)f.State);
                bw.Write(f.StateTimer);
                bw.Write(f.X.Raw);
                bw.Write(f.Y.Raw);
                bw.Write(f.VX.Raw);
                bw.Write(f.VY.Raw);
                bw.Write(f.FacingRight);
                bw.Write(f.Health);
                bw.Write(f.Structure);
                bw.Write(f.Meter);
                bw.Write(f.Hitstop);
                bw.Write((byte)f.CurrentMoveId);
                bw.Write(f.MoveTick);
                bw.Write(f.MoveHitArmed);
                bw.Write(f.MoveConnected);
                bw.Write(f.MoveBlocked);
                bw.Write(f.ComboHits);
                bw.Write(f.ComboDamage);
                bw.Write(f.LastScalingPercent);
                bw.Write(f.WillGroundBounce);
                bw.Write(f.UsedGroundBounce);
                bw.Write(f.LastFrameAdvantage);
                bw.Write(f.LastOutcomeWasBlock);
                bw.Write(f.HasFrameAdvantageSample);
                bw.Write(f.InputBuffer.HeadIndex);
                bw.Write(f.InputBuffer.Count);
                for (int k = 0; k < InputRingBuffer.Capacity; k++)
                {
                    bw.Write((ushort)f.InputBuffer.PressedHistory[k]);
                    bw.Write((ushort)f.InputBuffer.JustPressedHistory[k]);
                }
                bw.Write(f.FrameTimelineCursor);
                for (int k = 0; k < FighterState.FrameMeterWindow; k++)
                {
                    bw.Write((byte)f.FrameTimeline[k]);
                }
            }
            return ms.ToArray();
        }

        public void LoadBinarySnapshot(byte[] snapshot)
        {
            using var ms = new MemoryStream(snapshot);
            using var br = new BinaryReader(ms);
            Tick = br.ReadInt32();
            RngState = br.ReadUInt32();
            SuperFlashTicks = br.ReadInt32();
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                f.PlayerIndex = br.ReadInt32();
                f.State = (FighterStateKind)br.ReadByte();
                f.StateTimer = br.ReadInt32();
                f.X = Fix.FromRaw(br.ReadInt32());
                f.Y = Fix.FromRaw(br.ReadInt32());
                f.VX = Fix.FromRaw(br.ReadInt32());
                f.VY = Fix.FromRaw(br.ReadInt32());
                f.FacingRight = br.ReadBoolean();
                f.Health = br.ReadInt32();
                f.Structure = br.ReadInt32();
                f.Meter = br.ReadInt32();
                f.Hitstop = br.ReadInt32();
                f.CurrentMoveId = (MoveId)br.ReadByte();
                f.MoveTick = br.ReadInt32();
                f.MoveHitArmed = br.ReadBoolean();
                f.MoveConnected = br.ReadBoolean();
                f.MoveBlocked = br.ReadBoolean();
                f.ComboHits = br.ReadInt32();
                f.ComboDamage = br.ReadInt32();
                f.LastScalingPercent = br.ReadInt32();
                f.WillGroundBounce = br.ReadBoolean();
                f.UsedGroundBounce = br.ReadBoolean();
                f.LastFrameAdvantage = br.ReadInt32();
                f.LastOutcomeWasBlock = br.ReadBoolean();
                f.HasFrameAdvantageSample = br.ReadBoolean();
                f.InputBuffer.HeadIndex = br.ReadInt32();
                f.InputBuffer.Count = br.ReadInt32();
                for (int k = 0; k < InputRingBuffer.Capacity; k++)
                {
                    f.InputBuffer.PressedHistory[k] = (FighterButtons)br.ReadUInt16();
                    f.InputBuffer.JustPressedHistory[k] = (FighterButtons)br.ReadUInt16();
                }
                f.FrameTimelineCursor = br.ReadInt32();
                for (int k = 0; k < FighterState.FrameMeterWindow; k++)
                {
                    f.FrameTimeline[k] = (FrameTimelineCellKind)br.ReadByte();
                }
            }
        }
    }
}
