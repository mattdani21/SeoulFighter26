using System;
using Godot;
using FirstFighter.Sim;

namespace FirstFighter.Game
{
    public enum DummyBehaviorMode : int
    {
        Stand = 0,
        Crouch = 1,
        AutoBlock = 2,
        CounterJab = 3,
        AiSparring = 4
    }

    public enum MatchPhase : int
    {
        RoundIntro = 0,
        ActiveMatch = 1,
        KoFinish = 2
    }

    /// <summary>
    /// AAA match HUD: angled health bars with delayed damage trail, structure / meter,
    /// round timer + win pips, and Round/FIGHT/K.O. callouts.
    /// F6 training overlay adds the 60-tick frame meter and hotkey legend.
    /// </summary>
    public partial class HudAndFrameStudio : Control
    {
        private MatchState? _matchState;
        private bool _showDebugBoxes;
        private bool _paused;
        private DummyBehaviorMode _dummyMode = DummyBehaviorMode.Stand;
        private bool _inspectorActive;
        private int _inspectorMoveIndex;
        private int _inspectorTick;
        private FighterMeshMode _meshMode = FighterMeshMode.ProceduralRig;
        private bool _trainingOverlay;

        private MatchPhase _phase = MatchPhase.ActiveMatch;
        private int _roundNumber = 1;
        private int _timerSeconds = 99;
        private int _p1Wins;
        private int _p2Wins;
        private int _phaseTick;
        private float _p1TrailHp = 1000f;
        private float _p2TrailHp = 1000f;
        private float _meterPulse;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void SyncHudState(
            MatchState matchState,
            bool showDebugBoxes,
            bool paused,
            DummyBehaviorMode dummyMode,
            bool inspectorActive,
            int inspectorMoveIndex,
            int inspectorTick,
            FighterMeshMode meshMode,
            bool trainingOverlay = false,
            MatchPhase phase = MatchPhase.ActiveMatch,
            int roundNumber = 1,
            int timerSeconds = 99,
            int p1Wins = 0,
            int p2Wins = 0,
            int phaseTick = 0)
        {
            _matchState = matchState;
            _showDebugBoxes = showDebugBoxes;
            _paused = paused;
            _dummyMode = dummyMode;
            _inspectorActive = inspectorActive;
            _inspectorMoveIndex = inspectorMoveIndex;
            _inspectorTick = inspectorTick;
            _meshMode = meshMode;
            _trainingOverlay = trainingOverlay;
            _phase = phase;
            _roundNumber = roundNumber;
            _timerSeconds = timerSeconds;
            _p1Wins = p1Wins;
            _p2Wins = p2Wins;
            _phaseTick = phaseTick;
            _meterPulse = (_meterPulse + 0.08f) % Mathf.Tau;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_matchState == null)
            {
                return;
            }

            Vector2 vp = GetViewportRect().Size;
            Font font = ThemeDB.FallbackFont;

            if (_inspectorActive)
            {
                DrawTurnaroundAndMoveInspectorOverlay(vp, font);
                return;
            }

            DrawTopFighterBars(vp, font, _matchState.Fighters[0], _matchState.Fighters[1]);
            DrawMatchCallout(vp, font);
            if (_trainingOverlay)
            {
                DrawBottomFrameMeterStudio(vp, font, _matchState.Fighters[0], _matchState.Fighters[1]);
            }
        }

        private void DrawTopFighterBars(Vector2 vp, Font font, FighterState p1, FighterState p2)
        {
            var def = FighterCatalog.FirstFighter;
            float marginX = 40f;
            float barWidth = (vp.X * 0.5f) - 120f;
            float bannerH = _trainingOverlay ? 112f : 96f;

            DrawRect(new Rect2(0, 0, vp.X, bannerH), new Color(0.06f, 0.07f, 0.10f, 0.72f));

            // Delayed damage trail
            _p1TrailHp = ApproachTrail(_p1TrailHp, p1.Health, 4.5f);
            _p2TrailHp = ApproachTrail(_p2TrailHp, p2.Health, 4.5f);

            float p1HpRatio = Mathf.Clamp((float)p1.Health / def.MaxHealth, 0f, 1f);
            float p1TrailRatio = Mathf.Clamp(_p1TrailHp / def.MaxHealth, 0f, 1f);
            float p2HpRatio = Mathf.Clamp((float)p2.Health / def.MaxHealth, 0f, 1f);
            float p2TrailRatio = Mathf.Clamp(_p2TrailHp / def.MaxHealth, 0f, 1f);

            // P1 health (drains rightward from the right edge of the left bar)
            Rect2 p1HpBg = new Rect2(marginX, 18f, barWidth, 24f);
            DrawRect(p1HpBg, new Color("161412"));
            DrawRect(new Rect2(marginX + barWidth * (1f - p1TrailRatio), 18f, barWidth * p1TrailRatio, 24f), new Color("efe6dc"));
            DrawRect(new Rect2(marginX + barWidth * (1f - p1HpRatio), 18f, barWidth * p1HpRatio, 24f), new Color("d9534f"));
            DrawAngledTrim(p1HpBg, new Color("cfc0ae"));

            float p2Left = vp.X - marginX - barWidth;
            Rect2 p2HpBg = new Rect2(p2Left, 18f, barWidth, 24f);
            DrawRect(p2HpBg, new Color("161412"));
            DrawRect(new Rect2(p2Left, 18f, barWidth * p2TrailRatio, 24f), new Color("efe6dc"));
            DrawRect(new Rect2(p2Left, 18f, barWidth * p2HpRatio, 24f), new Color("4b8bbe"));
            DrawAngledTrim(p2HpBg, new Color("cfc0ae"));

            // Structure bars — gold, with warning glow when high
            float p1StructRatio = Mathf.Clamp((float)p1.Structure / def.MaxStructure, 0f, 1f);
            float p2StructRatio = Mathf.Clamp((float)p2.Structure / def.MaxStructure, 0f, 1f);
            Color structCol = new Color("e6b84c");
            DrawRect(new Rect2(marginX, 46f, barWidth * 0.72f, 9f), new Color("1c1917"));
            DrawRect(new Rect2(marginX, 46f, barWidth * 0.72f * p1StructRatio, 9f),
                p1StructRatio > 0.75f ? structCol.Lightened(0.25f) : structCol);
            DrawRect(new Rect2(p2Left + barWidth * 0.28f, 46f, barWidth * 0.72f, 9f), new Color("1c1917"));
            DrawRect(new Rect2(p2Left + barWidth * 0.28f, 46f, barWidth * 0.72f * p2StructRatio, 9f),
                p2StructRatio > 0.75f ? structCol.Lightened(0.25f) : structCol);

            // Segmented super meter
            DrawSegmentedMeter(marginX, 60f, barWidth * 0.55f, 11f, (float)p1.Meter / def.MaxMeter, p1.Meter >= def.MaxMeter);
            DrawSegmentedMeter(p2Left + barWidth * 0.45f, 60f, barWidth * 0.55f, 11f, (float)p2.Meter / def.MaxMeter, p2.Meter >= def.MaxMeter);

            // Names
            DrawString(font, new Vector2(marginX, 88f), "FIRST FIGHTER", HorizontalAlignment.Left, -1, 15, new Color("efe6dc"));
            DrawString(font, new Vector2(p2Left, 88f), "MIRROR DUMMY", HorizontalAlignment.Left, -1, 15, new Color("efe6dc"));

            // Center: round pips + timer + round label
            float cx = vp.X * 0.5f;
            DrawRoundPips(cx - 58f, 22f, _p1Wins, facingRight: false);
            DrawRoundPips(cx + 34f, 22f, _p2Wins, facingRight: true);

            string timerText = _timerSeconds.ToString("00");
            DrawString(font, new Vector2(cx - 18f, 48f), timerText, HorizontalAlignment.Left, -1, 28, new Color("efe6dc"));
            DrawString(font, new Vector2(cx - 36f, 72f), $"ROUND {_roundNumber}", HorizontalAlignment.Left, -1, 13, new Color("cfc0ae"));

            if (_trainingOverlay)
            {
                DrawString(font, new Vector2(marginX, 104f),
                    $"HP:{p1.Health}  STRUCT:{p1.Structure}  METER:{p1.Meter}  STATE:{p1.State}",
                    HorizontalAlignment.Left, -1, 12, new Color("b6a89a"));
                DrawString(font, new Vector2(p2Left, 104f),
                    $"HP:{p2.Health}  STRUCT:{p2.Structure}  METER:{p2.Meter}  {_dummyMode}",
                    HorizontalAlignment.Left, -1, 12, new Color("b6a89a"));
            }

            // Combo banner
            FighterState? comboVictim = p2.ComboHits > 0 ? p2 : (p1.ComboHits > 0 ? p1 : null);
            if (comboVictim != null && comboVictim.ComboHits >= 1)
            {
                string who = comboVictim.PlayerIndex == 1 ? "P1 COMBO" : "P2 COMBO";
                float bannerX = comboVictim.PlayerIndex == 1 ? marginX : p2Left;
                float comboTop = bannerH + 8f;
                Rect2 comboBox = new Rect2(bannerX, comboTop, 320f, 36f);
                DrawRect(comboBox, new Color(0.08f, 0.09f, 0.12f, 0.86f));
                DrawRect(comboBox, new Color("e6b84c"), filled: false, width: 1.5f);
                DrawString(font, new Vector2(bannerX + 12f, comboTop + 24f),
                    $"{who}: {comboVictim.ComboHits} HITS  ({comboVictim.ComboDamage} DMG)",
                    HorizontalAlignment.Left, -1, 16, new Color("ffd166"));
            }
        }

        private void DrawMatchCallout(Vector2 vp, Font font)
        {
            if (_trainingOverlay && _phase == MatchPhase.ActiveMatch)
            {
                return;
            }

            string text = "";
            Color col = new Color("efe6dc");
            if (_phase == MatchPhase.RoundIntro)
            {
                text = _phaseTick < 40 ? $"ROUND {_roundNumber}" : "FIGHT";
                col = _phaseTick < 40 ? new Color("efe6dc") : new Color("ffd166");
            }
            else if (_phase == MatchPhase.KoFinish)
            {
                text = "K.O.";
                col = new Color("ff6b6b");
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float alpha = _phase == MatchPhase.KoFinish
                ? Mathf.Clamp(1f - (_phaseTick - 70) / 20f, 0f, 1f)
                : 1f;
            col.A = alpha;
            DrawString(font, new Vector2(vp.X * 0.5f - (text.Length * 14f), vp.Y * 0.42f),
                text, HorizontalAlignment.Left, -1, 48, col);
        }

        private void DrawSegmentedMeter(float x, float y, float w, float h, float ratio, bool full)
        {
            DrawRect(new Rect2(x, y, w, h), new Color("121014"));
            float fill = Mathf.Clamp(ratio, 0f, 1f) * w;
            Color fillCol = full
                ? new Color("56e39f").Lightened(0.15f + 0.10f * Mathf.Sin(_meterPulse))
                : new Color("3caea3");
            DrawRect(new Rect2(x, y, fill, h), fillCol);
            // Segment ticks
            for (int i = 1; i < 4; i++)
            {
                float sx = x + w * (i / 4f);
                DrawRect(new Rect2(sx, y, 1.5f, h), new Color(0.08f, 0.08f, 0.10f, 0.85f));
            }
            DrawRect(new Rect2(x, y, w, h), new Color("8a9a9a"), filled: false, width: 1f);
            if (full)
            {
                DrawString(ThemeDB.FallbackFont, new Vector2(x + w + 6f, y + h - 1f),
                    "MAX", HorizontalAlignment.Left, -1, 11, fillCol);
            }
        }

        private void DrawRoundPips(float x, float y, int wins, bool facingRight)
        {
            for (int i = 0; i < 2; i++)
            {
                float px = facingRight ? x + i * 18f : x + (1 - i) * 18f;
                bool lit = i < wins;
                DrawRect(new Rect2(px, y, 14f, 14f), lit ? new Color("ffd166") : new Color("2a2e34"));
                DrawRect(new Rect2(px, y, 14f, 14f), new Color("cfc0ae"), filled: false, width: 1.2f);
            }
        }

        private void DrawAngledTrim(Rect2 r, Color c)
        {
            DrawRect(r, c, filled: false, width: 1.6f);
            // Subtle bevel corners
            DrawRect(new Rect2(r.Position.X, r.Position.Y, 8f, 2f), c);
            DrawRect(new Rect2(r.Position.X + r.Size.X - 8f, r.Position.Y, 8f, 2f), c);
        }

        private static float ApproachTrail(float current, int target, float rate)
        {
            float t = target;
            if (t >= current) return t;
            return Mathf.Max(t, current - rate);
        }

        private void DrawBottomFrameMeterStudio(Vector2 vp, Font font, FighterState p1, FighterState p2)
        {
            float panelHeight = 148f;
            float panelTop = vp.Y - panelHeight;
            DrawRect(new Rect2(0, panelTop, vp.X, panelHeight), new Color(0.08f, 0.09f, 0.11f, 0.86f));

            float leftX = 44f;
            string statusLine =
                $"[F1] Boxes: {(_showDebugBoxes ? "ON" : "OFF")}   " +
                $"[F2] Pause: {(_paused ? "PAUSED" : "LIVE")}   " +
                $"[F3] Dummy: {_dummyMode}   " +
                $"[F4] Combo   " +
                $"[F6] Training: ON   " +
                $"[F8] Model: {_meshMode}   " +
                $"[F9] Turnaround";

            DrawString(font, new Vector2(leftX, panelTop + 22f),
                statusLine, HorizontalAlignment.Left, -1, 13, new Color("e6b84c"));

            DrawFighterFrameRow(font, "P1", p1, leftX, panelTop + 38f, 14f, 20f, 2f);
            DrawFighterFrameRow(font, "P2", p2, leftX, panelTop + 68f, 14f, 20f, 2f);

            DrawString(font, new Vector2(leftX, panelTop + 114f),
                "FRAME METER:  [CYAN Startup]  [RED Active]  [BLUE Recovery]  [YELLOW Stun]  [GOLD Parry]",
                HorizontalAlignment.Left, -1, 13, new Color("cfc0ae"));
            DrawString(font, new Vector2(leftX, panelTop + 134f),
                "CONTROLS:  WASD/Arrows Move  J=L  K=M  L=H  U=S  I=Throw  O=Super",
                HorizontalAlignment.Left, -1, 12, new Color("b6a89a"));
        }

        private void DrawFighterFrameRow(
            Font font, string label, FighterState f, float startX, float y, float cellW, float cellH, float gap)
        {
            var def = FighterCatalog.FirstFighter;
            var move = def.GetMove(f.CurrentMoveId);

            DrawString(font, new Vector2(startX, y + 15f), label, HorizontalAlignment.Left, -1, 15, new Color("efe6dc"));

            float barStartX = startX + 36f;
            for (int i = 0; i < FighterState.FrameMeterWindow; i++)
            {
                int idx = (f.FrameTimelineCursor + i) % FighterState.FrameMeterWindow;
                Color c = GetTimelineCellColor(f.FrameTimeline[idx]);
                Rect2 r = new Rect2(barStartX + i * (cellW + gap), y, cellW, cellH);
                DrawRect(r, c);
                if (i == FighterState.FrameMeterWindow - 1)
                {
                    DrawRect(r, Colors.White, filled: false, width: 1.5f);
                }
            }

            float infoX = barStartX + FighterState.FrameMeterWindow * (cellW + gap) + 18f;
            string moveText = f.CurrentMoveId != MoveId.None
                ? $"{move.Code} ({move.Name})  [{move.Startup}/{move.Active}/{move.Recovery}]  t={f.MoveTick}"
                : $"{f.State}";
            DrawString(font, new Vector2(infoX, y + 15f), moveText, HorizontalAlignment.Left, -1, 14, new Color("efe6dc"));

            if (f.HasFrameAdvantageSample)
            {
                string sign = f.LastFrameAdvantage >= 0 ? "+" : "";
                string tag = f.LastOutcomeWasBlock ? "ON BLOCK" : "ON HIT";
                Color advColor = f.LastFrameAdvantage > 0
                    ? new Color("56e39f")
                    : (f.LastFrameAdvantage < 0 ? new Color("ff6b6b") : new Color("ffd166"));
                DrawString(font, new Vector2(infoX + 350f, y + 15f),
                    $"ADV: {sign}{f.LastFrameAdvantage} {tag}",
                    HorizontalAlignment.Left, -1, 15, advColor);
            }
        }

        private static Color GetTimelineCellColor(FrameTimelineCellKind kind)
        {
            return kind switch
            {
                FrameTimelineCellKind.Startup => new Color("2ec4b6"),
                FrameTimelineCellKind.Active => new Color("e71d36"),
                FrameTimelineCellKind.Recovery => new Color("3a86ff"),
                FrameTimelineCellKind.Hitstun => new Color("ffbe0b"),
                FrameTimelineCellKind.Blockstun => new Color("fb8500"),
                FrameTimelineCellKind.ParryOrInvuln => new Color("ffd700"),
                FrameTimelineCellKind.Movement => new Color("5c554f"),
                _ => new Color("2b2724")
            };
        }

        private void DrawTurnaroundAndMoveInspectorOverlay(Vector2 vp, Font font)
        {
            DrawRect(new Rect2(0, 0, vp.X, 74f), new Color(0.12f, 0.11f, 0.10f, 0.82f));
            DrawString(font, new Vector2(40f, 30f),
                "F I R S T   F I G H T E R   —   4-PANEL TURNAROUND SHEET & 12-MOVE KEY-POSE STUDIO (F9 TO RETURN TO MATCH)",
                HorizontalAlignment.Left, -1, 17, new Color("efe6dc"));

            MoveId currentMoveId = FighterCatalog.CoreInspectorMoveOrder[
                Math.Clamp(_inspectorMoveIndex, 0, FighterCatalog.CoreInspectorMoveOrder.Length - 1)];
            MoveDef move = FighterCatalog.FirstFighter.GetMove(currentMoveId);

            DrawString(font, new Vector2(40f, 56f),
                $"ACTIVE INSPECTOR MOVE [Up/Down]: {move.Code} — {move.Name} ({move.InputNotation})   |   " +
                $"SCRUB [Left/Right]: {_inspectorTick} / {move.TotalTicks}   |   [F1] Boxes: {(_showDebugBoxes ? "ON" : "OFF")}",
                HorizontalAlignment.Left, -1, 14, new Color("ffd166"));

            float labelY = vp.Y - 115f;
            DrawString(font, new Vector2(vp.X * 0.14f, labelY), "F R O N T", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.31f, labelY), "S I D E", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.48f, labelY), "B A C K", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.66f, labelY), "G U A R D", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.85f, labelY), $"M O V E : {move.Code}", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));

            DrawRect(new Rect2(0, vp.Y - 86f, vp.X, 86f), new Color(0.11f, 0.10f, 0.09f, 0.88f));
            DrawString(font, new Vector2(40f, vp.Y - 58f),
                $"KEY-POSE: {move.KeyPoseSummary}",
                HorizontalAlignment.Left, -1, 14, new Color("efe6dc"));

            float barX = 40f;
            float barY = vp.Y - 40f;
            for (int t = 0; t < move.TotalTicks; t++)
            {
                Color c = t < move.Startup
                    ? new Color("2ec4b6")
                    : (t < move.Startup + move.Active ? new Color("e71d36") : new Color("3a86ff"));
                Rect2 r = new Rect2(barX + t * 20f, barY, 18f, 22f);
                DrawRect(r, c);
                if (t == _inspectorTick)
                {
                    DrawRect(r, Colors.White, filled: false, width: 2.5f);
                }
            }
        }
    }
}
