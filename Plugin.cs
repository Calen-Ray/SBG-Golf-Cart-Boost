using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfCartBoost
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "sbg.golfcartboost";
        public const string ModName = "GolfCartBoost";
        public const string ModVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal ConfigEntry<KeyCode> boostKeyConfig;
        internal ConfigEntry<float> boostDurationConfig;
        internal ConfigEntry<float> boostCooldownConfig;
        internal ConfigEntry<bool> uiPromptEnabledConfig;

        private static MethodInfo addSpeedBoostMethod;
        private float lastBoostTime = float.NegativeInfinity;
        private BoostPromptUi promptUi;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            boostKeyConfig = Config.Bind(
                "Boost",
                "BoostKey",
                KeyCode.Mouse1,
                "Key/button to trigger the cart boost. Defaults to right click (Mouse1). Any UnityEngine.KeyCode value works.");
            boostDurationConfig = Config.Bind(
                "Boost",
                "DurationSeconds",
                1.5f,
                "Duration of each boost in seconds. Reuses the SpeedBoost status's natural tick-down.");
            boostCooldownConfig = Config.Bind(
                "Boost",
                "CooldownSeconds",
                4.0f,
                "Minimum seconds between boost activations.");
            uiPromptEnabledConfig = Config.Bind(
                "Ui",
                "PromptEnabled",
                true,
                "Show the on-screen \"Click [button] to boost!\" prompt while driving.");

            addSpeedBoostMethod = AccessTools.Method(typeof(PlayerMovement), "AddSpeedBoost");
            if (addSpeedBoostMethod == null)
            {
                Log.LogWarning("PlayerMovement.AddSpeedBoost not found via reflection; boost will fall back to InformDrankCoffee with the coffee duration.");
            }

            new Harmony(ModGuid).PatchAll();
            Log.LogInfo($"{ModName} v{ModVersion} loaded.");
        }

        private void Update()
        {
            EnsurePromptUi();

            PlayerInfo info = GameManager.LocalPlayerInfo;
            bool driving = IsLocalPlayerDriving(info);
            float now = Time.time;
            bool offCooldown = now - lastBoostTime >= boostCooldownConfig.Value;

            if (driving && offCooldown && InputBridge.WasPressedThisFrame(boostKeyConfig.Value))
            {
                TriggerBoost(info);
                lastBoostTime = now;
            }

            if (promptUi != null)
                promptUi.UpdateState(driving, offCooldown, boostKeyConfig.Value, uiPromptEnabledConfig.Value);
        }

        private static bool IsLocalPlayerDriving(PlayerInfo info)
        {
            if (info == null)
                return false;
            GolfCartSeat seat = info.ActiveGolfCartSeat;
            if (!seat.IsValid())
                return false;
            return seat.IsDriver();
        }

        private void TriggerBoost(PlayerInfo info)
        {
            if (info == null || info.Movement == null)
                return;

            if (addSpeedBoostMethod != null)
            {
                addSpeedBoostMethod.Invoke(info.Movement, new object[] { boostDurationConfig.Value });
            }
            else
            {
                // Reflection fallback path: vanilla coffee informer applies the same status with
                // the coffee duration. Better than no boost.
                info.Movement.InformDrankCoffee();
            }
        }

        private void EnsurePromptUi()
        {
            if (promptUi != null)
                return;
            promptUi = BoostPromptUi.Create();
        }
    }

    internal sealed class BoostPromptUi : MonoBehaviour
    {
        private CanvasGroup group;
        private TextMeshProUGUI label;
        private float currentAlpha;

        public static BoostPromptUi Create()
        {
            GameObject canvasObj = new GameObject("GolfCartBoost-Canvas");
            DontDestroyOnLoad(canvasObj);
            canvasObj.hideFlags = HideFlags.HideAndDontSave;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            CanvasGroup group = canvasObj.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform rt = labelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 220f);
            rt.sizeDelta = new Vector2(640f, 60f);

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 0.94f, 0.55f, 1f);
            label.outlineColor = Color.black;
            label.outlineWidth = 0.2f;
            label.text = string.Empty;

            BoostPromptUi ui = canvasObj.AddComponent<BoostPromptUi>();
            ui.group = group;
            ui.label = label;
            return ui;
        }

        public void UpdateState(bool driving, bool offCooldown, KeyCode key, bool enabledByConfig)
        {
            float target = (driving && offCooldown && enabledByConfig) ? 1f : 0f;
            // Asymmetric fade: fade in fast when ready, fade out slower so the cooldown feels
            // like the prompt "cools" with the boost rather than blinking off the moment we
            // press the button.
            float speed = target > currentAlpha ? 6f : 2f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, target, speed * Time.deltaTime);
            group.alpha = currentAlpha;

            if (currentAlpha > 0.01f && driving)
            {
                string keyName = FormatKey(key);
                label.text = $"Press <b>{keyName}</b> to boost!";
            }
        }

        private static string FormatKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "Left Click";
                case KeyCode.Mouse1: return "Right Click";
                case KeyCode.Mouse2: return "Middle Click";
                default: return key.ToString();
            }
        }
    }
}
