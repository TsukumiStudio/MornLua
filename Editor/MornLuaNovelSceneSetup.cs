#if USE_LUA
using System;
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
            var root = new GameObject("LuaNovel");
            Undo.RegisterCreatedObjectUndo(root, "MornLua Novel Setup");
            var runner = root.AddComponent<MornLuaNovelRunner>();
            var player = root.AddComponent<MornLuaNovelPlayer>();

            var bgmGo = new GameObject("BgmSource");
            bgmGo.transform.SetParent(root.transform);
            var bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            var seGo = new GameObject("SeSource");
            seGo.transform.SetParent(root.transform);
            var seSource = seGo.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
            seSource.loop = false;

            var novelLayer = MornLuaGlobal.I.NovelLayer;
            if (novelLayer < 0)
            {
                Debug.LogWarning($"[MornLua] Novelレイヤーが見つかりません。Defaultレイヤーで生成します。");
                novelLayer = 0;
            }

            var cameraGo = new GameObject("Camera");
            cameraGo.transform.SetParent(root.transform);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Nothing;
            cam.cullingMask = 1 << novelLayer;
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            AddInternalComponent(cameraGo, "MornLib.MornAspectCamera, MornAspect");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            AddInternalComponent(canvasGo, "MornLib.MornAspectCanvas, MornAspect");

            var panelGo = new GameObject("Panel");
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.SetParent(canvasGo.transform, false);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(1920, 1080);
            AddInternalComponent(panelGo, "MornLib.MornAspectFullScreenUI, MornAspect");

            var bgGo = CreateFullRect("BackgroundView", panelRt);
            var bgViewObj = bgGo.AddComponent<MornLuaNovelBackgroundView>();
            var bgImageA = CreateFullRect("ImageA", bgGo.transform).AddComponent<Image>();
            bgImageA.raycastTarget = false;
            bgImageA.color = new Color(1f, 1f, 1f, 0f);
            bgImageA.enabled = false;
            var bgImageB = CreateFullRect("ImageB", bgGo.transform).AddComponent<Image>();
            bgImageB.raycastTarget = false;
            bgImageB.color = new Color(1f, 1f, 1f, 0f);
            bgImageB.enabled = false;

            var portraitGo = CreateFullRect("PortraitView", panelRt);
            var portraitView = portraitGo.AddComponent<MornLuaNovelPortraitView>();

            var bubbleAnchorGo = CreateFullRect("BubbleAnchor", panelRt);
            var bubbleAnchorRt = (RectTransform)bubbleAnchorGo.transform;

            var btnGo = CreateFullRect("AdvanceButton", panelRt);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0f, 0f, 0f, 0f);
            var btn = btnGo.AddComponent<Button>();

            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_advanceButton").objectReferenceValue = btn;
            runnerSo.FindProperty("_autoPlayOnStart").boolValue = false;
            runnerSo.FindProperty("_focusAdvanceButtonOnStart").boolValue = true;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("_runner").objectReferenceValue = runner;
            playerSo.FindProperty("_portraitView").objectReferenceValue = portraitView;
            playerSo.FindProperty("_backgroundView").objectReferenceValue = bgViewObj;
            playerSo.FindProperty("_bubbleAnchor").objectReferenceValue = bubbleAnchorRt;
            playerSo.FindProperty("_bgmSource").objectReferenceValue = bgmSource;
            playerSo.FindProperty("_seSource").objectReferenceValue = seSource;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            var bgViewSo = new SerializedObject(bgViewObj);
            bgViewSo.FindProperty("_imageA").objectReferenceValue = bgImageA;
            bgViewSo.FindProperty("_imageB").objectReferenceValue = bgImageB;
            bgViewSo.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, novelLayer);
            Selection.activeGameObject = root;
            Debug.Log("[MornLua] ノベルUI一式を現在のシーンに生成しました");
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
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

        private static void AddInternalComponent(GameObject target, string assemblyQualifiedName)
        {
            var type = Type.GetType(assemblyQualifiedName);
            if (type == null)
            {
                Debug.LogWarning($"[MornLua] 型 '{assemblyQualifiedName}' が見つかりません。コンポーネント追加をスキップします。");
                return;
            }

            target.AddComponent(type);
        }
    }
}
#endif
