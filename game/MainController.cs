using System;
using System.Collections.Generic;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    /// <summary>
    /// Root scene controller coordinating the 60 Hz deterministic MatchSim,
    /// P1 & P2 FighterVisualNodes, Sloclap StudioArenaNode, F9 Turnaround & Move Inspector,
    /// and live Golden 11-Hit Showcase Combo playback (F4).
    /// </summary>
    public partial class MainController : Node3D
    {
        public MatchState SimState { get; private set; } = new MatchState();

        public StudioArenaNode Arena { get; private set; } = null!;
        public FighterVisualNode P1Visual { get; private set; } = null!;
        public FighterVisualNode P2Visual { get; private set; } = null!;
        public HudAndFrameStudio HudStudio { get; private set; } = null!;

        // Turnaround sheet mannequins for F9 Inspector Mode (FRONT, SIDE, BACK, GUARD, MOVE SCRUB)
        private readonly FighterVisualNode[] _turnaroundMannequins = new FighterVisualNode[3];
        private FighterState _inspectorPreviewState = new FighterState();

        private bool _showDebugBoxes = true;
        private bool _paused;
        private bool _stepSingleTickRequested;
        private DummyBehaviorMode _dummyMode = DummyBehaviorMode.Stand;

        private bool _turnaroundInspectorActive;
        private int _inspectorMoveIndex;
        private int _inspectorTick;

        // Live Golden Combo script queue (triggered via F4)
        private readonly Queue<FighterButtons> _goldenComboScriptQueue = new Queue<FighterButtons>();

        // Key edge tracking
        private bool _prevF1, _prevF2, _prevF3, _prevF4, _prevF8, _prevF9, _prevSpace, _prevR;
        private bool _prevLeft, _prevRight, _prevUp, _prevDown;
        private FighterMeshMode _globalMeshMode = FighterMeshMode.MeshyGlbWithGuardStatue;

        public override void _Ready()
        {
            Engine.MaxFps = 60;
            Engine.PhysicsTicksPerSecond = 60;

            Arena = new StudioArenaNode { Name = "StudioArena" };
            AddChild(Arena);

            P1Visual = new FighterVisualNode { Name = "P1FighterVisual" };
            P1Visual.Initialize(isPlayerTwoPalette: false);
            AddChild(P1Visual);

            P2Visual = new FighterVisualNode { Name = "P2FighterVisual" };
            P2Visual.Initialize(isPlayerTwoPalette: true);
            AddChild(P2Visual);

            for (int i = 0; i < 3; i++)
            {
                var m = new FighterVisualNode { Name = $"TurnaroundMannequin_{i}" };
                m.Initialize(isPlayerTwoPalette: false);
                m.Visible = false;
                AddChild(m);
                _turnaroundMannequins[i] = m;
            }

            var canvasLayer = new CanvasLayer { Name = "StudioHudCanvasLayer" };
            AddChild(canvasLayer);

            HudStudio = new HudAndFrameStudio { Name = "HudAndFrameStudio" };
            canvasLayer.AddChild(HudStudio);

            SimState.Reset();
            SimState.Fighters[0].Meter = 100; // Start with full Super meter so 236H+S can be tested immediately
            SyncPresentation(1f / 60f);
        }

        public override void _PhysicsProcess(double delta)
        {
            HandleStudioHotkeys();

            if (_turnaroundInspectorActive)
            {
                UpdateTurnaroundInspectorScene((float)delta);
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                _turnaroundMannequins[i].Visible = false;
            }
            P2Visual.Visible = true;

            if (!_paused || _stepSingleTickRequested)
            {
                _stepSingleTickRequested = false;

                FighterButtons p1Input = _goldenComboScriptQueue.Count > 0
                    ? _goldenComboScriptQueue.Dequeue()
                    : SamplePlayerOneKeyboard(SimState.Fighters[0].FacingRight);

                FighterButtons p2Input = ComputeDummyInput(SimState, _dummyMode);

                MatchSim.Step(SimState, p1Input, p2Input);

                // Auto-refill health when a fighter is KO'd in Studio training mode
                if (SimState.Fighters[0].Health <= 0 && SimState.Fighters[0].State == FighterStateKind.Idle)
                {
                    SimState.Fighters[0].Health = FighterCatalog.FirstFighter.MaxHealth;
                }
                if (SimState.Fighters[1].Health <= 0 && SimState.Fighters[1].State == FighterStateKind.Idle)
                {
                    SimState.Fighters[1].Health = FighterCatalog.FirstFighter.MaxHealth;
                }
            }

            SyncPresentation((float)delta);
        }

        private void SyncPresentation(float deltaSec)
        {
            P1Visual.CurrentMeshMode = _globalMeshMode;
            P2Visual.CurrentMeshMode = _globalMeshMode;
            P1Visual.SyncFromSimState(SimState.Fighters[0], SimState.Tick, _showDebugBoxes, deltaSec);
            P2Visual.SyncFromSimState(SimState.Fighters[1], SimState.Tick, _showDebugBoxes, deltaSec);
            Arena.UpdateCameraForMatch(SimState, _turnaroundInspectorActive, deltaSec);
            HudStudio.SyncHudState(
                SimState,
                _showDebugBoxes,
                _paused,
                _dummyMode,
                _turnaroundInspectorActive,
                _inspectorMoveIndex,
                _inspectorTick,
                _globalMeshMode);
        }

        private void UpdateTurnaroundInspectorScene(float deltaSec)
        {
            // Display 4-panel reference turnaround (FRONT, SIDE, BACK, GUARD) + 5th Move Inspector mannequin!
            for (int i = 0; i < 3; i++)
            {
                _turnaroundMannequins[i].Visible = true;
            }
            P2Visual.Visible = true;

            // 1. FRONT (facing camera +Z -> yaw 0 deg)
            _turnaroundMannequins[0].SyncForTurnaroundInspector(
                new Vector3(-1.55f, 0f, 0f),
                0f,
                FighterPoseEngine.TurnaroundNeutralPose,
                null,
                false);

            // 2. SIDE (facing right +X -> yaw +90 deg)
            _turnaroundMannequins[1].SyncForTurnaroundInspector(
                new Vector3(-0.78f, 0f, 0f),
                90f,
                FighterPoseEngine.TurnaroundNeutralPose,
                null,
                false);

            // 3. BACK (facing away -Z -> yaw 180 deg)
            _turnaroundMannequins[2].SyncForTurnaroundInspector(
                new Vector3(0.00f, 0f, 0f),
                180f,
                FighterPoseEngine.TurnaroundNeutralPose,
                null,
                false);

            // 4. GUARD (Panel 4 of reference_first_fighter.jpg)
            P1Visual.SyncForTurnaroundInspector(
                new Vector3(0.80f, 0f, 0f),
                32f,
                FighterPoseEngine.GuardPose,
                null,
                false);

            // 5. LIVE MOVE KEY-POSE SCRUBBER (with 3D Pushbox/Hurtbox/Hitbox wireframes)
            MoveId moveId = FighterCatalog.CoreInspectorMoveOrder[
                Math.Clamp(_inspectorMoveIndex, 0, FighterCatalog.CoreInspectorMoveOrder.Length - 1)];
            MoveDef moveDef = FighterCatalog.FirstFighter.GetMove(moveId);
            _inspectorTick = Math.Clamp(_inspectorTick, 0, Math.Max(0, moveDef.TotalTicks - 1));

            _inspectorPreviewState.ResetForMatch(0, 1650, true, FighterCatalog.FirstFighter);
            _inspectorPreviewState.State = moveId == MoveId.ParryDeflect ? FighterStateKind.Parry : FighterStateKind.Attack;
            _inspectorPreviewState.CurrentMoveId = moveId;
            _inspectorPreviewState.MoveTick = _inspectorTick;

            SkeletonPose attackPose = FighterPoseEngine.EvalAttackPose(
                moveId,
                _inspectorTick,
                inHitstop: false,
                moveConnected: false,
                moveBlocked: false);

            P2Visual.SyncForTurnaroundInspector(
                new Vector3(1.62f, moveDef.Stance == MoveStance.Air ? 0.38f : 0f, 0f),
                90f,
                attackPose,
                _inspectorPreviewState,
                _showDebugBoxes);

            Arena.UpdateCameraForMatch(SimState, true, deltaSec);
            HudStudio.SyncHudState(
                SimState,
                _showDebugBoxes,
                _paused,
                _dummyMode,
                _turnaroundInspectorActive,
                _inspectorMoveIndex,
                _inspectorTick,
                _globalMeshMode);
        }

        private void HandleStudioHotkeys()
        {
            bool f1 = Input.IsPhysicalKeyPressed(Key.F1);
            bool f2 = Input.IsPhysicalKeyPressed(Key.F2);
            bool f3 = Input.IsPhysicalKeyPressed(Key.F3);
            bool f4 = Input.IsPhysicalKeyPressed(Key.F4);
            bool f8 = Input.IsPhysicalKeyPressed(Key.F8);
            bool f9 = Input.IsPhysicalKeyPressed(Key.F9);
            bool space = Input.IsPhysicalKeyPressed(Key.Space);
            bool rKey = Input.IsPhysicalKeyPressed(Key.R);

            if (f1 && !_prevF1) _showDebugBoxes = !_showDebugBoxes;
            if (f2 && !_prevF2)
            {
                if (!_paused)
                {
                    _paused = true;
                }
                else
                {
                    _stepSingleTickRequested = true;
                }
            }
            if (space && !_prevSpace && _paused)
            {
                _paused = false;
            }
            if (f3 && !_prevF3)
            {
                _dummyMode = (DummyBehaviorMode)(((int)_dummyMode + 1) % 5);
            }
            if (f4 && !_prevF4)
            {
                EnqueueGoldenComboShowcase();
            }
            if (f8 && !_prevF8)
            {
                _globalMeshMode = (FighterMeshMode)(((int)_globalMeshMode + 1) % 3);
            }
            if (f9 && !_prevF9)
            {
                _turnaroundInspectorActive = !_turnaroundInspectorActive;
                _inspectorTick = 0;
            }
            if (rKey && !_prevR)
            {
                _goldenComboScriptQueue.Clear();
                SimState.Reset();
                SimState.Fighters[0].Meter = 100;
            }

            if (_turnaroundInspectorActive)
            {
                bool up = Input.IsPhysicalKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W);
                bool down = Input.IsPhysicalKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S);
                bool left = Input.IsPhysicalKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A);
                bool right = Input.IsPhysicalKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D);

                if (up && !_prevUp)
                {
                    _inspectorMoveIndex = (_inspectorMoveIndex - 1 + FighterCatalog.CoreInspectorMoveOrder.Length) %
                                          FighterCatalog.CoreInspectorMoveOrder.Length;
                    _inspectorTick = 0;
                }
                if (down && !_prevDown)
                {
                    _inspectorMoveIndex = (_inspectorMoveIndex + 1) % FighterCatalog.CoreInspectorMoveOrder.Length;
                    _inspectorTick = 0;
                }
                if (left && !_prevLeft)
                {
                    _inspectorTick = Math.Max(0, _inspectorTick - 1);
                }
                if (right && !_prevRight)
                {
                    MoveId moveId = FighterCatalog.CoreInspectorMoveOrder[_inspectorMoveIndex];
                    int maxTick = FighterCatalog.FirstFighter.GetMove(moveId).TotalTicks - 1;
                    _inspectorTick = Math.Min(maxTick, _inspectorTick + 1);
                }

                _prevUp = up;
                _prevDown = down;
                _prevLeft = left;
                _prevRight = right;
            }

            _prevF1 = f1;
            _prevF2 = f2;
            _prevF3 = f3;
            _prevF4 = f4;
            _prevF8 = f8;
            _prevF9 = f9;
            _prevSpace = space;
            _prevR = rKey;
        }

        private static FighterButtons SamplePlayerOneKeyboard(bool facingRight)
        {
            FighterButtons btns = FighterButtons.None;

            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
                btns |= FighterButtons.Up;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
                btns |= FighterButtons.Down;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
                btns |= FighterButtons.Left;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
                btns |= FighterButtons.Right;

            if (Input.IsPhysicalKeyPressed(Key.J))
                btns |= FighterButtons.L;
            if (Input.IsPhysicalKeyPressed(Key.K))
                btns |= FighterButtons.M;
            if (Input.IsPhysicalKeyPressed(Key.L))
                btns |= FighterButtons.H;
            if (Input.IsPhysicalKeyPressed(Key.U))
                btns |= FighterButtons.S;

            // Throw Shortcut (I = L+M)
            if (Input.IsPhysicalKeyPressed(Key.I))
                btns |= FighterButtons.L | FighterButtons.M;

            // Super Art Shortcut (O = H+S)
            if (Input.IsPhysicalKeyPressed(Key.O))
                btns |= FighterButtons.H | FighterButtons.S;

            return btns;
        }

        private static FighterButtons ComputeDummyInput(MatchState state, DummyBehaviorMode mode)
        {
            var p1 = state.Fighters[0];
            var p2 = state.Fighters[1];
            FighterButtons backBtn = p2.FacingRight ? FighterButtons.Left : FighterButtons.Right;
            FighterButtons fwdBtn = p2.FacingRight ? FighterButtons.Right : FighterButtons.Left;

            switch (mode)
            {
                case DummyBehaviorMode.Crouch:
                    return FighterButtons.Down;

                case DummyBehaviorMode.AutoBlock:
                {
                    if (p1.State == FighterStateKind.Attack)
                    {
                        var atk = FighterCatalog.FirstFighter.GetMove(p1.CurrentMoveId);
                        if (atk.Height == AttackHeight.Low)
                        {
                            return FighterButtons.Down | backBtn;
                        }
                        return backBtn;
                    }
                    return FighterButtons.None;
                }

                case DummyBehaviorMode.CounterJab:
                {
                    // Triggers 5L immediately on the first frame after recovering from Blockstun or Hitstun
                    if (p2.State == FighterStateKind.Blockstun && p2.StateTimer <= 1)
                    {
                        return FighterButtons.L;
                    }
                    return backBtn;
                }

                case DummyBehaviorMode.AiSparring:
                {
                    int dist = Math.Abs(p1.X.ToMilli() - p2.X.ToMilli());
                    int phase = state.Tick % 45;
                    if (dist > 780)
                    {
                        return fwdBtn;
                    }
                    if (p1.State == FighterStateKind.Attack && phase < 12)
                    {
                        return backBtn | FighterButtons.S; // Attempt Sifu Forearm Deflect Parry!
                    }
                    return phase switch
                    {
                        5 => FighterButtons.L,
                        14 => FighterButtons.Down | FighterButtons.M,
                        24 => FighterButtons.H,
                        _ => FighterButtons.None
                    };
                }

                default:
                    return FighterButtons.None;
            }
        }

        private void EnqueueGoldenComboShowcase()
        {
            _turnaroundInspectorActive = false;
            _paused = false;
            _dummyMode = DummyBehaviorMode.Stand;
            _goldenComboScriptQueue.Clear();

            SimState.Reset();
            SimState.Fighters[0].X = Fix.FromMilli(-210);
            SimState.Fighters[1].X = Fix.FromMilli(210);
            SimState.Fighters[0].Meter = 100;

            // Pre-compute the exact tick-by-tick input sequence for the 11-Hit Golden Combo
            // by dry-running on a clone of SimState!
            var simClone = SimState.Clone();

            void StepCloneAndRecord(FighterButtons input)
            {
                _goldenComboScriptQueue.Enqueue(input);
                MatchSim.Step(simClone, input, FighterButtons.None);
            }

            void WaitConnectAndHitstop(MoveId expected)
            {
                var f = simClone.Fighters[0];
                for (int i = 0; i < 45; i++)
                {
                    StepCloneAndRecord(FighterButtons.None);
                    if (f.CurrentMoveId == expected && f.MoveConnected && f.Hitstop == 0)
                    {
                        return;
                    }
                }
            }

            // 5L -> 2M -> 5H -> 5S -> Jump 9 -> j.L -> j.M -> j.H -> j.S (Ground Bounce) -> Land -> 623H -> 236H+S
            StepCloneAndRecord(FighterButtons.L);
            WaitConnectAndHitstop(MoveId.StandL);

            StepCloneAndRecord(FighterButtons.Down | FighterButtons.M);
            WaitConnectAndHitstop(MoveId.CrouchM);

            StepCloneAndRecord(FighterButtons.H);
            WaitConnectAndHitstop(MoveId.StandH);

            StepCloneAndRecord(FighterButtons.S);
            WaitConnectAndHitstop(MoveId.StandS);

            StepCloneAndRecord(FighterButtons.Up | FighterButtons.Right);
            for (int i = 0; i < 5; i++) StepCloneAndRecord(FighterButtons.Right);

            StepCloneAndRecord(FighterButtons.L);
            WaitConnectAndHitstop(MoveId.AirL);

            StepCloneAndRecord(FighterButtons.M);
            WaitConnectAndHitstop(MoveId.AirM);

            StepCloneAndRecord(FighterButtons.H);
            WaitConnectAndHitstop(MoveId.AirH);

            StepCloneAndRecord(FighterButtons.S);
            WaitConnectAndHitstop(MoveId.AirS);

            for (int i = 0; i < 35; i++)
            {
                StepCloneAndRecord(FighterButtons.None);
                if (simClone.Fighters[1].UsedGroundBounce &&
                    !simClone.Fighters[0].IsAirborne &&
                    simClone.Fighters[0].State == FighterStateKind.Idle)
                {
                    break;
                }
            }

            StepCloneAndRecord(FighterButtons.Right);
            StepCloneAndRecord(FighterButtons.Down);
            StepCloneAndRecord(FighterButtons.Down | FighterButtons.Right | FighterButtons.H);
            WaitConnectAndHitstop(MoveId.DpUpper);

            // Grant meter in live state as well by ensuring P1 starts with enough meter (or natural meter gain:
            // P1 started at 100 and gained meter on 9 hits, still at 100!)
            StepCloneAndRecord(FighterButtons.Down);
            StepCloneAndRecord(FighterButtons.Down | FighterButtons.Right);
            StepCloneAndRecord(FighterButtons.Right | FighterButtons.H | FighterButtons.S);
        }
    }
}
