#if USE_LUA
using System.Threading;
using Cysharp.Threading.Tasks;
using Lua.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MornLib
{
    [CreateAssetMenu(fileName = nameof(MornLuaGlobal), menuName = "Morn/" + nameof(MornLuaGlobal))]
    public sealed class MornLuaGlobal : MornGlobalBase<MornLuaGlobal>
    {
        [SerializeField] private MornSceneObject _luaNovelScene;
        protected override string ModuleName => "MornLua";
        public MornSceneObject LuaNovelScene => _luaNovelScene;

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

                    // 再生完了（シーンアンロード）を待機
                    while (scene.isLoaded)
                    {
                        await UniTask.Yield(ct);
                    }

                    return;
                }
            }

            Logger.LogError($"シーン '{sceneName}' にMornLuaNovelRunnerが見つかりません");
        }
    }
}
#endif
