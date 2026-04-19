#if USE_LUA
using System.Threading;
using Cysharp.Threading.Tasks;
using Lua.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MornLib
{
    [CreateAssetMenu(fileName = nameof(MornLuaGlobal), menuName = "Morn/" + nameof(MornLuaGlobal))]
    public sealed class MornLuaGlobal : MornGlobalBase<MornLuaGlobal>
    {
        [SerializeField] private MornSceneObject _luaNovelScene;
        [SerializeField] private string _debugMenuKey = "MornLuaNovel/再生";
        [SerializeField] private string _novelLayerName = "Novel";
        [SerializeField] private TMP_FontAsset _defaultFont;
        [SerializeField, Min(0f)] private float _advanceIconBlinkPeriod = 0.8f;
        [SerializeField] private InputActionReference _advanceAction;
        [SerializeField] private AudioClip _typeSe;
        [SerializeField, Min(1)] private int _typeSeInterval = 4;
        [SerializeField] private AudioClip _advanceSe;
        protected override string ModuleName => "MornLua";
        public MornSceneObject LuaNovelScene => _luaNovelScene;
        public string DebugMenuKey => _debugMenuKey;
        public int NovelLayer => LayerMask.NameToLayer(_novelLayerName);
        public TMP_FontAsset DefaultFont => _defaultFont;
        public float AdvanceIconBlinkPeriod => _advanceIconBlinkPeriod;
        public InputActionReference AdvanceAction => _advanceAction;
        public AudioClip TypeSe => _typeSe;
        public int TypeSeInterval => _typeSeInterval;
        public AudioClip AdvanceSe => _advanceSe;

        /// <summary>指定したLuaAssetをノベルシーンで再生する</summary>
        public static async UniTask PlayLuaAsync(
            LuaAsset luaAsset,
            LoadSceneMode mode = LoadSceneMode.Additive,
            CancellationToken ct = default)
        {
            if (luaAsset == null)
            {
                Logger.LogWarning("LuaAssetがnullです");
                return;
            }

            string sceneName = I._luaNovelScene;
            if (string.IsNullOrEmpty(sceneName))
            {
                Logger.LogError("LuaNovelSceneが未設定です");
                return;
            }

            Logger.Log($"PlayLua: {luaAsset.name} (mode={mode})");

            await SceneManager.LoadSceneAsync(sceneName, mode);
            var scene = SceneManager.GetSceneByName(sceneName);

            // シーン内のMornLuaNovelRunnerを探してシナリオをセット・再生
            foreach (var root in scene.GetRootGameObjects())
            {
                var runner = root.GetComponentInChildren<MornLuaNovelRunner>(true);
                if (runner != null)
                {
                    runner.Scenario = luaAsset;
                    runner.Play();

                    // 再生完了を待機（IsPlaying が false になるまで）
                    while (runner != null && runner.IsPlaying)
                    {
                        await UniTask.Yield(ct);
                    }

                    // Lua再生終了 → シーンを破棄
                    if (scene.isLoaded)
                    {
                        await SceneManager.UnloadSceneAsync(scene).WithCancellation(ct);
                    }

                    return;
                }
            }

            Logger.LogError($"シーン '{sceneName}' にMornLuaNovelRunnerが見つかりません");
        }
    }
}
#endif
