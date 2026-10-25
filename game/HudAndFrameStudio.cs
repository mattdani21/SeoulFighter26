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

    /// <summary>
    /// Renders the Sifu-inspired Top HUD (Health, Structure/Guard Gauge, Super Meter, Combo & Scaling Counter),
    /// Bottom 60-Tick Color-Coded Frame Meter + Live Frame Advantage (+3 ON HIT, -4 ON BLOCK),
    /// and the F9 Character Turnaround (FRONT / SIDE / BACK / GUARD) & Move Inspector overlay.
    /// </summary>
    public partial class HudAndFrameStudio : Control
    {
        private MatchState? _matchState;
        private bool _showDebugBoxes = true;
        private bool _paused;
        private DummyBehaviorMode _dummyMode = DummyBehaviorMode.Stand;
        private bool _inspectorActive;
        private int _inspectorMoveIndex;
        private int _inspectorTick;

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
            int inspectorTick)
        {
            _matchState = matchState;
            _showDebugBoxes = showDebugBoxes;
            _paused = paused;
            _dummyMode = dummyMode;
            _inspectorActive = inspectorActive;
            _inspectorMoveIndex = inspectorMoveIndex;
            _inspectorTick = inspectorTick;
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
            DrawBottomFrameMeterStudio(vp, font, _matchState.Fighters[0], _matchState.Fighters[1]);
        }

        private void DrawTopFighterBars(Vector2 vp, Font font, FighterState p1, FighterState p2)
        {
            var def = FighterCatalog.FirstFighter;
            float marginX = 44f;
            float barWidth = (vp.X * 0.5f) - 110f;

            // Top Banner Background
            DrawRect(new Rect2(0, 0, vp.X, 108f), new Color(0.11f, 0.10f, 0.09f, 0.72f));

            // P1 Health Bar (Left)
            float p1HpRatio = Mathf.Clamp((float)p1.Health / def.MaxHealth, 0f, 1f);
            Rect2 p1HpBg = new Rect2(marginX, 20f, barWidth, 22f);
            DrawRect(p1HpBg, new Color("221f1c"));
            DrawRect(new Rect2(marginX + barWidth * (1f - p1HpRatio), 20f, barWidth * p1HpRatio, 22f), new Color("d9534f"));
            DrawRect(p1HpBg, new Color("cfc0ae"), filled: false, width: 1.5f);

            // P2 Health Bar (Right)
            float p2Left = vp.X - marginX - barWidth;
            float p2HpRatio = Mathf.Clamp((float)p2.Health / def.MaxHealth, 0f, 1f);
            Rect2 p2HpBg = new Rect2(p2Left, 20f, barWidth, 22f);
            DrawRect(p2HpBg, new Color("221f1c"));
            DrawRect(new Rect2(p2Left, 20f, barWidth * p2HpRatio, 22f), new Color("4b8bbe"));
            DrawRect(p2HpBg, new Color("cfc0ae"), filled: false, width: 1.5f);

            // Structure / Guard Gauge (Sifu gold bar directly beneath Health)
            float p1StructRatio = Mathf.Clamp((float)p1.Structure / def.MaxStructure, 0f, 1f);
            Rect2 p1StructBg = new Rect2(marginX, 46f, barWidth * 0.75f, 10f);
            DrawRect(p1StructBg, new Color("1c1917"));
            DrawRect(new Rect2(marginX, 46f, p1StructBg.Size.X * p1StructRatio, 10f), new Color("e6b84c"));

            float p2StructRatio = Mathf.Clamp((float)p2.Structure / def.MaxStructure, 0f, 1f);
            Rect2 p2StructBg = new Rect2(p2Left + barWidth * 0.25f, 46f, barWidth * 0.75f, 10f);
            DrawRect(p2StructBg, new Color("1c1917"));
            DrawRect(new Rect2(p2StructBg.Position.X, 46f, p2StructBg.Size.X * p2StructRatio, 10f), new Color("e6b84c"));

            // Super Meter Bars
            float p1MeterRatio = Mathf.Clamp((float)p1.Meter / def.MaxMeter, 0f, 1f);
            Rect2 p1MeterBg = new Rect2(marginX, 62f, barWidth * 0.55f, 12f);
            DrawRect(p1MeterBg, new Color("181614"));
            DrawRect(new Rect2(marginX, 62f, p1MeterBg.Size.X * p1MeterRatio, 12f),
                p1.Meter >= 100 ? new Color("56e39f") : new Color("3caea3"));

            float p2MeterRatio = Mathf.Clamp((float)p2.Meter / def.MaxMeter, 0f, 1f);
            Rect2 p2MeterBg = new Rect2(p2Left + barWidth * 0.45f, 62f, barWidth * 0.55f, 12f);
            DrawRect(p2MeterBg, new Color("181614"));
            DrawRect(new Rect2(p2MeterBg.Position.X, 62f, p2MeterBg.Size.X * p2MeterRatio, 12f),
                p2.Meter >= 100 ? new Color("56e39f") : new Color("3caea3"));

            // Fighter Labels & Telemetry
            DrawString(font, new Vector2(marginX, 94f),
                $"P1 FIRST FIGHTER (OATMEAL HENLEY)   HP:{p1.Health}  STRUCT:{p1.Structure}  METER:{p1.Meter}/100  STATE:{p1.State}",
                HorizontalAlignment.Left, -1, 14, new Color("efe6dc"));

            DrawString(font, new Vector2(p2Left, 94f),
                $"P2 MIRROR DUMMY ({_dummyMode.ToString().ToUpper()})   HP:{p2.Health}  STRUCT:{p2.Structure}  METER:{p2.Meter}/100  STATE:{p2.State}",
                HorizontalAlignment.Left, -1, 14, new Color("efe6dc"));

            // Center Tick Counter
            DrawString(font, new Vector2(vp.X * 0.5f - 48f, 42f),
                $"60Hz TICK\n  {_matchState!.Tick:D5}",
                HorizontalAlignment.Center, -1, 15, new Color("cfc0ae"));

            // Live Combo & Holy Trinity Scaling Banner
            FighterState? comboVictim = p2.ComboHits > 0 ? p2 : (p1.ComboHits > 0 ? p1 : null);
            if (comboVictim != null && comboVictim.ComboHits >= 1)
            {
                string who = comboVictim.PlayerIndex == 1 ? "P1 COMBO" : "P2 COMBO";
                float bannerX = comboVictim.PlayerIndex == 1 ? marginX : p2Left;
                Rect2 comboBox = new Rect2(bannerX, 118f, 340f, 62f);
                DrawRect(comboBox, new Color(0.14f, 0.12f, 0.10f, 0.86f));
                DrawRect(comboBox, new Color("e6b84c"), filled: false, width: 2f);

                DrawString(font, new Vector2(bannerX + 14f, 144f),
                    $"{who}: {comboVictim.ComboHits} HITS  ({comboVictim.ComboDamage} DMG)",
                    HorizontalAlignment.Left, -1, 18, new Color("ffd166"));

                int pushScale = 100 + Math.Min(Math.Max(0, comboVictim.ComboHits - 1), 8) * 15;
                string gbText = comboVictim.UsedGroundBounce ? " [GROUND BOUNCE USED]" : "";
                DrawString(font, new Vector2(bannerX + 14f, 168f),
                    $"DMG SCALE: {comboVictim.LastScalingPercent}%   PUSHBACK: {pushScale}%{gbText}",
                    HorizontalAlignment.Left, -1, 13, new Color("cfc0ae"));
            }
        }

        private void DrawBottomFrameMeterStudio(Vector2 vp, Font font, FighterState p1, FighterState p2)
        {
            float panelHeight = 148f;
            float panelTop = vp.Y - panelHeight;
            DrawRect(new Rect2(0, panelTop, vp.X, panelHeight), new Color(0.10f, 0.09f, 0.08f, 0.86f));

            float leftX = 44f;
            float cellW = 14f;
            float cellH = 20f;
            float gap = 2f;

            // Legend & Hotkeys Bar
            string statusLine =
                $"[F1] 3D Boxes: {(_showDebugBoxes ? "ON" : "OFF")}   " +
                $"[F2] Pause/Step: {(_paused ? "PAUSED (Press F2 to Step +1f, Space to Resume)" : "LIVE 60Hz")}   " +
                $"[F3] Dummy: {_dummyMode}   " +
                $"[F4] Golden 11-Hit Combo Demo   " +
                $"[F9] Turnaround & 12-Move Studio Inspector";

            DrawString(font, new Vector2(leftX, panelTop + 22f),
                statusLine, HorizontalAlignment.Left, -1, 13, new Color("e6b84c"));

            // Draw 60-tick Frame Timeline for P1 and P2
            DrawFighterFrameRow(font, "P1", p1, leftX, panelTop + 38f, cellW, cellH, gap);
            DrawFighterFrameRow(font, "P2", p2, leftX, panelTop + 68f, cellW, cellH, gap);

            // Color Legend + Controls Reference
            DrawString(font, new Vector2(leftX, panelTop + 114f),
                "FRAME METER LEGEND:  [CYAN = Startup]   [RED = Active]   [BLUE = Recovery]   [YELLOW = Hit/Blockstun]   [GOLD = Parry/Invuln]",
                HorizontalAlignment.Left, -1, 13, new Color("cfc0ae"));

            DrawString(font, new Vector2(leftX, panelTop + 134f),
                "CONTROLS:  WASD / Arrows = Move/Crouch/Jump   J = L (Jab)   K = M (Kick/Sweep)   L = H (Cross/6H Overhead)   U = S (5S Launch / j.S Meteor / 4S Parry)   I = L+M Throw   O = 236H+S Super",
                HorizontalAlignment.Left, -1, 12, new Color("b6a89a"));
        }

        private void DrawFighterFrameRow(
            Font font,
            string label,
            FighterState f,
            float startX,
            float y,
            float cellW,
            float cellH,
            float gap)
        {
            var def = FighterCatalog.FirstFighter;
            var move = def.GetMove(f.CurrentMoveId);

            DrawString(font, new Vector2(startX, y + 15f), label, HorizontalAlignment.Left, -1, 15, new Color("efe6dc"));

            float barStartX = startX + 36f;
            for (int i = 0; i < FighterState.FrameMeterWindow; i++)
            {
                int idx = (f.FrameTimelineCursor + i) % FighterState.FrameMeterWindow;
                FrameTimelineCellKind kind = f.FrameTimeline[idx];
                Color c = GetTimelineCellColor(kind);

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
                string sign = f.LastFrameAdvantage >= 0 ? $"+" : "";
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
                FrameTimelineCellKind.Startup => new Color("2ec4b6"),       // Cyan
                FrameTimelineCellKind.Active => new Color("e71d36"),        // Red
                FrameTimelineCellKind.Recovery => new Color("3a86ff"),      // Blue
                FrameTimelineCellKind.Hitstun => new Color("ffbe0b"),       // Yellow
                FrameTimelineCellKind.Blockstun => new Color("fb8500"),     // Amber-Yellow
                FrameTimelineCellKind.ParryOrInvuln => new Color("ffd700"), // Gold
                FrameTimelineCellKind.Movement => new Color("5c554f"),
                _ => new Color("2b2724")
            };
        }

        private void DrawTurnaroundAndMoveInspectorOverlay(Vector2 vp, Font font)
        {
            // Top Studio Title matching reference_first_fighter.jpg
            DrawRect(new Rect2(0, 0, vp.X, 74f), new Color(0.12f, 0.11f, 0.10f, 0.82f));
            DrawString(font, new Vector2(40f, 30f),
                "F I R S T   F I G H T E R   —   4-PANEL TURNAROUND SHEET & 12-MOVE KEY-POSE STUDIO (F9 TO RETURN TO MATCH)",
                HorizontalAlignment.Left, -1, 17, new Color("efe6dc"));

            MoveId currentMoveId = FighterCatalog.CoreInspectorMoveOrder[
                Math.Clamp(_inspectorMoveIndex, 0, FighterCatalog.CoreInspectorMoveOrder.Length - 1)];
            MoveDef move = FighterCatalog.FirstFighter.GetMove(currentMoveId);

            DrawString(font, new Vector2(40f, 56f),
                $"ACTIVE INSPECTOR MOVE [Up/Down Arrows]: {move.Code} — {move.Name} ({move.InputNotation})   |   " +
                $"SCRUB TICK [Left/Right Arrows]: {_inspectorTick} / {move.TotalTicks}   (Startup:{move.Startup}  Active:{move.Active}  Recovery:{move.Recovery})   |   [F1] 3D Boxes: {(_showDebugBoxes ? "ON" : "OFF")}",
                HorizontalAlignment.Left, -1, 14, new Color("ffd166"));

            // Labels underneath the 4 Turnaround Figures + 5th Live Move Scrub Figure
            float labelY = vp.Y - 115f;
            DrawString(font, new Vector2(vp.X * 0.14f, labelY), "F R O N T", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.31f, labelY), "S I D E", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.48f, labelY), "B A C K", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.66f, labelY), "G U A R D", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));
            DrawString(font, new Vector2(vp.X * 0.85f, labelY), $"M O V E : {move.Code} (t={_inspectorTick})", HorizontalAlignment.Center, -1, 16, new Color("2b2623"));

            // Bottom Frame Bar for the selected move
            DrawRect(new Rect2(0, vp.Y - 86f, vp.X, 86f), new Color(0.11f, 0.10f, 0.09f, 0.88f));
            DrawString(font, new Vector2(40f, vp.Y - 58f),
                $"KEY-POSE SILHOUETTE: {move.KeyPoseSummary}",
                HorizontalAlignment.Left, -1, 14, new Color("efe6dc"));

            float barX = 40f;
            float barY = vp.Y - 40f;
            float cellW = 18f;
            float cellH = 22f;
            for (int t = 0; t < move.TotalTicks; t++)
            {
                Color c = t < move.Startup
                    ? new Color("2ec4b6")
                    : (t < move.Startup + move.Active ? new Color("e71d36") : new Color("3a86ff"));
                Rect2 r = new Rect2(barX + t * (cellW + 2f), barY, cellW, cellH);
                DrawRect(r, c);
                if (t == _inspectorTick)
                {
                    DrawRect(r, Colors.White, filled: false, width: 2.5f);
                }
            }
        }
    }
}
