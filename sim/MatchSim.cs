using System;

namespace FirstFighter.Sim
{
    /// <summary>
    /// 60 Hz deterministic simulation engine implementing Andrea Jens Parts 1-8:
    /// Input Latching -> FSM & Cancels -> Movement & Gravity -> Pushbox Separation ->
    /// Hitbox/Hurtbox/Guard/Parry Resolution -> Holy Trinity of Combo Scaling & Ground Bounce.
    /// </summary>
    public static class MatchSim
    {
        public static void Step(MatchState state, FighterButtons p1Input, FighterButtons p2Input)
        {
            var def = FighterCatalog.FirstFighter;
            state.Tick++;

            var f0 = state.Fighters[0];
            var f1 = state.Fighters[1];

            // 1. Latch once-per-tick inputs into 16-tick ring buffers (even during hitstop so motions buffer cleanly!)
            f0.InputBuffer.PushTick(p1Input);
            f1.InputBuffer.PushTick(p2Input);

            if (state.SuperFlashTicks > 0)
            {
                state.SuperFlashTicks--;
            }

            // 2. Update facing when grounded & neutral
            UpdateFacing(f0, f1);
            UpdateFacing(f1, f0);

            // 3. Process FSM transitions, cancels, and movement for each fighter
            StepFighterFsmAndMovement(state, f0, f1, def);
            StepFighterFsmAndMovement(state, f1, f0, def);

            // 4. Resolve Pushbox collision (prevents horizontal overlap)
            ResolvePushboxCollision(f0, f1, def);

            // 5. Resolve Attacks (Hitboxes vs Hurtboxes, Guard Matrix, Sifu Forearm Deflect Parry, Combo Scaling)
            ResolveCombat(state, f0, f1, def);

            // 6. Re-clamp arena bounds and pushboxes after knockback
            ResolvePushboxCollision(f0, f1, def);

            // 7. Record 60-tick Frame Meter cells
            f0.RecordFrameTimelineCell(def);
            f1.RecordFrameTimelineCell(def);
        }

        private static void UpdateFacing(FighterState self, FighterState opponent)
        {
            if (self.IsAirborne) return;
            if (self.State == FighterStateKind.Idle ||
                self.State == FighterStateKind.WalkFwd ||
                self.State == FighterStateKind.WalkBack ||
                self.State == FighterStateKind.Crouch)
            {
                if (opponent.X > self.X)
                {
                    self.FacingRight = true;
                }
                else if (opponent.X < self.X)
                {
                    self.FacingRight = false;
                }
            }
        }

        private static void StepFighterFsmAndMovement(MatchState match, FighterState f, FighterState opponent, FighterDef def)
        {
            // Hitstop freeze locks state & animation on the active frame
            if (f.Hitstop > 0)
            {
                f.Hitstop--;
                return;
            }

            int numpad = f.InputBuffer.GetNumpadDirectionAgo(0, f.FacingRight);
            bool holdUp = numpad == 7 || numpad == 8 || numpad == 9;
            bool holdDown = numpad == 1 || numpad == 2 || numpad == 3;
            bool holdFwd = numpad == 6 || numpad == 9 || numpad == 3;
            bool holdBack = numpad == 4 || numpad == 7 || numpad == 1;

            // Check candidate move from priority parser (including 2-tick kara cancels)
            MoveId candidateMove = f.InputBuffer.ParseCandidateMove(
                f.FacingRight,
                f.IsAirborne,
                f.Meter,
                f.State,
                f.CurrentMoveId,
                f.MoveTick);

            // Handle 2-tick Kara-Cancel Leniency during early startup (MoveTick <= 2)
            if (f.State == FighterStateKind.Attack && f.MoveTick <= 2 &&
                (candidateMove == MoveId.Throw || candidateMove == MoveId.SuperArt) &&
                candidateMove != f.CurrentMoveId)
            {
                StartMove(match, f, candidateMove, def);
                return;
            }

            // Handle On-Hit / On-Block Cancels (Gatlings, Jump Cancel on 5S, Special Cancel, Super Cancel)
            if (f.State == FighterStateKind.Attack && (f.MoveConnected || f.MoveBlocked))
            {
                var curMove = def.GetMove(f.CurrentMoveId);

                // Jump-Cancel on Hit (e.g., 5S -> Jump Forward 9)
                if (f.MoveConnected && curMove.Cancels.JumpCancelOnHit && holdUp && !f.IsAirborne)
                {
                    EnterJumpStart(f, numpad, def);
                    return;
                }

                if (candidateMove != MoveId.None && CanCancelCurrentMoveInto(f, curMove, candidateMove, def))
                {
                    StartMove(match, f, candidateMove, def);
                    return;
                }
            }

            // State-specific progression
            switch (f.State)
            {
                case FighterStateKind.Idle:
                case FighterStateKind.WalkFwd:
                case FighterStateKind.WalkBack:
                case FighterStateKind.Crouch:
                {
                    // Reset combo counter when opponent is no longer in hitstun/knockdown
                    if (f.ComboHits > 0)
                    {
                        f.ComboHits = 0;
                        f.ComboDamage = 0;
                        f.LastScalingPercent = 100;
                        f.WillGroundBounce = false;
                        f.UsedGroundBounce = false;
                    }

                    if (candidateMove != MoveId.None)
                    {
                        StartMove(match, f, candidateMove, def);
                        return;
                    }

                    if (holdUp)
                    {
                        EnterJumpStart(f, numpad, def);
                        return;
                    }

                    if (holdDown)
                    {
                        f.State = FighterStateKind.Crouch;
                        f.VX = Fix.Zero;
                    }
                    else if (holdFwd)
                    {
                        f.State = FighterStateKind.WalkFwd;
                        int sign = f.FacingRight ? 1 : -1;
                        f.X += Fix.FromMilli(sign * def.WalkForwardSpeedMu);
                    }
                    else if (holdBack)
                    {
                        f.State = FighterStateKind.WalkBack;
                        int sign = f.FacingRight ? -1 : 1;
                        f.X += Fix.FromMilli(sign * def.WalkBackSpeedMu);
                    }
                    else
                    {
                        f.State = FighterStateKind.Idle;
                        f.VX = Fix.Zero;
                    }
                    f.StateTimer++;
                    break;
                }

                case FighterStateKind.DashFwd:
                case FighterStateKind.DashBack:
                {
                    if (candidateMove != MoveId.None)
                    {
                        StartMove(match, f, candidateMove, def);
                        return;
                    }
                    int sign = (f.State == FighterStateKind.DashFwd)
                        ? (f.FacingRight ? 1 : -1)
                        : (f.FacingRight ? -1 : 1);
                    int speed = (f.State == FighterStateKind.DashFwd) ? def.DashForwardSpeedMu : def.DashBackSpeedMu;
                    f.X += Fix.FromMilli(sign * speed);
                    f.StateTimer--;
                    if (f.StateTimer <= 0)
                    {
                        f.State = FighterStateKind.Idle;
                    }
                    break;
                }

                case FighterStateKind.JumpStart:
                {
                    f.StateTimer--;
                    if (f.StateTimer <= 0)
                    {
                        f.State = FighterStateKind.Airborne;
                        f.Y = Fix.FromMilli(10); // Lift off ground
                        f.VY = Fix.FromMilli(def.JumpInitialVY);
                    }
                    break;
                }

                case FighterStateKind.Airborne:
                {
                    if (candidateMove != MoveId.None && IsAirMove(candidateMove))
                    {
                        StartMove(match, f, candidateMove, def);
                        IntegrateAirPhysics(f, def);
                        return;
                    }
                    IntegrateAirPhysics(f, def);
                    break;
                }

                case FighterStateKind.Attack:
                case FighterStateKind.Parry:
                {
                    var move = def.GetMove(f.CurrentMoveId);

                    // Apply forward micro-step velocity on configured ticks
                    if (move.StepForwardMuPerTick != 0 &&
                        f.MoveTick >= move.StepStartTick &&
                        f.MoveTick <= move.StepEndTick)
                    {
                        int sign = f.FacingRight ? 1 : -1;
                        f.X += Fix.FromMilli(sign * move.StepForwardMuPerTick);
                    }

                    f.MoveTick++;

                    // j.S (Axe-Heel Meteor Drop) drives the attacker downward on active frames for fast landing pickup
                    if (f.CurrentMoveId == MoveId.AirS && f.MoveTick == move.Startup && f.VY > Fix.FromMilli(-260))
                    {
                        f.VY = Fix.FromMilli(-260);
                    }

                    // Re-arm hitbox on multi-hit ticks (e.g., 236P at t=7, 10, 14 or Super at t=6, 12, 18, 24, 30, 36)
                    if (move.ShouldRearmHitAtTick(f.MoveTick))
                    {
                        f.MoveHitArmed = true;
                    }

                    if (f.IsAirborne)
                    {
                        IntegrateAirPhysics(f, def);
                        // Landing during an air attack cancels into Idle recovery
                        if (f.Y <= Fix.Zero)
                        {
                            f.Y = Fix.Zero;
                            f.VX = Fix.Zero;
                            f.VY = Fix.Zero;
                            f.State = FighterStateKind.Idle;
                            f.CurrentMoveId = MoveId.None;
                            f.MoveTick = 0;
                            break;
                        }
                    }

                    if (f.MoveTick >= move.TotalTicks)
                    {
                        f.CurrentMoveId = MoveId.None;
                        f.MoveTick = 0;
                        f.MoveHitArmed = false;
                        f.MoveConnected = false;
                        f.MoveBlocked = false;
                        f.State = f.IsAirborne ? FighterStateKind.Airborne : (holdDown ? FighterStateKind.Crouch : FighterStateKind.Idle);
                    }
                    break;
                }

                case FighterStateKind.Hitstun:
                {
                    if (f.IsAirborne)
                    {
                        IntegrateAirborneHitstunPhysics(f, def);
                    }
                    else
                    {
                        f.StateTimer--;
                        if (f.StateTimer <= 0)
                        {
                            f.State = holdDown ? FighterStateKind.Crouch : FighterStateKind.Idle;
                            f.ComboHits = 0;
                            f.ComboDamage = 0;
                            f.LastScalingPercent = 100;
                            f.WillGroundBounce = false;
                            f.UsedGroundBounce = false;
                        }
                    }
                    break;
                }

                case FighterStateKind.Blockstun:
                {
                    f.StateTimer--;
                    if (f.StateTimer <= 0)
                    {
                        f.State = holdDown ? FighterStateKind.Crouch : FighterStateKind.Idle;
                    }
                    break;
                }

                case FighterStateKind.Knockdown:
                {
                    if (f.IsAirborne)
                    {
                        IntegrateAirborneHitstunPhysics(f, def);
                    }
                    else
                    {
                        f.StateTimer--;
                        if (f.StateTimer <= 0)
                        {
                            f.State = FighterStateKind.Wakeup;
                            f.StateTimer = 8;
                        }
                    }
                    break;
                }

                case FighterStateKind.Wakeup:
                {
                    f.StateTimer--;
                    if (f.StateTimer <= 0)
                    {
                        f.State = FighterStateKind.Idle;
                        f.ComboHits = 0;
                        f.ComboDamage = 0;
                        f.LastScalingPercent = 100;
                        f.WillGroundBounce = false;
                        f.UsedGroundBounce = false;
                    }
                    break;
                }

                case FighterStateKind.ThrowTech:
                {
                    f.StateTimer--;
                    if (f.StateTimer <= 0)
                    {
                        f.State = FighterStateKind.Idle;
                    }
                    break;
                }
            }
        }

        private static void EnterJumpStart(FighterState f, int numpad, FighterDef def)
        {
            f.State = FighterStateKind.JumpStart;
            f.StateTimer = def.JumpStartTicks;
            f.CurrentMoveId = MoveId.None;
            f.MoveTick = 0;
            f.MoveHitArmed = false;

            int sign = f.FacingRight ? 1 : -1;
            if (numpad == 9 || numpad == 6)
            {
                f.VX = Fix.FromMilli(sign * def.JumpForwardVX);
            }
            else if (numpad == 7 || numpad == 4)
            {
                f.VX = Fix.FromMilli(sign * def.JumpBackVX);
            }
            else
            {
                f.VX = Fix.Zero;
            }
        }

        private static void IntegrateAirPhysics(FighterState f, FighterDef def)
        {
            // Velocity VX/VY is integrated at 1/10 milli-unit per tick (so VY=500 -> +50 mu/tick initial lift)
            f.X += f.VX / 10;
            f.Y += f.VY / 10;
            f.VY -= Fix.FromMilli(def.GravityPerTick);

            if (f.Y <= Fix.Zero)
            {
                f.Y = Fix.Zero;
                f.VX = Fix.Zero;
                f.VY = Fix.Zero;
                if (f.State == FighterStateKind.Airborne)
                {
                    f.State = FighterStateKind.Idle;
                }
            }
        }

        private static void IntegrateAirborneHitstunPhysics(FighterState f, FighterDef def)
        {
            f.X += f.VX / 10;
            f.Y += f.VY / 10;
            f.VY -= Fix.FromMilli(def.GravityPerTick);

            if (f.Y <= Fix.Zero)
            {
                f.Y = Fix.Zero;

                // Part 7: Once-Per-Combo Ground Bounce (e.g. j.S Axe-Heel Meteor Drop)
                if (f.WillGroundBounce && !f.UsedGroundBounce)
                {
                    f.VY = Fix.FromMilli(465);
                    f.VX = Fix.ScalePercent(f.VX, 50);
                    f.Y = Fix.FromMilli(10); // Lift off floor into bounce arc
                    f.State = FighterStateKind.Hitstun;
                    f.StateTimer = 30;
                    f.UsedGroundBounce = true;
                    f.WillGroundBounce = false;
                    return;
                }

                f.VX = Fix.Zero;
                f.VY = Fix.Zero;
                if (f.State == FighterStateKind.Knockdown)
                {
                    f.StateTimer = 18;
                }
                else
                {
                    f.State = FighterStateKind.Knockdown;
                    f.StateTimer = 14;
                }
            }
        }

        private static bool IsAirMove(MoveId id)
        {
            return id == MoveId.AirL || id == MoveId.AirM || id == MoveId.AirH || id == MoveId.AirS;
        }

        private static bool IsSpecialMove(MoveId id)
        {
            return id == MoveId.QcfRekka || id == MoveId.DpUpper || id == MoveId.ParryDeflect;
        }

        private static bool CanCancelCurrentMoveInto(FighterState f, MoveDef curMove, MoveId nextId, FighterDef def)
        {
            if (nextId == MoveId.SuperArt)
            {
                return curMove.Cancels.CancelIntoSuper && f.Meter >= 100;
            }
            if (IsSpecialMove(nextId))
            {
                return curMove.Cancels.CancelIntoSpecials;
            }
            return curMove.Cancels.CanCancelIntoNormal(nextId);
        }

        public static void StartMove(MatchState match, FighterState f, MoveId moveId, FighterDef def)
        {
            var move = def.GetMove(moveId);
            if (move.MeterCost > 0)
            {
                f.Meter = Math.Max(0, f.Meter - move.MeterCost);
            }
            if (moveId == MoveId.SuperArt)
            {
                match.SuperFlashTicks = 24;
            }

            f.State = moveId == MoveId.ParryDeflect ? FighterStateKind.Parry : FighterStateKind.Attack;
            f.CurrentMoveId = moveId;
            f.MoveTick = 0;
            f.MoveHitArmed = true;
            f.MoveConnected = false;
            f.MoveBlocked = false;

            // Apply tick 0 forward step if configured
            if (move.StepForwardMuPerTick != 0 && move.StepStartTick == 0)
            {
                int sign = f.FacingRight ? 1 : -1;
                f.X += Fix.FromMilli(sign * move.StepForwardMuPerTick);
            }
        }

        private static void ResolvePushboxCollision(FighterState f0, FighterState f1, FighterDef def)
        {
            // Clamp fighters to arena walls first
            int minWall = -MatchState.ArenaHalfWidthMilli;
            int maxWall = MatchState.ArenaHalfWidthMilli;
            f0.X = Fix.Clamp(f0.X, Fix.FromMilli(minWall), Fix.FromMilli(maxWall));
            f1.X = Fix.Clamp(f1.X, Fix.FromMilli(minWall), Fix.FromMilli(maxWall));

            var pb = def.Pushbox;
            pb.GetWorldBounds(f0.X.ToMilli(), f0.Y.ToMilli(), f0.FacingRight, out int aMinX, out int aMaxX, out int aMinY, out int aMaxY);
            pb.GetWorldBounds(f1.X.ToMilli(), f1.Y.ToMilli(), f1.FacingRight, out int bMinX, out int bMaxX, out int bMinY, out int bMaxY);

            if (aMinX < bMaxX && aMaxX > bMinX && aMinY < bMaxY && aMaxY > bMinY)
            {
                int overlap = Math.Min(aMaxX - bMinX, bMaxX - aMinX);
                if (overlap > 0)
                {
                    int half = (overlap + 1) / 2;
                    if (f0.X <= f1.X)
                    {
                        f0.X -= Fix.FromMilli(half);
                        f1.X += Fix.FromMilli(overlap - half);
                    }
                    else
                    {
                        f0.X += Fix.FromMilli(half);
                        f1.X -= Fix.FromMilli(overlap - half);
                    }
                }
            }

            f0.X = Fix.Clamp(f0.X, Fix.FromMilli(minWall), Fix.FromMilli(maxWall));
            f1.X = Fix.Clamp(f1.X, Fix.FromMilli(minWall), Fix.FromMilli(maxWall));
        }

        private static void ResolveCombat(MatchState state, FighterState f0, FighterState f1, FighterDef def)
        {
            bool f0HitsF1 = CheckAttackOverlap(f0, f1, def, out MoveDef move0);
            bool f1HitsF0 = CheckAttackOverlap(f1, f0, def, out MoveDef move1);

            if (f0HitsF1)
            {
                ApplyHitOrBlockOrParry(state, f0, f1, move0, def);
            }
            if (f1HitsF0 && f1.State == FighterStateKind.Attack)
            {
                ApplyHitOrBlockOrParry(state, f1, f0, move1, def);
            }
        }

        private static bool CheckAttackOverlap(FighterState attacker, FighterState defender, FighterDef def, out MoveDef move)
        {
            move = def.GetMove(attacker.CurrentMoveId);
            if (attacker.State != FighterStateKind.Attack ||
                attacker.CurrentMoveId == MoveId.None ||
                !attacker.MoveHitArmed ||
                attacker.Hitstop > 0)
            {
                return false;
            }

            if (!move.IsActiveAtTick(attacker.MoveTick) || move.Hitbox.Width <= 0)
            {
                return false;
            }

            if (defender.IsInvulnerable)
            {
                return false;
            }

            // Part 8: Standing High attack whiffs on crouching opponents!
            if (move.Height == AttackHeight.High && defender.IsCrouching)
            {
                return false;
            }

            // Throw only connects on grounded, non-airborne opponents
            if (move.Height == AttackHeight.Throw && defender.IsAirborne)
            {
                return false;
            }

            Span<BoxDef> defenderHurtboxes = stackalloc BoxDef[5];
            int hurtCount = defender.GetCurrentHurtboxes(def, defenderHurtboxes);

            int ax = attacker.X.ToMilli();
            int ay = attacker.Y.ToMilli();
            int dx = defender.X.ToMilli();
            int dy = defender.Y.ToMilli();

            for (int i = 0; i < hurtCount; i++)
            {
                if (BoxDef.OverlapsWorld(
                    move.Hitbox, ax, ay, attacker.FacingRight,
                    defenderHurtboxes[i], dx, dy, defender.FacingRight))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyHitOrBlockOrParry(
            MatchState state,
            FighterState attacker,
            FighterState defender,
            MoveDef move,
            FighterDef def)
        {
            attacker.MoveHitArmed = false;

            // 1. Check Sifu Forearm Deflect Parry (Part 8):
            // Active ticks 2..10. Parries High, Mid, Overhead strikes, negates all damage,
            // grants +35 meter, triggers 10 ticks of hitstop freeze, and auto-transitions defender into ParryCounter!
            if (defender.IsParryActive &&
                (move.Height == AttackHeight.High || move.Height == AttackHeight.Mid || move.Height == AttackHeight.Overhead))
            {
                defender.Meter = Math.Min(def.MaxMeter, defender.Meter + 35);
                attacker.Hitstop = 10;
                defender.Hitstop = 10;
                attacker.MoveBlocked = true;

                // Auto-transition defender into ParryCounter (instant launching palm strike (110, 460))
                defender.State = FighterStateKind.Attack;
                defender.CurrentMoveId = MoveId.ParryCounter;
                defender.MoveTick = 0;
                defender.MoveHitArmed = true;
                defender.MoveConnected = false;
                defender.MoveBlocked = false;
                return;
            }

            // 2. Check Guard Matrix (Part 8):
            // Stand Guard (4) blocks High, Mid, Overhead; loses to Low and Throw.
            // Crouch Guard (1) blocks Low, Mid; ducks High; loses to Overhead (6H) and Throw (L+M).
            if (CanDefenderBlock(defender, move))
            {
                attacker.MoveBlocked = true;
                attacker.Hitstop = move.Hitstop;
                defender.Hitstop = move.Hitstop;

                defender.State = FighterStateKind.Blockstun;
                defender.StateTimer = move.Blockstun;
                defender.Structure = Math.Min(def.MaxStructure, defender.Structure + move.StructureDamage);

                attacker.Meter = Math.Min(def.MaxMeter, attacker.Meter + move.MeterGainOnBlock);
                defender.Meter = Math.Min(def.MaxMeter, defender.Meter + (move.MeterGainOnBlock / 2));

                int pushSign = attacker.FacingRight ? 1 : -1;
                defender.X += Fix.FromMilli(pushSign * move.BlockPushbackXMu);

                int remainingAttackerTicks = (move.Startup + move.Active + move.Recovery) - attacker.MoveTick;
                attacker.LastFrameAdvantage = move.Blockstun - remainingAttackerTicks;
                attacker.LastOutcomeWasBlock = true;
                attacker.HasFrameAdvantageSample = true;

                defender.LastFrameAdvantage = -attacker.LastFrameAdvantage;
                defender.LastOutcomeWasBlock = true;
                defender.HasFrameAdvantageSample = true;
                return;
            }

            // 3. Clean Hit — Apply The Holy Trinity of Combo Scaling (Part 7):
            attacker.MoveConnected = true;
            int comboIndex = defender.ComboHits; // 0 on first hit, 1 on second hit, etc.
            defender.ComboHits++;

            // (A) Damage Scaling: 100%, 90%, 80%, 70%... (min 20%)
            int scalePct = Math.Max(20, 100 - comboIndex * 10);
            int scaledDamage = Math.Max(1, (move.Damage * scalePct) / 100);
            defender.LastScalingPercent = scalePct;
            defender.ComboDamage += scaledDamage;
            defender.Health = Math.Max(0, defender.Health - scaledDamage);
            defender.Structure = Math.Min(def.MaxStructure, defender.Structure + move.StructureDamage);

            attacker.Meter = Math.Min(def.MaxMeter, attacker.Meter + move.MeterGainOnHit);
            defender.Meter = Math.Min(def.MaxMeter, defender.Meter + (move.MeterGainOnHit / 3));

            attacker.Hitstop = move.Hitstop;
            defender.Hitstop = move.Hitstop;

            int remainingAttacker = (move.Startup + move.Active + move.Recovery) - attacker.MoveTick;
            attacker.LastFrameAdvantage = move.Hitstun - remainingAttacker;
            attacker.LastOutcomeWasBlock = false;
            attacker.HasFrameAdvantageSample = true;

            defender.LastFrameAdvantage = -attacker.LastFrameAdvantage;
            defender.LastOutcomeWasBlock = false;
            defender.HasFrameAdvantageSample = true;

            int dirSign = attacker.FacingRight ? 1 : -1;

            // (B) Hitstun & Air Gravity Decay + Launches + Ground Bounce:
            if (move.Launches)
            {
                defender.State = FighterStateKind.Hitstun;
                defender.StateTimer = move.Hitstun;
                defender.VX = Fix.FromMilli(dirSign * move.LaunchVX);
                defender.VY = Fix.FromMilli(move.LaunchVY);
                if (defender.Y <= Fix.Zero && move.LaunchVY > 0)
                {
                    defender.Y = Fix.FromMilli(20); // Lift target into airborne state immediately
                }
                if (move.GroundBounce && !defender.UsedGroundBounce)
                {
                    defender.WillGroundBounce = true;
                }
            }
            else if (defender.IsAirborne)
            {
                // Air normal pop decay: VY = Fix.FromMilli(Math.Max(60, 195 - ComboHits * 16))
                int popBase = move.AirPopVY > 0 ? move.AirPopVY : 195;
                int decayedPop = Math.Max(60, popBase - comboIndex * 16);
                defender.VY = Fix.FromMilli(decayedPop);
                defender.VX = Fix.FromMilli(dirSign * move.KnockbackXMu);
                defender.State = FighterStateKind.Hitstun;
                defender.StateTimer = Math.Max(10, move.Hitstun);
            }
            else if (move.CausesHardKnockdown)
            {
                defender.State = FighterStateKind.Knockdown;
                defender.StateTimer = 24;
                defender.X += Fix.FromMilli(dirSign * move.KnockbackXMu);
            }
            else
            {
                // (C) Combo Pushback Scaling (Anti-Infinite):
                // Multiply ground horizontal knockback by 100 + Math.Min(ComboHits, 8) * 15 percent
                int pushbackScalePct = 100 + Math.Min(comboIndex, 8) * 15;
                int scaledKnockbackX = (move.KnockbackXMu * pushbackScalePct) / 100;

                // If defender is near the corner wall, push attacker back by the remaining distance
                int oldDefX = defender.X.ToMilli();
                int targetDefX = Math.Clamp(oldDefX + dirSign * scaledKnockbackX, -MatchState.ArenaHalfWidthMilli, MatchState.ArenaHalfWidthMilli);
                int actualDefDelta = targetDefX - oldDefX;
                defender.X = Fix.FromMilli(targetDefX);

                int leftoverPush = (dirSign * scaledKnockbackX) - actualDefDelta;
                if (leftoverPush != 0)
                {
                    attacker.X -= Fix.FromMilli(leftoverPush);
                }

                // Ground Hitstun decay: reduce by Math.Max(0, comboIndex - 2) ticks
                int hitstunDecay = Math.Max(0, comboIndex - 2);
                defender.State = FighterStateKind.Hitstun;
                defender.StateTimer = Math.Max(4, move.Hitstun - hitstunDecay);
            }
        }

        private static bool CanDefenderBlock(FighterState defender, MoveDef move)
        {
            if (defender.IsAirborne) return false;
            if (move.Height == AttackHeight.Throw) return false;

            // Can only block from neutral/movement/crouch or while already in blockstun
            bool blockableState =
                defender.State == FighterStateKind.Idle ||
                defender.State == FighterStateKind.WalkBack ||
                defender.State == FighterStateKind.Crouch ||
                defender.State == FighterStateKind.Blockstun;

            if (!blockableState) return false;

            int numpad = defender.InputBuffer.GetNumpadDirectionAgo(0, defender.FacingRight);
            bool standGuard = numpad == 4 || numpad == 7;
            bool crouchGuard = numpad == 1;

            if (standGuard)
            {
                // Stand Guard (4) blocks High, Mid, Overhead; loses to Low and Throw
                return move.Height == AttackHeight.High ||
                       move.Height == AttackHeight.Mid ||
                       move.Height == AttackHeight.Overhead;
            }

            if (crouchGuard)
            {
                // Crouch Guard (1) blocks Low, Mid; loses to Overhead (6H) and Throw (L+M)
                return move.Height == AttackHeight.Low ||
                       move.Height == AttackHeight.Mid;
            }

            return false;
        }
    }
}
