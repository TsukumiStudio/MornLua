#if USE_LUA
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MornLib
{
    internal static class MornLuaNovelSceneSetup
    {
        [MenuItem("Tools/MornLua/ノベルシーン自動セットアップ")]
        private static void Setup()
        {
            // Root
            var root = new GameObject("LuaNovel");
            Undo.RegisterCreatedObjectUndo(root, "MornLua Novel Setup");
            var runner = root.AddComponent<MornLuaNovelRunner>();
            var player = root.AddComponent<MornLuaNovelPlayer>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // PortraitView
            var portraitGo = CreateFullRect("PortraitView", canvasGo.transform);
            var portraitView = portraitGo.AddComponent<MornLuaNovelPortraitView>();

            // DialoguePanel
            var panelGo = new GameObject("DialoguePanel");
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.SetParent(canvasGo.transform, false);
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0.25f);
            panelRt.sizeDelta = Vector2.zero;
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);
            var novelView = panelGo.AddComponent<MornLuaNovelView>();

            // SpeakerText
            var speakerGo = new GameObject("SpeakerText");
            var speakerRt = speakerGo.AddComponent<RectTransform>();
            speakerRt.SetParent(panelRt, false);
            speakerRt.anchorMin = new Vector2(0.05f, 0.7f);
            speakerRt.anchorMax = new Vector2(0.4f, 0.95f);
            speakerRt.sizeDelta = Vector2.zero;
            var speakerTmp = speakerGo.AddComponent<TextMeshProUGUI>();
            speakerTmp.fontSize = 28;
            speakerTmp.fontStyle = FontStyles.Bold;
            speakerTmp.color = Color.yellow;
            speakerTmp.alignment = TextAlignmentOptions.BottomLeft;

            // DialogueText
            var dialogueGo = new GameObject("DialogueText");
            var dialogueRt = dialogueGo.AddComponent<RectTransform>();
            dialogueRt.SetParent(panelRt, false);
            dialogueRt.anchorMin = new Vector2(0.05f, 0.05f);
            dialogueRt.anchorMax = new Vector2(0.95f, 0.7f);
            dialogueRt.sizeDelta = Vector2.zero;
            var dialogueTmp = dialogueGo.AddComponent<TextMeshProUGUI>();
            dialogueTmp.fontSize = 24;
            dialogueTmp.color = Color.white;
            dialogueTmp.alignment = TextAlignmentOptions.TopLeft;

            // AdvanceButton（全画面透明）
            var btnGo = CreateFullRect("AdvanceButton", canvasGo.transform);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0f, 0f, 0f, 0f);
            var btn = btnGo.AddComponent<Button>();

            // SerializeField 接続
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_advanceButton").objectReferenceValue = btn;
            runnerSo.FindProperty("_autoPlayOnStart").boolValue = false;
            runnerSo.FindProperty("_focusAdvanceButtonOnStart").boolValue = true;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("_runner").objectReferenceValue = runner;
            playerSo.FindProperty("_view").objectReferenceValue = novelView;
            playerSo.FindProperty("_portraitView").objectReferenceValue = portraitView;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            var viewSo = new SerializedObject(novelView);
            viewSo.FindProperty("_speakerText").objectReferenceValue = speakerTmp;
            viewSo.FindProperty("_dialogueText").objectReferenceValue = dialogueTmp;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            Debug.Log("[MornLua] ノベルUI一式を現在のシーンに生成しました");
        }

        private static GameObject CreateFullRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return go;
        }
    }
}
#endif
