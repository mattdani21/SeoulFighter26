using System;

namespace FirstFighter.Sim
{
    /// <summary>
    /// Code-first catalog of FIRST FIGHTER's complete 12-move urban Pak Mei / Jeet Kune Do martial-arts kit,
    /// hurtbox geometry, and frame data per Section 3.2 of the Master Brief.
    /// </summary>
    public static class FighterCatalog
    {
        public static readonly FighterDef FirstFighter = BuildFirstFighter();

        /// <summary>
        /// The 12 flagship moves in display order for the F9 Turnaround & Move Inspector.
        /// </summary>
        public static readonly MoveId[] CoreInspectorMoveOrder = new[]
        {
            MoveId.StandL,
            MoveId.CrouchL,
            MoveId.StandM,
            MoveId.CrouchM,
            MoveId.StandH,
            MoveId.ForwardH,
            MoveId.StandS,
            MoveId.AirL,
            MoveId.AirM,
            MoveId.AirH,
            MoveId.AirS,
            MoveId.QcfRekka,
            MoveId.DpUpper,
            MoveId.ParryDeflect,
            MoveId.SuperArt
        };

        private static FighterDef BuildFirstFighter()
        {
            // 3-Box Collision System (Part 4):
            // Pushbox: Width = 420 mu, Height = 1500 mu
            var pushbox = new BoxDef(-210, 0, 420, 1500);

            // Standing Hurtboxes:
            // Lower Body (440 x 940 mu), Upper Torso (460 x 620 mu at Y: 940..1560), Head (240 x 260 mu at Y: 1560..1820)
            var standingHurtboxes = new[]
            {
                new BoxDef(-220, 0, 440, 940),
                new BoxDef(-230, 940, 460, 620),
                new BoxDef(-120, 1560, 240, 260)
            };

            // Crouching Hurtboxes:
            // Total hurtbox height drops to Y: 0..780 mu so standing High attacks (Y >= 820 mu) cleanly whiff!
            var crouchingHurtboxes = new[]
            {
                new BoxDef(-240, 0, 480, 780)
            };

            // Airborne Hurtbox: compact tucked body box in air
            var airborneHurtboxes = new[]
            {
                new BoxDef(-220, 160, 440, 1280)
            };

            var moves = new[]
            {
                // 0: None placeholder
                new MoveDef
                {
                    Id = MoveId.None,
                    Code = "IDLE",
                    Name = "Guard Neutral",
                    InputNotation = "5",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 0,
                    Startup = 1,
                    Active = 0,
                    Recovery = 0
                },

                // 1: 5L — Snap Lead Jab (Stand / High, 4 / 2 / 7, Hit/Block 12 / 8 -> +3 / -1)
                // Hitbox Y: 840..1040 mu -> cleanly whiffs over crouching hurtbox (Y: 0..780 mu)!
                new MoveDef
                {
                    Id = MoveId.StandL,
                    Code = "5L",
                    Name = "Snap Lead Jab",
                    InputNotation = "L",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.High,
                    Priority = 1,
                    Startup = 4,
                    Active = 2,
                    Recovery = 7,
                    Damage = 40,
                    StructureDamage = 8,
                    MeterGainOnHit = 8,
                    MeterGainOnBlock = 4,
                    Hitstun = 12,
                    Blockstun = 8,
                    Hitstop = 6,
                    StepStartTick = 0,
                    StepEndTick = 3,
                    StepForwardMuPerTick = 26,
                    KnockbackXMu = 240,
                    BlockPushbackXMu = 190,
                    Hitbox = new BoxDef(140, 840, 460, 200), // X: 140..600, Y: 840..1040 mu
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.StandL, MoveId.CrouchL, MoveId.StandM, MoveId.CrouchM, MoveId.StandH, MoveId.ForwardH, MoveId.StandS }),
                    KeyPoseSummary = "Coiled guard -> razor-straight horizontal left fist at chin height (Y: 840..1040 mu)"
                },

                // 2: 2L — Low Lead Hook-Check (Crouch / Low, 5 / 2 / 8, Hit/Block 13 / 8 -> +3 / -2)
                new MoveDef
                {
                    Id = MoveId.CrouchL,
                    Code = "2L",
                    Name = "Low Lead Hook-Check",
                    InputNotation = "2 + L",
                    Stance = MoveStance.Crouch,
                    Height = AttackHeight.Low,
                    Priority = 1,
                    Startup = 5,
                    Active = 2,
                    Recovery = 8,
                    Damage = 45,
                    StructureDamage = 9,
                    MeterGainOnHit = 9,
                    MeterGainOnBlock = 4,
                    Hitstun = 13,
                    Blockstun = 8,
                    Hitstop = 6,
                    StepStartTick = 0,
                    StepEndTick = 4,
                    StepForwardMuPerTick = 20,
                    KnockbackXMu = 200,
                    BlockPushbackXMu = 170,
                    Hitbox = new BoxDef(160, 140, 560, 280),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.CrouchL, MoveId.StandM, MoveId.CrouchM, MoveId.StandH, MoveId.StandS }),
                    KeyPoseSummary = "Deep knee bend (Hips -0.40m) -> quick low oblique punch/shin check"
                },

                // 3: 5M — Side-Thrust Snap Kick (Stand / Mid, 6 / 3 / 12, Hit/Block 17 / 11 -> +2 / -4)
                new MoveDef
                {
                    Id = MoveId.StandM,
                    Code = "5M",
                    Name = "Side-Thrust Snap Kick",
                    InputNotation = "M",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 1,
                    Startup = 6,
                    Active = 3,
                    Recovery = 12,
                    Damage = 70,
                    StructureDamage = 14,
                    MeterGainOnHit = 12,
                    MeterGainOnBlock = 6,
                    Hitstun = 17,
                    Blockstun = 11,
                    Hitstop = 8,
                    StepStartTick = 0,
                    StepEndTick = 5,
                    StepForwardMuPerTick = 38,
                    KnockbackXMu = 210,
                    BlockPushbackXMu = 180,
                    Hitbox = new BoxDef(180, 620, 740, 340),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.CrouchM, MoveId.StandH, MoveId.ForwardH, MoveId.StandS }),
                    KeyPoseSummary = "High knee chamber -> full horizontal side-thrust kick showing bare ankle & loafer sole"
                },

                // 4: 2M — Tailored Low Sweep (Crouch / Low, 7 / 3 / 14, Hit/Block 19 / 11 -> +2 / -6)
                new MoveDef
                {
                    Id = MoveId.CrouchM,
                    Code = "2M",
                    Name = "Tailored Low Sweep",
                    InputNotation = "2 + M",
                    Stance = MoveStance.Crouch,
                    Height = AttackHeight.Low,
                    Priority = 1,
                    Startup = 7,
                    Active = 3,
                    Recovery = 14,
                    Damage = 75,
                    StructureDamage = 15,
                    MeterGainOnHit = 14,
                    MeterGainOnBlock = 7,
                    Hitstun = 19,
                    Blockstun = 11,
                    Hitstop = 8,
                    StepStartTick = 0,
                    StepEndTick = 6,
                    StepForwardMuPerTick = 34,
                    KnockbackXMu = 140,
                    BlockPushbackXMu = 160,
                    Hitbox = new BoxDef(160, 80, 780, 240), // Y: 80..320 mu
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.StandH, MoveId.ForwardH, MoveId.StandS }),
                    KeyPoseSummary = "Drops low (Hips -0.48m, torso leaned back -30 deg) -> whips long right leg across ankle height (Y: 80..320 mu)"
                },

                // 5: 5H — Step-In Rear Cross & Elbow (Stand / Mid, 8 / 3 / 16, Hit/Block 21 / 13 -> +2 / -6)
                new MoveDef
                {
                    Id = MoveId.StandH,
                    Code = "5H",
                    Name = "Step-In Rear Cross & Elbow",
                    InputNotation = "H",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 1,
                    Startup = 8,
                    Active = 3,
                    Recovery = 16,
                    Damage = 95,
                    StructureDamage = 20,
                    MeterGainOnHit = 16,
                    MeterGainOnBlock = 8,
                    Hitstun = 21,
                    Blockstun = 13,
                    Hitstop = 10,
                    StepStartTick = 1,
                    StepEndTick = 8,
                    StepForwardMuPerTick = 54,
                    KnockbackXMu = 150,
                    BlockPushbackXMu = 200,
                    Hitbox = new BoxDef(160, 680, 760, 420),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.ForwardH, MoveId.StandS }),
                    KeyPoseSummary = "Rear shoulder coils back -38 deg -> explosive rear straight cross (Torso +46 deg, UpperArmR +90 deg)"
                },

                // 6: 6H — Hopping Downward Elbow (Stand / Overhead, 16 / 3 / 14, Hit/Block 21 / 11 -> +4 / -6)
                new MoveDef
                {
                    Id = MoveId.ForwardH,
                    Code = "6H",
                    Name = "Hopping Downward Elbow",
                    InputNotation = "6 + H",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Overhead,
                    Priority = 2,
                    Startup = 16,
                    Active = 3,
                    Recovery = 14,
                    Damage = 90,
                    StructureDamage = 22,
                    MeterGainOnHit = 16,
                    MeterGainOnBlock = 8,
                    Hitstun = 21,
                    Blockstun = 11,
                    Hitstop = 10,
                    StepStartTick = 2,
                    StepEndTick = 15,
                    StepForwardMuPerTick = 46,
                    KnockbackXMu = 160,
                    BlockPushbackXMu = 180,
                    Hitbox = new BoxDef(140, 420, 700, 680), // Reaches down to crouching opponent
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "16f hop-step raising rear elbow high (UpperArmR -130 deg) -> crashing downward vertical elbow strike"
                },

                // 7: 5S — Rising Knee & Sky Uppercut (Stand / Mid Launcher, 7 / 3 / 17, Launches (125, 500), Jump-Cancelable)
                new MoveDef
                {
                    Id = MoveId.StandS,
                    Code = "5S",
                    Name = "Rising Knee & Sky Uppercut",
                    InputNotation = "S",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 1,
                    Startup = 7,
                    Active = 3,
                    Recovery = 17,
                    Damage = 85,
                    StructureDamage = 18,
                    MeterGainOnHit = 18,
                    MeterGainOnBlock = 8,
                    Hitstun = 38,
                    Blockstun = 12,
                    Hitstop = 10,
                    StepStartTick = 0,
                    StepEndTick = 7,
                    StepForwardMuPerTick = 40,
                    KnockbackXMu = 100,
                    BlockPushbackXMu = 180,
                    Launches = true,
                    LaunchVX = 125,
                    LaunchVY = 500,
                    Hitbox = new BoxDef(120, 500, 680, 960),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: true,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "Deep crouch load (Hips -0.34m) -> driving right knee + vertical right sky-uppercut (UpperArmR -162 deg)"
                },

                // 8: j.L — Air Snap Backfist (Air / Overhead, 4 / 3 / 9, Hit/Block 18 / 9, AirPop 195)
                new MoveDef
                {
                    Id = MoveId.AirL,
                    Code = "j.L",
                    Name = "Air Snap Backfist",
                    InputNotation = "Air L",
                    Stance = MoveStance.Air,
                    Height = AttackHeight.Overhead,
                    Priority = 1,
                    Startup = 4,
                    Active = 3,
                    Recovery = 9,
                    Damage = 45,
                    StructureDamage = 8,
                    MeterGainOnHit = 10,
                    MeterGainOnBlock = 5,
                    Hitstun = 24,
                    Blockstun = 9,
                    Hitstop = 6,
                    KnockbackXMu = 80,
                    BlockPushbackXMu = 120,
                    AirPopVY = 195,
                    Hitbox = new BoxDef(100, 320, 680, 680),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: false,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.AirM, MoveId.AirH, MoveId.AirS }),
                    KeyPoseSummary = "Crisp mid-air lead backfist as fighter rises; chains into j.M, j.H, j.S"
                },

                // 9: j.M — Air Roundhouse Kick (Air / Overhead, 5 / 3 / 11, Hit/Block 19 / 10, AirPop 195)
                new MoveDef
                {
                    Id = MoveId.AirM,
                    Code = "j.M",
                    Name = "Air Roundhouse Kick",
                    InputNotation = "Air M",
                    Stance = MoveStance.Air,
                    Height = AttackHeight.Overhead,
                    Priority = 1,
                    Startup = 5,
                    Active = 3,
                    Recovery = 11,
                    Damage = 65,
                    StructureDamage = 12,
                    MeterGainOnHit = 12,
                    MeterGainOnBlock = 6,
                    Hitstun = 26,
                    Blockstun = 10,
                    Hitstop = 7,
                    KnockbackXMu = 90,
                    BlockPushbackXMu = 140,
                    AirPopVY = 195,
                    Hitbox = new BoxDef(100, 260, 760, 720),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: false,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.AirH, MoveId.AirS }),
                    KeyPoseSummary = "High-chambered mid-air roundhouse whip kick (UpperLegR +86 deg); chains into j.H, j.S"
                },

                // 10: j.H — Air Spinning Hook Kick (Air / Overhead, 6 / 3 / 13, Hit/Block 21 / 11, AirPop 195)
                new MoveDef
                {
                    Id = MoveId.AirH,
                    Code = "j.H",
                    Name = "Air Spinning Hook Kick",
                    InputNotation = "Air H",
                    Stance = MoveStance.Air,
                    Height = AttackHeight.Overhead,
                    Priority = 1,
                    Startup = 6,
                    Active = 3,
                    Recovery = 13,
                    Damage = 80,
                    StructureDamage = 15,
                    MeterGainOnHit = 14,
                    MeterGainOnBlock = 7,
                    Hitstun = 28,
                    Blockstun = 11,
                    Hitstop = 8,
                    KnockbackXMu = 100,
                    BlockPushbackXMu = 150,
                    AirPopVY = 195,
                    Hitbox = new BoxDef(100, 220, 800, 760),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: true,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: false,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: new[] { MoveId.AirS }),
                    KeyPoseSummary = "Full torso-twist aerial hook kick; chains into j.S"
                },

                // 11: j.S — Axe-Heel Meteor Drop (Air / Overhead, 6 / 4 / 16, Knock (90, -680) Ground Bounce!)
                new MoveDef
                {
                    Id = MoveId.AirS,
                    Code = "j.S",
                    Name = "Axe-Heel Meteor Drop",
                    InputNotation = "Air S",
                    Stance = MoveStance.Air,
                    Height = AttackHeight.Overhead,
                    Priority = 1,
                    Startup = 6,
                    Active = 4,
                    Recovery = 16,
                    Damage = 90,
                    StructureDamage = 20,
                    MeterGainOnHit = 18,
                    MeterGainOnBlock = 8,
                    Hitstun = 35,
                    Blockstun = 12,
                    Hitstop = 10,
                    Launches = true,
                    LaunchVX = 90,
                    LaunchVY = -680,
                    GroundBounce = true,
                    Hitbox = new BoxDef(80, 60, 820, 920),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: false,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "High knee tuck -> violent downward axe-heel stomp that slams target into floor and ground-bounces (VY = +465)!"
                },

                // 12: 236L/M/H — Pak Mei Chain Flurry (3-Hit Rekka Burst, 7 / 9 / 16, 3 hits at t=7, 10, 14)
                new MoveDef
                {
                    Id = MoveId.QcfRekka,
                    Code = "236P",
                    Name = "Pak Mei Chain Flurry",
                    InputNotation = "236 + L/M/H",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 3,
                    Startup = 7,
                    Active = 9,
                    Recovery = 16,
                    Damage = 45, // per hit (3 hits = 135 base)
                    StructureDamage = 10,
                    MeterGainOnHit = 8,
                    MeterGainOnBlock = 4,
                    Hitstun = 20,
                    Blockstun = 12,
                    Hitstop = 5,
                    StepStartTick = 0,
                    StepEndTick = 12,
                    StepForwardMuPerTick = 72,
                    KnockbackXMu = 110,
                    BlockPushbackXMu = 120,
                    MultiHitTicks = new[] { 7, 10, 14 },
                    Hitbox = new BoxDef(140, 620, 760, 520),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "Signature Sifu advancing 3-punch vertical-fist & palm-heel blitz. Super-Cancelable on Hit 3!"
                },

                // 13: 623H — Dragon Rising Uppercut (Stand / Mid, 6 / 5 / 22, Invuln 1..7, Launch (95, 420))
                new MoveDef
                {
                    Id = MoveId.DpUpper,
                    Code = "623H",
                    Name = "Dragon Rising Uppercut",
                    InputNotation = "623 + H",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 4,
                    Startup = 6,
                    Active = 5,
                    Recovery = 22,
                    Damage = 110,
                    StructureDamage = 24,
                    MeterGainOnHit = 20,
                    MeterGainOnBlock = 8,
                    Hitstun = 36,
                    Blockstun = 12,
                    Hitstop = 10,
                    StepStartTick = 0,
                    StepEndTick = 8,
                    StepForwardMuPerTick = 58,
                    Launches = true,
                    LaunchVX = 95,
                    LaunchVY = 420,
                    InvulnStartTick = 1,
                    InvulnEndTick = 7,
                    Hitbox = new BoxDef(100, 180, 760, 1280),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "Invincible anti-air & post-ground-bounce pickup. Super-Cancelable!"
                },

                // 14: 214S — Intercept Forearm Deflect (Parry, 2 / 8 / 18, Active ticks 2..10)
                new MoveDef
                {
                    Id = MoveId.ParryDeflect,
                    Code = "214S",
                    Name = "Intercept Forearm Deflect",
                    InputNotation = "214 + S / 4 + S",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Parry,
                    Priority = 3,
                    Startup = 2,
                    Active = 8,
                    Recovery = 18,
                    ParryStartTick = 2,
                    ParryEndTick = 10,
                    KeyPoseSummary = "Tucks lead forearm across centerline (t=2..10); if struck, freezes 10 ticks and auto-launches!"
                },

                // 15: ParryCounter — Auto-triggered launching throat-palm counter after Parry!
                new MoveDef
                {
                    Id = MoveId.ParryCounter,
                    Code = "PARRY_CTR",
                    Name = "Deflect Throat-Palm Launch",
                    InputNotation = "(Auto after Parry)",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 4,
                    Startup = 1,
                    Active = 3,
                    Recovery = 12,
                    Damage = 95,
                    StructureDamage = 25,
                    MeterGainOnHit = 15,
                    Hitstun = 38,
                    Blockstun = 14,
                    Hitstop = 8,
                    Launches = true,
                    LaunchVX = 110,
                    LaunchVY = 460,
                    Hitbox = new BoxDef(80, 500, 820, 800),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: true,
                        cancelIntoSuper: true,
                        jumpCancelOnHit: true,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "Explosive throat-palm strike that launches the parried attacker airborne (110, 460)!"
                },

                // 16: 236H+S — SUPER: FIVE-POINT STRUCTURE BREAK (Stand / Mid, 6 / 36 / 20, 100 Meter, 6-Hit Cinema)
                new MoveDef
                {
                    Id = MoveId.SuperArt,
                    Code = "236H+S",
                    Name = "SUPER: FIVE-POINT STRUCTURE BREAK",
                    InputNotation = "236 + H+S",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Mid,
                    Priority = 5,
                    Startup = 6,
                    Active = 36,
                    Recovery = 20,
                    Damage = 45, // 6 hits * 45 = 270 base damage
                    StructureDamage = 12,
                    MeterCost = 100,
                    Hitstun = 40,
                    Blockstun = 18,
                    Hitstop = 5,
                    StepStartTick = 0,
                    StepEndTick = 18,
                    StepForwardMuPerTick = 85,
                    Launches = true,
                    LaunchVX = 80,
                    LaunchVY = 260,
                    CausesHardKnockdown = true,
                    InvulnStartTick = 1,
                    InvulnEndTick = 10,
                    MultiHitTicks = new[] { 6, 12, 18, 24, 30, 36 },
                    Hitbox = new BoxDef(100, 180, 920, 1320),
                    Cancels = new CancelSpec(
                        cancelIntoTargetNormals: false,
                        cancelIntoSpecials: false,
                        cancelIntoSuper: false,
                        jumpCancelOnHit: false,
                        allowedTargetNormals: Array.Empty<MoveId>()),
                    KeyPoseSummary = "24-tick studio camera zoom & spotlight dim -> 5 rapid vital-point strikes -> final twin-palm shockwave launch!"
                },

                // 17: L+M — Clinch Takedown Throw (Stand / Throw, 5 / 2 / 15)
                new MoveDef
                {
                    Id = MoveId.Throw,
                    Code = "L+M",
                    Name = "Clinch Takedown Throw",
                    InputNotation = "L + M",
                    Stance = MoveStance.Stand,
                    Height = AttackHeight.Throw,
                    Priority = 2,
                    Startup = 5,
                    Active = 2,
                    Recovery = 15,
                    Damage = 110,
                    StructureDamage = 25,
                    MeterGainOnHit = 15,
                    Hitstun = 30,
                    Hitstop = 10,
                    KnockbackXMu = 320,
                    CausesHardKnockdown = true,
                    Hitbox = new BoxDef(60, 400, 520, 1000),
                    KeyPoseSummary = "Pak Mei collar-clinch trip and downward shoulder takedown (unblockable)"
                }
            };

            return new FighterDef
            {
                Id = "first_fighter",
                DisplayName = "FIRST FIGHTER",
                MaxHealth = 1000,
                MaxStructure = 100,
                MaxMeter = 100,
                Pushbox = pushbox,
                StandingHurtboxes = standingHurtboxes,
                CrouchingHurtboxes = crouchingHurtboxes,
                AirborneHurtboxes = airborneHurtboxes,
                Moves = moves
            };
        }
    }
}
