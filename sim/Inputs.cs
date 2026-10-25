using System;

namespace FirstFighter.Sim
{
    /// <summary>
    /// 8 Virtual Buttons per Andrea Jens Part 6: Up, Down, Left, Right, L, M, H, S.
    /// </summary>
    [Flags]
    public enum FighterButtons : ushort
    {
        None  = 0,
        Up    = 1 << 0,
        Down  = 1 << 1,
        Left  = 1 << 2,
        Right = 1 << 3,
        L     = 1 << 4,
        M     = 1 << 5,
        H     = 1 << 6,
        S     = 1 << 7,

        ThrowChord = L | M,
        SuperChord = H | S
    }

    /// <summary>
    /// Fixed 16-tick circular input ring buffer with strict motion priority parser
    /// (Super [P5] > DP [P4] > QCF/QCB [P3] > Command Normal [P2] > Normal [P1])
    /// and 2-tick kara-cancel leniency.
    /// </summary>
    public sealed class InputRingBuffer
    {
        public const int Capacity = 16;

        public readonly FighterButtons[] PressedHistory = new FighterButtons[Capacity];
        public readonly FighterButtons[] JustPressedHistory = new FighterButtons[Capacity];
        public int HeadIndex;
        public int Count;

        public void Clear()
        {
            Array.Clear(PressedHistory, 0, Capacity);
            Array.Clear(JustPressedHistory, 0, Capacity);
            HeadIndex = 0;
            Count = 0;
        }

        public void CopyFrom(InputRingBuffer other)
        {
            Array.Copy(other.PressedHistory, PressedHistory, Capacity);
            Array.Copy(other.JustPressedHistory, JustPressedHistory, Capacity);
            HeadIndex = other.HeadIndex;
            Count = other.Count;
        }

        public void PushTick(FighterButtons currentPressed)
        {
            FighterButtons prevPressed = Count > 0 ? GetPressedAgo(0) : FighterButtons.None;
            FighterButtons justPressed = currentPressed & ~prevPressed;

            HeadIndex = (HeadIndex + 1) & (Capacity - 1);
            PressedHistory[HeadIndex] = currentPressed;
            JustPressedHistory[HeadIndex] = justPressed;
            if (Count < Capacity)
            {
                Count++;
            }
        }

        public FighterButtons GetPressedAgo(int ticksAgo)
        {
            if (ticksAgo < 0 || ticksAgo >= Count)
            {
                return FighterButtons.None;
            }
            int idx = (HeadIndex - ticksAgo + Capacity) & (Capacity - 1);
            return PressedHistory[idx];
        }

        public FighterButtons GetJustPressedAgo(int ticksAgo)
        {
            if (ticksAgo < 0 || ticksAgo >= Count)
            {
                return FighterButtons.None;
            }
            int idx = (HeadIndex - ticksAgo + Capacity) & (Capacity - 1);
            return JustPressedHistory[idx];
        }

        /// <summary>
        /// Converts raw directional buttons at ticksAgo into relative fighting-game numpad notation (1..9).
        /// </summary>
        public int GetNumpadDirectionAgo(int ticksAgo, bool facingRight)
        {
            return ToNumpadDirection(GetPressedAgo(ticksAgo), facingRight);
        }

        public static int ToNumpadDirection(FighterButtons buttons, bool facingRight)
        {
            bool up = (buttons & FighterButtons.Up) != 0 && (buttons & FighterButtons.Down) == 0;
            bool down = (buttons & FighterButtons.Down) != 0 && (buttons & FighterButtons.Up) == 0;

            bool rawLeft = (buttons & FighterButtons.Left) != 0 && (buttons & FighterButtons.Right) == 0;
            bool rawRight = (buttons & FighterButtons.Right) != 0 && (buttons & FighterButtons.Left) == 0;

            bool forward = facingRight ? rawRight : rawLeft;
            bool back = facingRight ? rawLeft : rawRight;

            if (down)
            {
                if (back) return 1;
                if (forward) return 3;
                return 2;
            }
            if (up)
            {
                if (back) return 7;
                if (forward) return 9;
                return 8;
            }
            if (back) return 4;
            if (forward) return 6;
            return 5;
        }

        public bool WasButtonJustPressedWithin(FighterButtons button, int windowTicks)
        {
            int limit = Math.Min(Count, windowTicks);
            for (int i = 0; i < limit; i++)
            {
                if ((GetJustPressedAgo(i) & button) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if buttonA and buttonB were pressed together or within 2-tick kara window.
        /// </summary>
        public bool HasKaraChordJustPressed(FighterButtons buttonA, FighterButtons buttonB, int karaWindowTicks = 2)
        {
            FighterButtons jpNow = GetJustPressedAgo(0);
            bool aNow = (jpNow & buttonA) != 0;
            bool bNow = (jpNow & buttonB) != 0;
            if (aNow && bNow)
            {
                return true;
            }

            if (aNow && WasButtonJustPressedWithin(buttonB, karaWindowTicks + 1))
            {
                return true;
            }
            if (bNow && WasButtonJustPressedWithin(buttonA, karaWindowTicks + 1))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Detects Dragon Punch motion (6 -> 2 -> 3, including 6 -> 2 -> 3 -> 6) within windowTicks.
        /// </summary>
        public bool HasDpMotion(bool facingRight, int windowTicks = 12)
        {
            int limit = Math.Min(Count, windowTicks);
            int stage = 0; // 0: need 6, 1: need 2, 2: need 3
            for (int age = limit - 1; age >= 0; age--)
            {
                int dir = GetNumpadDirectionAgo(age, facingRight);
                if (stage == 0)
                {
                    if (dir == 6) stage = 1;
                }
                else if (stage == 1)
                {
                    if (dir == 2) stage = 2;
                }
                else if (stage == 2)
                {
                    if (dir == 3) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Detects Quarter-Circle Forward motion (2 -> 3 -> 6) within windowTicks.
        /// </summary>
        public bool HasQcfMotion(bool facingRight, int windowTicks = 12)
        {
            int limit = Math.Min(Count, windowTicks);
            int stage = 0; // 0: need 2, 1: need 3, 2: need 6
            for (int age = limit - 1; age >= 0; age--)
            {
                int dir = GetNumpadDirectionAgo(age, facingRight);
                if (stage == 0)
                {
                    if (dir == 2) stage = 1;
                }
                else if (stage == 1)
                {
                    if (dir == 3) stage = 2;
                }
                else if (stage == 2)
                {
                    if (dir == 6) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Detects Quarter-Circle Back motion (2 -> 1 -> 4) within windowTicks.
        /// </summary>
        public bool HasQcbMotion(bool facingRight, int windowTicks = 12)
        {
            int limit = Math.Min(Count, windowTicks);
            int stage = 0; // 0: need 2, 1: need 1, 2: need 4
            for (int age = limit - 1; age >= 0; age--)
            {
                int dir = GetNumpadDirectionAgo(age, facingRight);
                if (stage == 0)
                {
                    if (dir == 2) stage = 1;
                }
                else if (stage == 1)
                {
                    if (dir == 1) stage = 2;
                }
                else if (stage == 2)
                {
                    if (dir == 4) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Strict Priority Motion Parser per Part 6:
        /// Priority 5: Super (236 + H+S, 15-tick window, 100 meter)
        /// Priority 4: Dragon Punch (623 + H, 12-tick window)
        /// Priority 3: Quarter-Circle Forward / Back (236 + L/M/H, 214 + S, 12-tick window)
        /// Priority 2: Command Normals (6 + H Overhead, 4 + S Parry, L + M Throw)
        /// Priority 1: Standard Normals (5L, 2L, 5M, 2M, 5H, 5S, j.L, j.M, j.H, j.S)
        /// </summary>
        public MoveId ParseCandidateMove(
            bool facingRight,
            bool airborne,
            int meter,
            FighterStateKind currentState,
            MoveId currentMoveId,
            int currentMoveAge,
            int bufferLeniencyTicks = 4)
        {
            int dirNow = GetNumpadDirectionAgo(0, facingRight);
            bool downHeld = dirNow == 1 || dirNow == 2 || dirNow == 3;
            bool forwardHeld = dirNow == 6 || dirNow == 3;
            bool backHeld = dirNow == 4 || dirNow == 1;

            bool lTrigger = WasButtonJustPressedWithin(FighterButtons.L, bufferLeniencyTicks);
            bool mTrigger = WasButtonJustPressedWithin(FighterButtons.M, bufferLeniencyTicks);
            bool hTrigger = WasButtonJustPressedWithin(FighterButtons.H, bufferLeniencyTicks);
            bool sTrigger = WasButtonJustPressedWithin(FighterButtons.S, bufferLeniencyTicks);

            // Check 2-tick Kara-Cancel Leniency during early startup (MoveAge <= 2)
            if (currentState == FighterStateKind.Attack && currentMoveAge <= 2)
            {
                FighterButtons jpNow = GetJustPressedAgo(0);
                // Kara Super: started 5H, 5S, or 236H after 236 motion, and pressed partner H/S within 2 ticks
                if (meter >= 100 && HasQcfMotion(facingRight, 15))
                {
                    if (((currentMoveId == MoveId.StandH || currentMoveId == MoveId.ForwardH || currentMoveId == MoveId.QcfRekka || currentMoveId == MoveId.DpUpper) && (jpNow & FighterButtons.S) != 0) ||
                        ((currentMoveId == MoveId.StandS || currentMoveId == MoveId.ParryDeflect) && (jpNow & FighterButtons.H) != 0) ||
                        HasKaraChordJustPressed(FighterButtons.H, FighterButtons.S, 2))
                    {
                        return MoveId.SuperArt;
                    }
                }

                // Kara Throw: started 5L or 5M on tick T and pressed partner M or L on tick T+1 or T+2
                if (!airborne)
                {
                    if ((currentMoveId == MoveId.StandL && (jpNow & FighterButtons.M) != 0) ||
                        (currentMoveId == MoveId.StandM && (jpNow & FighterButtons.L) != 0) ||
                        HasKaraChordJustPressed(FighterButtons.L, FighterButtons.M, 2))
                    {
                        return MoveId.Throw;
                    }
                }
            }

            if (airborne)
            {
                if (sTrigger) return MoveId.AirS;
                if (hTrigger) return MoveId.AirH;
                if (mTrigger) return MoveId.AirM;
                if (lTrigger) return MoveId.AirL;
                return MoveId.None;
            }

            // Priority 5: Super (236 + H+S, 15-tick window, 100 meter)
            if (meter >= 100 && HasQcfMotion(facingRight, 15) &&
                (HasKaraChordJustPressed(FighterButtons.H, FighterButtons.S, 2) ||
                 (hTrigger && sTrigger)))
            {
                return MoveId.SuperArt;
            }

            // Priority 4: Dragon Punch (623 + H, 12-tick window) — strictly beats QCF (Priority 3)!
            if (hTrigger && HasDpMotion(facingRight, 12))
            {
                return MoveId.DpUpper;
            }

            // Priority 3: Quarter-Circle Forward / Back (236 + L/M/H, 214 + S, 12-tick window)
            if ((lTrigger || mTrigger || hTrigger) && HasQcfMotion(facingRight, 12))
            {
                return MoveId.QcfRekka;
            }
            if (sTrigger && HasQcbMotion(facingRight, 12))
            {
                return MoveId.ParryDeflect;
            }

            // Priority 2: Command Normals & Chords (L+M Throw, 6+H Overhead, 4+S Parry)
            if (HasKaraChordJustPressed(FighterButtons.L, FighterButtons.M, 2))
            {
                return MoveId.Throw;
            }
            if (hTrigger && forwardHeld && !downHeld)
            {
                return MoveId.ForwardH;
            }
            if (sTrigger && backHeld && !downHeld)
            {
                return MoveId.ParryDeflect;
            }

            // Priority 1: Standard Normals (5L, 2L, 5M, 2M, 5H, 5S)
            if (sTrigger) return MoveId.StandS;
            if (hTrigger) return MoveId.StandH;
            if (mTrigger) return downHeld ? MoveId.CrouchM : MoveId.StandM;
            if (lTrigger) return downHeld ? MoveId.CrouchL : MoveId.StandL;

            return MoveId.None;
        }
    }
}
