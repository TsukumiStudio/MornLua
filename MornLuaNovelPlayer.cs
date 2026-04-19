#if USE_LUA
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lua;
using UnityEngine;
#if USE_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MornLib
{
    /// <summary>
    /// MornLuaNovelRunner に対して Lua 関数を登録する薄いグルー。
    /// Lua 関数:
    ///   message(speaker, text)              メッセージ表示
    ///   portrait(speaker, addr [, scale])   立ち絵スプライトをAddressables経由でロード。scaleはネイティブ解像度倍率（既定1.0）
    ///   show(speaker, x [, y])              立ち絵を x∈[-1,1], y∈[-1,1] の位置に表示（yは省略時0）
    ///   hide(speaker)                       立ち絵を非表示
    /// </summary>
    public sealed class MornLuaNovelPlayer : MonoBehaviour
    {
        [SerializeField] private MornLuaNovelRunner _runner;
        [SerializeField] private MornLuaNovelView _view;
        [SerializeField] private MornLuaNovelPortraitView _portraitView;

#if USE_ADDRESSABLE
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> _portraitHandles = new();
#endif

        private void Awake()
        {
            _view.Clear();
            _portraitView.Clear();
            _runner.AddSetupHandler(lua =>
            {
                lua.RegisterAsync<string, string>("message", async (speaker, text, ct) =>
                {
                    _portraitView.SetFocus(speaker);
                    await _runner.DOMessageAsync(
                        text,
                        (visibleCount, _) => _view.SetMessage(
                            speaker,
                            visibleCount >= text.Length ? text : text.Substring(0, visibleCount)),
                        ct);
                });

                // portrait は第3引数 scale が省略可能なので生 LuaFunction で処理する
                lua.AddDefaultFunction("portrait", new LuaFunction(async (ctx, ct) =>
                {
                    var speaker = ctx.GetArgument<string>(0);
                    var address = ctx.GetArgument<string>(1);
                    var scale = ctx.ArgumentCount >= 3 ? (float)ctx.GetArgument<double>(2) : 1f;
                    var sprite = await LoadSpriteAsync(speaker, address, ct);
                    _portraitView.Preload(speaker, sprite, scale);
                    return 0;
                }));

                // show は第3引数 y が省略可能なので生 LuaFunction で処理する
                lua.AddDefaultFunction("show", new LuaFunction((ctx, _) =>
                {
                    var speaker = ctx.GetArgument<string>(0);
                    var x = (float)ctx.GetArgument<double>(1);
                    var y = ctx.ArgumentCount >= 3 ? (float)ctx.GetArgument<double>(2) : 0f;
                    _portraitView.Show(speaker, x, y);
                    return new ValueTask<int>(0);
                }));

                lua.RegisterAction<string>("hide", speaker =>
                {
                    _portraitView.Hide(speaker);
                });
            });
        }

#if USE_ADDRESSABLE
        private void OnDestroy()
        {
            foreach (var handle in _portraitHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _portraitHandles.Clear();
        }

        private async UniTask<Sprite> LoadSpriteAsync(string speaker, string address, System.Threading.CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address))
            {
                return null;
            }

            // 同じspeakerに対する旧ハンドルを解放
            if (_portraitHandles.TryGetValue(speaker, out var oldHandle) && oldHandle.IsValid())
            {
                Addressables.Release(oldHandle);
                _portraitHandles.Remove(speaker);
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(address);
                await handle.Task.AsUniTask().AttachExternalCancellation(ct);
                var sprite = handle.Result;
                _portraitHandles[speaker] = handle;
                return sprite;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MornLuaNovelPlayer] portrait load failed: speaker='{speaker}' addr='{address}' error={e.Message}");
                return null;
            }
        }
#else
        private UniTask<Sprite> LoadSpriteAsync(string speaker, string address, System.Threading.CancellationToken ct)
        {
            Debug.LogWarning("[MornLuaNovelPlayer] Addressables is not available. portrait command is disabled.");
            return UniTask.FromResult<Sprite>(null);
        }
#endif
    }
}
#endif
