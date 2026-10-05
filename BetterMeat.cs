using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using Beam;
using Beam.Crafting;
using Bamex.StrandedDeep.ModSettings;

namespace BamEx.StrandedDeep.BetterMeat
{
    [BepInPlugin("bamex.strandeddeep.bettermeat", "Better Meat", "0.3.2")]
    [BepInDependency(
        "com.bamex.strandeddeep.modsettings",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BetterMeatPlugin : BaseUnityPlugin
    {
        internal static BetterMeatPlugin Instance;

        internal ConfigEntry<bool> SeparateStacks;
        internal ConfigEntry<bool> BlockRawMeat;
        internal ConfigEntry<bool> BlockSpoiledMeat;
        internal ConfigEntry<bool> CookingHud;
        internal ConfigEntry<float> SecondsPerGameHour;
        internal ConfigEntry<float> HudScale;
        internal ConfigEntry<float> HudVerticalPosition;
        internal ConfigEntry<float> HudBottomMargin;
        internal ConfigEntry<bool> HideWhenMenuOpen;
        internal ConfigEntry<bool> HideWhenAnyMenuOpen;
        internal ConfigEntry<bool> StrongTextOutline;
        internal ConfigEntry<bool> DebugLogging;

        private Harmony _harmony;

        private PlayerHudState[] _hud = new PlayerHudState[]
        {
            new PlayerHudState(),
            new PlayerHudState()
        };

        private float _nextCameraRefresh;
        private float _nextHudRefresh;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private float _styleScale = -1.0f;
        private Texture2D _softPanelTexture;

        private void Awake()
        {
            Instance = this;

            SeparateStacks = Config.Bind(
                "Stacks",
                "SeparateMeatByState",
                true,
                "RAW / COOKED / SMOKED / SPOILED meat cannot share one stack.");

            BlockRawMeat = Config.Bind(
                "Eating",
                "BlockRawMeat",
                true,
                "Prevent using/eating raw meat.");

            BlockSpoiledMeat = Config.Bind(
                "Eating",
                "BlockSpoiledMeat",
                true,
                "Prevent using/eating spoiled meat.");

            CookingHud = Config.Bind(
                "Cooking HUD",
                "Enabled",
                true,
                "Show cooking/smoking countdowns when aiming at a campfire/smoker.");

            SecondsPerGameHour = Config.Bind(
                "Cooking HUD",
                "SecondsPerGameHour",
                100.0f,
                "Real seconds represented by one internal cooking/smoking hour at timeScale 1. Diagnostic value for this build is 100.");

            HudScale = Config.Bind(
                "Cooking HUD",
                "Scale",
                1.30f,
                "Cooking HUD size multiplier. 1.0 = base size.");

            HudVerticalPosition = Config.Bind(
                "Cooking HUD",
                "VerticalPosition",
                0.68f,
                "Legacy setting kept for compatibility. v0.2.6 uses BottomMargin instead.");

            HudBottomMargin = Config.Bind(
                "Cooking HUD",
                "BottomMargin",
                25.0f,
                "Distance in base pixels from the bottom of each player's viewport. Smaller = lower.");

            HideWhenMenuOpen = Config.Bind(
                "Cooking HUD",
                "HideWhenMenuOpen",
                false,
                "Legacy setting kept for compatibility.");

            HideWhenAnyMenuOpen = Config.Bind(
                "Cooking HUD",
                "HideWhenAnyMenuOpen",
                false,
                "Experimental. Disabled in v0.2.8 until exact game menu state flags are identified.");

            StrongTextOutline = Config.Bind(
                "Cooking HUD",
                "StrongTextOutline",
                true,
                "Draw a stronger dark outline behind HUD text to improve readability.");

            DebugLogging = Config.Bind(
                "Debug",
                "Logging",
                false,
                "Write Better Meat debug messages to BepInEx log.");

            RegisterModSettings();

            _harmony = new Harmony("bamex.strandeddeep.bettermeat");
            MeatStackPatch.Install(_harmony);
            SafeEatingPatch.Install(_harmony);

            Logger.LogInfo("Better Meat v0.3.2 loaded.");
            Logger.LogInfo("Stacks: " + SeparateStacks.Value);
            Logger.LogInfo("Block raw: " + BlockRawMeat.Value);
            Logger.LogInfo("Block spoiled: " + BlockSpoiledMeat.Value);
            Logger.LogInfo("Cooking HUD: " + CookingHud.Value);
            Logger.LogInfo("Cooking HUD scale: " + HudScale.Value.ToString(CultureInfo.InvariantCulture));
            Logger.LogInfo("Hide HUD on menus: disabled in v0.2.8 pending exact menu-state diagnostic");
        }

        private void RegisterModSettings()
        {
            bool registered = ModSettingsClient.RegisterModLocalized(
                "bettermeat",
                "BETTER MEAT",
                "BETTER MEAT",
                300);

            if (!registered)
            {
                Logger.LogInfo(
                    "Mod Settings host not available; Better Meat will use BepInEx config only.");
                return;
            }

            ModSettingsClient.AddToggleLocalized(
                "bettermeat",
                "cooking_hud",
                "Таймер приготовления",
                "Cooking Timer",
                100,
                "Вкл.",
                "On",
                "Выкл.",
                "Off",
                delegate
                {
                    return CookingHud.Value;
                },
                delegate(bool value)
                {
                    CookingHud.Value = value;

                    if (!value)
                        ClearHud();
                });

            ModSettingsClient.AddSliderLocalized(
                "bettermeat",
                "hud_scale",
                "Размер таймера",
                "Timer Size",
                110,
                0.75f,
                1.75f,
                0.05f,
                100.0f,
                "%",
                "%",
                0,
                delegate
                {
                    return HudScale.Value;
                },
                delegate(float value)
                {
                    HudScale.Value =
                        Mathf.Clamp(value, 0.75f, 1.75f);

                    _styleScale = -1.0f;
                });

            ModSettingsClient.AddChoiceLocalized(
                "bettermeat",
                "hud_position",
                "Положение таймера",
                "Timer Position",
                120,
                new string[]
                {
                    "Низко",
                    "Средне",
                    "Высоко"
                },
                new string[]
                {
                    "Bottom",
                    "Middle",
                    "Top"
                },
                delegate
                {
                    return GetHudPositionChoice();
                },
                delegate(int index)
                {
                    SetHudPositionChoice(index);
                });

            ModSettingsClient.AddToggleLocalized(
                "bettermeat",
                "block_raw",
                "Запретить сырое мясо",
                "Prevent Eating Raw Meat",
                200,
                "Вкл.",
                "On",
                "Выкл.",
                "Off",
                delegate
                {
                    return BlockRawMeat.Value;
                },
                delegate(bool value)
                {
                    BlockRawMeat.Value = value;
                });

            ModSettingsClient.AddToggleLocalized(
                "bettermeat",
                "block_spoiled",
                "Запретить протухшее мясо",
                "Prevent Eating Spoiled Meat",
                210,
                "Вкл.",
                "On",
                "Выкл.",
                "Off",
                delegate
                {
                    return BlockSpoiledMeat.Value;
                },
                delegate(bool value)
                {
                    BlockSpoiledMeat.Value = value;
                });

            ModSettingsClient.AddToggleLocalized(
                "bettermeat",
                "separate_stacks",
                "Разделять мясо по состоянию",
                "Separate Meat by State",
                300,
                "Вкл.",
                "On",
                "Выкл.",
                "Off",
                delegate
                {
                    return SeparateStacks.Value;
                },
                delegate(bool value)
                {
                    SeparateStacks.Value = value;
                });

            Logger.LogInfo(
                "Better Meat settings registered in Mod Settings.");
        }

        private int GetHudPositionChoice()
        {
            float margin = HudBottomMargin.Value;

            if (margin < 47.5f)
                return 0;

            if (margin < 95.0f)
                return 1;

            return 2;
        }

        private void SetHudPositionChoice(int index)
        {
            if (index <= 0)
            {
                HudBottomMargin.Value = 25.0f;
                return;
            }

            if (index == 1)
            {
                HudBottomMargin.Value = 70.0f;
                return;
            }

            HudBottomMargin.Value = 120.0f;
        }

        private void OnDestroy()
        {
            try
            {
                if (_harmony != null)
                    _harmony.UnpatchSelf();
            }
            catch { }

            try
            {
                if (_softPanelTexture != null)
                    Destroy(_softPanelTexture);
            }
            catch { }
        }

        private void Update()
        {
            if (!CookingHud.Value)
            {
                ClearHud();
                return;
            }

            if (Time.unscaledTime >= _nextCameraRefresh)
            {
                _nextCameraRefresh = Time.unscaledTime + 1.0f;
                RefreshPlayerCameras();
            }

            if (Time.unscaledTime >= _nextHudRefresh)
            {
                _nextHudRefresh = Time.unscaledTime + 0.15f;
                RefreshHudTargets();
            }
        }

        private void OnGUI()
        {
            if (!CookingHud.Value)
                return;

            EnsureStyles();

            for (int i = 0; i < _hud.Length; i++)
            {
                DrawPlayerHud(_hud[i]);
            }
        }

        internal void DebugLog(string message)
        {
            if (DebugLogging != null && DebugLogging.Value)
                Logger.LogInfo(message);
        }

        internal void WarningLog(string message)
        {
            Logger.LogWarning(message);
        }

        internal void ErrorLog(string message)
        {
            Logger.LogError(message);
        }

        private void ClearHud()
        {
            for (int i = 0; i < _hud.Length; i++)
            {
                _hud[i].Station = null;
                _hud[i].Cookables.Clear();
            }
        }

        private void RefreshPlayerCameras()
        {
            Camera[] all = Camera.allCameras;
            List<Camera> gameplay = new List<Camera>();

            for (int i = 0; i < all.Length; i++)
            {
                Camera c = all[i];
                if (c == null ||
                    !c.enabled ||
                    !c.gameObject.activeInHierarchy ||
                    c.targetTexture != null)
                    continue;

                string path = GetPath(c.gameObject);

                if (path.IndexOf("PlayerCamera", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (path.IndexOf("PlayerUI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("GameUI", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                gameplay.Add(c);
            }

            gameplay.Sort(delegate(Camera a, Camera b)
            {
                int x = a.rect.center.x.CompareTo(b.rect.center.x);
                if (x != 0) return x;
                return b.rect.center.y.CompareTo(a.rect.center.y);
            });

            List<Camera> distinct = new List<Camera>();

            for (int i = 0; i < gameplay.Count; i++)
            {
                Camera c = gameplay[i];
                bool duplicate = false;

                for (int j = 0; j < distinct.Count; j++)
                {
                    Camera d = distinct[j];

                    if (Mathf.Abs(d.rect.x - c.rect.x) < 0.02f &&
                        Mathf.Abs(d.rect.y - c.rect.y) < 0.02f &&
                        Mathf.Abs(d.rect.width - c.rect.width) < 0.02f &&
                        Mathf.Abs(d.rect.height - c.rect.height) < 0.02f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    distinct.Add(c);
            }

            for (int i = 0; i < _hud.Length; i++)
            {
                _hud[i].Camera = i < distinct.Count ? distinct[i] : null;
            }
        }

        private void RefreshHudTargets()
        {
            for (int i = 0; i < _hud.Length; i++)
            {
                PlayerHudState state = _hud[i];
                state.Station = null;
                state.Cookables.Clear();

                Camera camera = state.Camera;
                if (camera == null || !camera.enabled)
                    continue;

                Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
                RaycastHit hit;

                if (!Physics.Raycast(ray, out hit, 8.0f, ~0, QueryTriggerInteraction.Collide))
                    continue;

                if (hit.collider == null)
                    continue;

                Construction_CAMPFIRE station = FindCampfireFromHit(hit.collider.gameObject);
                if (station == null)
                    continue;

                List<Cooking> cookables = CookingStationReader.GetCookables(station);

                if (cookables == null || cookables.Count == 0)
                    continue;

                state.Station = station;

                for (int c = 0; c < cookables.Count; c++)
                {
                    Cooking cooking = cookables[c];
                    if (cooking == null || cooking.Food == null)
                        continue;

                    state.Cookables.Add(
                        CookingStationReader.CreateView(cooking));
                }
            }
        }

        private Construction_CAMPFIRE FindCampfireFromHit(GameObject hit)
        {
            if (hit == null)
                return null;

            Transform current = hit.transform;

            // Important: only walk UP from the collider that the player's
            // crosshair actually hit. Do not search children of Zone,
            // SaveContainer or other shared parents: that can resolve a
            // completely unrelated campfire elsewhere in the same subtree.
            for (int level = 0; current != null && level < 7; level++)
            {
                Construction_CAMPFIRE station =
                    current.GetComponent<Construction_CAMPFIRE>();

                if (station != null)
                    return station;

                current = current.parent;
            }

            return null;
        }

        private float GetHudScale()
        {
            if (HudScale == null)
                return 1.30f;

            return Mathf.Clamp(HudScale.Value, 0.75f, 2.00f);
        }

        private void EnsureStyles()
        {
            float scale = GetHudScale();

            if (_panelStyle != null &&
                Mathf.Abs(_styleScale - scale) < 0.001f)
                return;

            _styleScale = scale;

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.alignment = TextAnchor.UpperLeft;
            _panelStyle.padding = new RectOffset(
                Mathf.RoundToInt(14.0f * scale),
                Mathf.RoundToInt(14.0f * scale),
                Mathf.RoundToInt(11.0f * scale),
                Mathf.RoundToInt(11.0f * scale));

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = Mathf.RoundToInt(21.0f * scale);
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = Mathf.RoundToInt(18.0f * scale);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = Color.white;

            _smallStyle = new GUIStyle(GUI.skin.label);
            _smallStyle.fontSize = Mathf.RoundToInt(16.0f * scale);
            _smallStyle.fontStyle = FontStyle.Bold;
            _smallStyle.alignment = TextAnchor.MiddleCenter;
            _smallStyle.normal.textColor = Color.white;
        }

        private void DrawPlayerHud(PlayerHudState state)
        {
            if (state == null ||
                state.Camera == null ||
                state.Station == null ||
                state.Cookables.Count == 0)
                return;

            float scale = GetHudScale();
            Rect viewport = ToGuiRect(state.Camera.rect);

            bool smoker = false;

            for (int i = 0; i < state.Cookables.Count; i++)
            {
                if (state.Cookables[i].SmokingActive ||
                    state.Cookables[i].Smoked)
                {
                    smoker = true;
                    break;
                }
            }

            bool aggregate = state.Cookables.Count >= 2;

            float panelWidth = Mathf.Min(
                390.0f * scale,
                viewport.width - (30.0f * scale));

            float panelHeight;

            if (aggregate)
            {
                panelHeight = smoker
                    ? 126.0f * scale
                    : 94.0f * scale;
            }
            else
            {
                panelHeight = smoker
                    ? 132.0f * scale
                    : 100.0f * scale;
            }

            panelHeight = Mathf.Min(
                panelHeight,
                viewport.height - (20.0f * scale));

            float bottomMargin = HudBottomMargin != null
                ? Mathf.Clamp(HudBottomMargin.Value, 15.0f, 220.0f) * scale
                : 55.0f * scale;

            float x = viewport.x + ((viewport.width - panelWidth) * 0.5f);
            float y = viewport.yMax - panelHeight - bottomMargin;

            float minY = viewport.y + (10.0f * scale);
            float maxY = viewport.y + viewport.height - panelHeight - (10.0f * scale);
            y = Mathf.Clamp(y, minY, maxY);

            Rect panel = new Rect(x, y, panelWidth, panelHeight);
            DrawPanelBackground(panel, scale);

            float pad = 14.0f * scale;
            float titleHeight = 24.0f * scale;
            float lineHeight = 21.0f * scale;
            float barHeight = 7.0f * scale;
            float gap = 5.0f * scale;

            DrawShadowedLabel(
                new Rect(x + pad, y + (7.0f * scale), panelWidth - (pad * 2.0f), titleHeight),
                smoker ? "КОПТИЛЬНЯ" : "КОСТЁР",
                _titleStyle);

            float cursorY = y + (34.0f * scale);

            if (aggregate)
            {
                DrawShadowedLabel(
                    new Rect(x + pad, cursorY, panelWidth - (pad * 2.0f), lineHeight),
                    GetPiecesLabel(state.Cookables.Count),
                    _labelStyle);

                cursorY += lineHeight + (2.0f * scale);

                CookingView lastCooking = GetLastCookingView(state.Cookables);
                DrawAggregateCookingRow(
                    x + pad,
                    cursorY,
                    panelWidth - (pad * 2.0f),
                    barHeight,
                    lastCooking,
                    AllCooked(state.Cookables));

                cursorY += lineHeight + barHeight + gap;

                if (smoker)
                {
                    CookingView lastSmoking = GetLastSmokingView(state.Cookables);
                    DrawAggregateSmokingRow(
                        x + pad,
                        cursorY,
                        panelWidth - (pad * 2.0f),
                        barHeight,
                        lastSmoking,
                        AllSmoked(state.Cookables));
                }

                return;
            }

            CookingView view = state.Cookables[0];

            DrawShadowedLabel(
                new Rect(x + pad, cursorY, panelWidth - (pad * 2.0f), lineHeight),
                view.Name,
                _labelStyle);

            cursorY += lineHeight + (2.0f * scale);

            DrawSingleCookingRow(
                x + pad,
                cursorY,
                panelWidth - (pad * 2.0f),
                barHeight,
                view);

            cursorY += lineHeight + barHeight + gap;

            if (smoker)
            {
                DrawSingleSmokingRow(
                    x + pad,
                    cursorY,
                    panelWidth - (pad * 2.0f),
                    barHeight,
                    view);
            }
        }

        private string GetPiecesLabel(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;
            string word;

            if (lastTwo >= 11 && lastTwo <= 14)
                word = "кусков";
            else if (last == 1)
                word = "кусок";
            else if (last >= 2 && last <= 4)
                word = "куска";
            else
                word = "кусков";

            return count.ToString(CultureInfo.InvariantCulture) + " " + word + " мяса";
        }

        private CookingView GetLastCookingView(List<CookingView> views)
        {
            CookingView best = null;
            float max = -1.0f;

            for (int i = 0; i < views.Count; i++)
            {
                CookingView view = views[i];

                if (view.Cooked)
                    continue;

                if (best == null || view.CookingHours > max)
                {
                    best = view;
                    max = view.CookingHours;
                }
            }

            return best;
        }

        private CookingView GetLastSmokingView(List<CookingView> views)
        {
            CookingView best = null;
            float max = -1.0f;

            for (int i = 0; i < views.Count; i++)
            {
                CookingView view = views[i];

                if (view.Smoked)
                    continue;

                if (best == null || view.SmokingHours > max)
                {
                    best = view;
                    max = view.SmokingHours;
                }
            }

            return best;
        }

        private bool AllCooked(List<CookingView> views)
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (!views[i].Cooked)
                    return false;
            }

            return true;
        }

        private bool AllSmoked(List<CookingView> views)
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (!views[i].Smoked)
                    return false;
            }

            return true;
        }

        private void DrawAggregateCookingRow(
            float x,
            float y,
            float width,
            float barHeight,
            CookingView last,
            bool allCooked)
        {
            string label;
            float progress;

            if (allCooked || last == null)
            {
                label = "Все готовы";
                progress = 1.0f;
            }
            else
            {
                label = "Все готовы через " + FormatRemaining(last.CookingHours);
                progress = last.CookingProgress;
            }

            DrawShadowedLabel(
                new Rect(x, y, width, 20.0f * GetHudScale()),
                label,
                _smallStyle);

            DrawProgressBar(
                new Rect(x, y + (20.0f * GetHudScale()), width, barHeight),
                progress);
        }

        private void DrawAggregateSmokingRow(
            float x,
            float y,
            float width,
            float barHeight,
            CookingView last,
            bool allSmoked)
        {
            string label;
            float progress;

            if (allSmoked || last == null)
            {
                label = "Все копчёные";
                progress = 1.0f;
            }
            else
            {
                label = "Все копчёные через " + FormatRemaining(last.SmokingHours);
                progress = last.SmokingProgress;
            }

            DrawShadowedLabel(
                new Rect(x, y, width, 20.0f * GetHudScale()),
                label,
                _smallStyle);

            DrawProgressBar(
                new Rect(x, y + (20.0f * GetHudScale()), width, barHeight),
                progress);
        }

        private void DrawSingleCookingRow(
            float x,
            float y,
            float width,
            float barHeight,
            CookingView view)
        {
            string label;

            if (view.Cooked)
                label = "Готово";
            else if (view.CookingActive)
                label = "Готово через " + FormatRemaining(view.CookingHours);
            else
                label = "Готовка остановлена — " + FormatRemaining(view.CookingHours);

            DrawShadowedLabel(
                new Rect(x, y, width, 20.0f * GetHudScale()),
                label,
                _smallStyle);

            DrawProgressBar(
                new Rect(x, y + (20.0f * GetHudScale()), width, barHeight),
                view.Cooked ? 1.0f : view.CookingProgress);
        }

        private void DrawSingleSmokingRow(
            float x,
            float y,
            float width,
            float barHeight,
            CookingView view)
        {
            string label;

            if (view.Smoked)
                label = "Копчёное";
            else if (view.SmokingActive)
                label = "Копчёное через " + FormatRemaining(view.SmokingHours);
            else
                label = "Копчение остановлено — " + FormatRemaining(view.SmokingHours);

            DrawShadowedLabel(
                new Rect(x, y, width, 20.0f * GetHudScale()),
                label,
                _smallStyle);

            DrawProgressBar(
                new Rect(x, y + (20.0f * GetHudScale()), width, barHeight),
                view.Smoked ? 1.0f : view.SmokingProgress);
        }

        private void DrawProgressBar(Rect rect, float progress)
        {
            progress = Mathf.Clamp01(progress);

            Color oldColor = GUI.color;

            GUI.color = new Color(0.00f, 0.00f, 0.00f, 0.88f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = new Color(0.96f, 0.96f, 0.96f, 0.98f);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width * progress, rect.height),
                Texture2D.whiteTexture);

            GUI.color = oldColor;
        }

        private void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
        {
            if (StrongTextOutline != null && StrongTextOutline.Value)
            {
                Color old = GUI.color;
                GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.92f);
                float o = Mathf.Max(1.0f, 1.0f * GetHudScale());
                GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);
                GUI.color = old;
            }

            GUI.Label(rect, text, style);
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawPanelBackground(Rect rect, float scale)
        {
            EnsureSoftPanelTexture();

            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, _softPanelTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private void EnsureSoftPanelTexture()
        {
            if (_softPanelTexture != null)
                return;

            const int size = 64;

            _softPanelTexture = new Texture2D(
                size,
                size,
                TextureFormat.ARGB32,
                false);

            _softPanelTexture.name = "BetterMeatSoftPanel";
            _softPanelTexture.wrapMode = TextureWrapMode.Clamp;
            _softPanelTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Min(x, size - 1 - x) / 8.0f;
                    float ny = Mathf.Min(y, size - 1 - y) / 8.0f;
                    float edge = Mathf.Clamp01(Mathf.Min(nx, ny));

                    edge = edge * edge * (3.0f - (2.0f * edge));

                    float alpha = Mathf.Lerp(
                        0.08f,
                        0.60f,
                        edge);

                    pixels[(y * size) + x] =
                        new Color(
                            0.015f,
                            0.015f,
                            0.015f,
                            alpha);
                }
            }

            _softPanelTexture.SetPixels(pixels);
            _softPanelTexture.Apply(false, true);
        }

        private bool IsBlockingGameUi(Camera gameplayCamera)
        {
            if (gameplayCamera == null)
                return false;

            if (Time.timeScale <= 0.001f)
                return true;

            EventSystem eventSystem = FindActiveEventSystem();

            if (eventSystem == null)
                return false;

            GameObject selected =
                eventSystem.currentSelectedGameObject;

            if (selected != null &&
                IsMenuUiPath(GetPath(selected)))
            {
                return true;
            }

            Vector3 screenPoint =
                gameplayCamera.ViewportToScreenPoint(
                    new Vector3(
                        0.5f,
                        0.5f,
                        0.0f));

            PointerEventData data =
                new PointerEventData(eventSystem);

            data.position =
                new Vector2(
                    screenPoint.x,
                    screenPoint.y);

            List<RaycastResult> hits =
                new List<RaycastResult>();

            eventSystem.RaycastAll(
                data,
                hits);

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go =
                    hits[i].gameObject;

                if (go == null ||
                    !go.activeInHierarchy)
                    continue;

                string path =
                    GetPath(go);

                if (IsMenuUiPath(path))
                    return true;

                if (IsLargeUiElement(
                        go,
                        gameplayCamera))
                {
                    return true;
                }
            }

            return false;
        }

        private EventSystem FindActiveEventSystem()
        {
            EventSystem[] systems;

            try
            {
                systems = Resources.FindObjectsOfTypeAll<EventSystem>();
            }
            catch
            {
                return null;
            }

            if (systems == null || systems.Length == 0)
                return null;

            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem system = systems[i];

                if (system == null)
                    continue;

                GameObject go = system.gameObject;

                if (go != null &&
                    go.activeInHierarchy &&
                    system.enabled)
                {
                    return system;
                }
            }

            return null;
        }

        private bool IsMenuUiPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string lower =
                path.ToLowerInvariant();

            string[] keywords =
                new string[]
                {
                    "craft",
                    "inventory",
                    "backpack",
                    "container",
                    "storage",
                    "loot",
                    "pause",
                    "menu",
                    "option",
                    "setting",
                    "save",
                    "load",
                    "journal",
                    "cartograph",
                    "sleep",
                    "building",
                    "quickcraft",
                    "quick craft"
                };

            for (int i = 0;
                 i < keywords.Length;
                 i++)
            {
                if (lower.Contains(
                        keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLargeUiElement(
            GameObject go,
            Camera gameplayCamera)
        {
            RectTransform rect =
                go.GetComponent<RectTransform>();

            if (rect == null)
                return false;

            Vector3[] corners =
                new Vector3[4];

            rect.GetWorldCorners(corners);

            Canvas canvas =
                go.GetComponentInParent<Canvas>();

            Camera uiCamera = null;

            if (canvas != null &&
                canvas.renderMode !=
                    RenderMode.ScreenSpaceOverlay)
            {
                uiCamera =
                    canvas.worldCamera;
            }

            Vector2 p0 =
                RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    corners[0]);

            Vector2 p2 =
                RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    corners[2]);

            float width =
                Mathf.Abs(p2.x - p0.x);

            float height =
                Mathf.Abs(p2.y - p0.y);

            float requiredWidth =
                gameplayCamera.pixelWidth * 0.42f;

            float requiredHeight =
                gameplayCamera.pixelHeight * 0.30f;

            if (width < requiredWidth ||
                height < requiredHeight)
            {
                return false;
            }

            string path =
                GetPath(go).ToLowerInvariant();

            if (path.Contains("crosshair") ||
                path.Contains("interaction") ||
                path.Contains("notification"))
            {
                return false;
            }

            return true;
        }

        private string FormatRemaining(float internalHours)
        {
            float secondsPerHour =
                SecondsPerGameHour != null
                    ? Mathf.Max(1.0f, SecondsPerGameHour.Value)
                    : 100.0f;

            float scale = Time.timeScale;
            if (scale <= 0.01f)
                scale = 1.0f;

            int totalSeconds =
                Mathf.Max(
                    0,
                    Mathf.CeilToInt(internalHours * secondsPerHour / scale));

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            return minutes.ToString("00", CultureInfo.InvariantCulture) +
                   ":" +
                   seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        private Rect ToGuiRect(Rect cameraRect)
        {
            float x = cameraRect.x * Screen.width;
            float y = (1.0f - cameraRect.y - cameraRect.height) * Screen.height;
            float width = cameraRect.width * Screen.width;
            float height = cameraRect.height * Screen.height;

            return new Rect(x, y, width, height);
        }

        private string GetPath(GameObject go)
        {
            if (go == null)
                return "<null>";

            List<string> parts = new List<string>();
            Transform t = go.transform;

            while (t != null && parts.Count < 64)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private sealed class PlayerHudState
        {
            internal Camera Camera;
            internal Construction_CAMPFIRE Station;
            internal List<CookingView> Cookables = new List<CookingView>();
        }
    }

    internal enum MeatState
    {
        Raw = 0,
        Cooked = 1,
        Smoked = 2,
        Spoiled = 3
    }

    internal static class MeatStateReader
    {
        internal static bool IsMeat(IPickupable pickupable)
        {
            InteractiveObject_FOOD food = pickupable as InteractiveObject_FOOD;
            return IsMeat(food);
        }

        internal static bool IsMeat(InteractiveObject_FOOD food)
        {
            if (food == null)
                return false;

            string craftingType = food.CraftingType.ToString();

            return craftingType.IndexOf(
                       "INTERACTIVE_TYPE_FOOD_MEAT",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static MeatState GetState(InteractiveObject_FOOD food)
        {
            if (food == null)
                return MeatState.Raw;

            if (food.Spoiled)
                return MeatState.Spoiled;

            Smoking smoking = food.GetComponent<Smoking>();
            if (smoking != null && smoking.IsSmoked)
                return MeatState.Smoked;

            Cooking cooking = food.GetComponent<Cooking>();
            if (cooking != null && cooking.IsCooked)
                return MeatState.Cooked;

            return MeatState.Raw;
        }

        internal static MeatState GetState(IPickupable pickupable)
        {
            return GetState(pickupable as InteractiveObject_FOOD);
        }
    }

    internal static class SafeEatingPatch
    {
        internal static void Install(Harmony harmony)
        {
            MethodInfo canUseGetter =
                AccessTools.PropertyGetter(
                    typeof(InteractiveObject),
                    "CanUse");

            MethodInfo foodUse =
                AccessTools.Method(
                    typeof(InteractiveObject_FOOD),
                    "Use");

            MethodInfo canUsePostfix =
                AccessTools.Method(
                    typeof(SafeEatingPatch),
                    "CanUsePostfix");

            MethodInfo usePrefix =
                AccessTools.Method(
                    typeof(SafeEatingPatch),
                    "FoodUsePrefix");

            if (canUseGetter != null)
            {
                harmony.Patch(
                    canUseGetter,
                    postfix: new HarmonyMethod(canUsePostfix));
            }

            if (foodUse != null)
            {
                harmony.Patch(
                    foodUse,
                    prefix: new HarmonyMethod(usePrefix));
            }
        }

        public static void CanUsePostfix(
            InteractiveObject __instance,
            ref bool __result)
        {
            if (!__result)
                return;

            InteractiveObject_FOOD food =
                __instance as InteractiveObject_FOOD;

            if (!ShouldBlock(food))
                return;

            __result = false;
        }

        public static bool FoodUsePrefix(
            InteractiveObject_FOOD __instance)
        {
            if (!ShouldBlock(__instance))
                return true;

            BetterMeatPlugin.Instance.DebugLog(
                "Blocked eating meat state: " +
                MeatStateReader.GetState(__instance).ToString());

            return false;
        }

        private static bool ShouldBlock(
            InteractiveObject_FOOD food)
        {
            BetterMeatPlugin plugin = BetterMeatPlugin.Instance;

            if (plugin == null ||
                food == null ||
                !MeatStateReader.IsMeat(food))
                return false;

            MeatState state =
                MeatStateReader.GetState(food);

            if (state == MeatState.Raw &&
                plugin.BlockRawMeat.Value)
                return true;

            if (state == MeatState.Spoiled &&
                plugin.BlockSpoiledMeat.Value)
                return true;

            return false;
        }
    }

    internal static class MeatStackPatch
    {
        [ThreadStatic]
        private static IPickupable _incoming;

        private static readonly FieldInfo SlotDataField =
            AccessTools.Field(
                typeof(SlotStorage),
                "_slotData");

        private static readonly MethodInfo GetStackSizeMethod =
            AccessTools.Method(
                typeof(SlotStorage),
                "GetStackSize");

        private static readonly FieldInfo SlotCraftingTypeField =
            AccessTools.Field(
                typeof(StorageSlot<IPickupable>),
                "<CraftingType>k__BackingField");

        internal static void Install(Harmony harmony)
        {
            MethodInfo contextPrefix =
                AccessTools.Method(
                    typeof(MeatStackPatch),
                    "ContextPrefix");

            MethodInfo contextPostfix =
                AccessTools.Method(
                    typeof(MeatStackPatch),
                    "ContextPostfix");

            MethodInfo getSlotPostfix =
                AccessTools.Method(
                    typeof(MeatStackPatch),
                    "GetSlotPostfix");

            MethodInfo[] methods =
                typeof(SlotStorage).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (!IsContextMethod(method))
                    continue;

                harmony.Patch(
                    method,
                    prefix: new HarmonyMethod(contextPrefix),
                    postfix: new HarmonyMethod(contextPostfix));
            }

            MethodInfo targetGetSlot = null;

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "GetSlot")
                    continue;

                ParameterInfo[] ps = method.GetParameters();

                if (ps.Length == 2 &&
                    ps[0].ParameterType == typeof(CraftingType) &&
                    ps[1].ParameterType == typeof(bool))
                {
                    targetGetSlot = method;
                    break;
                }
            }

            if (targetGetSlot != null)
            {
                harmony.Patch(
                    targetGetSlot,
                    postfix: new HarmonyMethod(getSlotPostfix));
            }
            else
            {
                BetterMeatPlugin.Instance.ErrorLog(
                    "Better Meat: SlotStorage.GetSlot(CraftingType,bool) not found.");
            }
        }

        private static bool IsContextMethod(MethodInfo method)
        {
            if (method == null)
                return false;

            if (method.Name != "CanPush" &&
                method.Name != "Push" &&
                method.Name != "ReplicatedPush")
                return false;

            ParameterInfo[] ps = method.GetParameters();

            return ps.Length > 0 &&
                   ps[0].ParameterType == typeof(IPickupable);
        }

        public static void ContextPrefix(
            object[] __args,
            ref IPickupable __state)
        {
            __state = _incoming;

            if (__args != null &&
                __args.Length > 0)
            {
                IPickupable pickupable =
                    __args[0] as IPickupable;

                if (pickupable != null)
                    _incoming = pickupable;
            }
        }

        public static void ContextPostfix(
            IPickupable __state)
        {
            _incoming = __state;
        }

        public static void GetSlotPostfix(
            SlotStorage __instance,
            CraftingType type,
            bool assign,
            ref StorageSlot<IPickupable> __result)
        {
            BetterMeatPlugin plugin =
                BetterMeatPlugin.Instance;

            if (plugin == null ||
                !plugin.SeparateStacks.Value)
                return;

            IPickupable incoming = _incoming;

            if (incoming == null ||
                !MeatStateReader.IsMeat(incoming))
                return;

            if (!incoming.CraftingType.Equals(type))
                return;

            StorageSlot<IPickupable> compatible =
                FindCompatibleSlot(
                    __instance,
                    incoming,
                    type,
                    assign);

            __result = compatible;
        }

        private static StorageSlot<IPickupable> FindCompatibleSlot(
            SlotStorage storage,
            IPickupable incoming,
            CraftingType type,
            bool assign)
        {
            IList rawSlots =
                SlotDataField != null
                    ? SlotDataField.GetValue(storage) as IList
                    : null;

            if (rawSlots == null)
                return null;

            MeatState wanted =
                MeatStateReader.GetState(incoming);

            int stackSize =
                GetNativeStackSize(storage, type);

            StorageSlot<IPickupable> emptySlot = null;

            for (int i = 0; i < rawSlots.Count; i++)
            {
                StorageSlot<IPickupable> slot =
                    rawSlots[i] as StorageSlot<IPickupable>;

                if (slot == null)
                    continue;

                IList<IPickupable> objects =
                    slot.Objects;

                int count =
                    objects != null
                        ? objects.Count
                        : 0;

                if (count == 0)
                {
                    if (emptySlot == null)
                        emptySlot = slot;

                    continue;
                }

                if (!slot.CraftingType.Equals(type))
                    continue;

                if (count >= stackSize)
                    continue;

                MeatState slotState;

                if (!TryGetUniformMeatState(
                        objects,
                        out slotState))
                    continue;

                if (slotState == wanted)
                {
                    BetterMeatPlugin.Instance.DebugLog(
                        "Stack target: existing " +
                        wanted.ToString() +
                        " meat slot.");

                    return slot;
                }
            }

            if (emptySlot != null)
            {
                if (assign &&
                    SlotCraftingTypeField != null)
                {
                    SlotCraftingTypeField.SetValue(
                        emptySlot,
                        type);
                }

                BetterMeatPlugin.Instance.DebugLog(
                    "Stack target: empty slot for " +
                    wanted.ToString() +
                    " meat.");

                return emptySlot;
            }

            BetterMeatPlugin.Instance.DebugLog(
                "No compatible slot for " +
                wanted.ToString() +
                " meat.");

            return null;
        }

        private static bool TryGetUniformMeatState(
            IList<IPickupable> objects,
            out MeatState state)
        {
            state = MeatState.Raw;

            if (objects == null ||
                objects.Count == 0)
                return false;

            bool haveState = false;
            MeatState first = MeatState.Raw;

            for (int i = 0; i < objects.Count; i++)
            {
                IPickupable item = objects[i];

                if (!MeatStateReader.IsMeat(item))
                    return false;

                MeatState current =
                    MeatStateReader.GetState(item);

                if (!haveState)
                {
                    first = current;
                    haveState = true;
                }
                else if (current != first)
                {
                    return false;
                }
            }

            if (!haveState)
                return false;

            state = first;
            return true;
        }

        private static int GetNativeStackSize(
            SlotStorage storage,
            CraftingType type)
        {
            if (GetStackSizeMethod == null)
                return 1;

            try
            {
                object result =
                    GetStackSizeMethod.Invoke(
                        storage,
                        new object[] { type });

                if (result is int)
                    return Mathf.Max(1, (int)result);
            }
            catch (Exception ex)
            {
                BetterMeatPlugin.Instance.WarningLog(
                    "Better Meat: GetStackSize failed: " +
                    ex.Message);
            }

            return 1;
        }
    }

    internal static class CookingStationReader
    {
        private static readonly FieldInfo CookablesField =
            AccessTools.Field(
                typeof(Construction_CAMPFIRE),
                "_cookables");

        private static readonly FieldInfo CookingHoursField =
            AccessTools.Field(
                typeof(Cooking),
                "_cookingHours");

        private static readonly FieldInfo OriginalCookingHoursField =
            AccessTools.Field(
                typeof(Cooking),
                "_originalCookingHours");

        private static readonly FieldInfo SmokingHoursField =
            AccessTools.Field(
                typeof(Smoking),
                "_smokingHours");

        private static readonly FieldInfo OriginalSmokingHoursField =
            AccessTools.Field(
                typeof(Smoking),
                "_originalSmokingHours");

        internal static List<Cooking> GetCookables(
            Construction_CAMPFIRE station)
        {
            List<Cooking> result =
                new List<Cooking>();

            if (station == null ||
                CookablesField == null)
                return result;

            IEnumerable enumerable =
                CookablesField.GetValue(station)
                as IEnumerable;

            if (enumerable == null)
                return result;

            foreach (object value in enumerable)
            {
                Cooking cooking =
                    value as Cooking;

                if (cooking != null)
                    result.Add(cooking);
            }

            return result;
        }

        internal static CookingView CreateView(
            Cooking cooking)
        {
            CookingView view =
                new CookingView();

            InteractiveObject_FOOD food =
                cooking.Food;

            view.Name =
                GetMeatName(food);

            view.Cooked =
                cooking.IsCooked;

            view.CookingActive =
                cooking.IsBeingCooked;

            view.CookingHours =
                ReadFloat(
                    CookingHoursField,
                    cooking);

            float originalCooking =
                ReadFloat(
                    OriginalCookingHoursField,
                    cooking);

            if (view.Cooked)
            {
                view.CookingProgress = 1.0f;
            }
            else if (originalCooking > 0.0001f)
            {
                view.CookingProgress =
                    Mathf.Clamp01(
                        1.0f -
                        (view.CookingHours /
                         originalCooking));
            }

            Smoking smoking =
                food != null
                    ? food.GetComponent<Smoking>()
                    : null;

            if (smoking != null)
            {
                view.Smoked =
                    smoking.IsSmoked;

                view.SmokingActive =
                    smoking.IsBeingSmoked;

                view.SmokingHours =
                    ReadFloat(
                        SmokingHoursField,
                        smoking);

                float originalSmoking =
                    ReadFloat(
                        OriginalSmokingHoursField,
                        smoking);

                if (view.Smoked)
                {
                    view.SmokingProgress = 1.0f;
                }
                else if (originalSmoking > 0.0001f)
                {
                    view.SmokingProgress =
                        Mathf.Clamp01(
                            1.0f -
                            (view.SmokingHours /
                             originalSmoking));
                }
            }

            return view;
        }

        private static float ReadFloat(
            FieldInfo field,
            object instance)
        {
            if (field == null ||
                instance == null)
                return 0.0f;

            try
            {
                object value =
                    field.GetValue(instance);

                if (value is float)
                    return (float)value;
            }
            catch { }

            return 0.0f;
        }

        private static string GetMeatName(
            InteractiveObject_FOOD food)
        {
            if (food == null)
                return "Мясо";

            string type =
                food.CraftingType.ToString();

            string name = "Мясо";

            if (type.IndexOf(
                    "ATTRIBUTE_TYPE_SMALL",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                name = "Малое мясо";
            }
            else if (type.IndexOf(
                         "ATTRIBUTE_TYPE_MEDIUM",
                         StringComparison.OrdinalIgnoreCase) >= 0)
            {
                name = "Среднее мясо";
            }
            else if (type.IndexOf(
                         "ATTRIBUTE_TYPE_LARGE",
                         StringComparison.OrdinalIgnoreCase) >= 0)
            {
                name = "Большое мясо";
            }

            if (food.Spoiled)
                return "Тухлое " + name.ToLowerInvariant();

            return name;
        }
    }

    internal sealed class CookingView
    {
        internal string Name = "Мясо";

        internal bool Cooked;
        internal bool CookingActive;
        internal float CookingHours;
        internal float CookingProgress;

        internal bool Smoked;
        internal bool SmokingActive;
        internal float SmokingHours;
        internal float SmokingProgress;
    }
}
