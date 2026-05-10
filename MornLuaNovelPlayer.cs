#if USE_LUA
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if USE_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MornLib
{
    /// <summary>
    /// MornLuaNovelRunner にノベル再生用の Lua 関数を登録するプレイヤー。
    /// 構文一覧：
    ///   message(speaker, text)                      メッセージ表示（クリック送り）
    ///   wait_submit()                               明示的なクリック待ち
    ///   chara_load(key, addr, scale)                立ち絵プリロード（実行前に自動ロード）
    ///   chara_show(key, {posX, posY} [, duration])  立ち絵表示（duration 秒フェードイン、省略=即時）
    ///   chara_move(key, {posX, posY} [, duration])  立ち絵を duration 秒かけて移動（省略=即時）
    ///   chara_hide(key)                             立ち絵非表示
    ///   all_hide([duration])                        立ち絵＋吹き出し＋背景を duration 秒かけてフェードアウト（省略=即時）
    ///   bubble_show(addr)                           吹き出し Prefab を Addressables からロードして差し替え
    ///   bubble_hide()                               現在の吹き出しを破棄
    ///   background(addr [, duration])               背景クロスフェード切替
    ///   bgm(addr [, duration])                      BGM 再生（duration 秒でフェードイン）
    ///   bgm_stop([duration])                        BGM 停止（duration 秒でフェードアウト）
    ///   se(addr)                                    SE 再生
    /// </summary>
    public sealed class MornLuaNovelPlayer : MonoBehaviour
    {
        [SerializeField] private MornLuaNovelRunner _runner;
        [SerializeField] private MornLuaNovelPortraitView _portraitView;
        [SerializeField] private MornLuaNovelBackgroundView _backgroundView;
        [SerializeField] private RectTransform _bubbleAnchor;
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _seSource;
#if USE_ADDRESSABLE
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> _spriteHandles = new();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _audioHandles = new();
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _bubbleHandles = new();
#endif
        private MornLuaNovelBubble _currentBubble;
        private string _currentBubbleAddr;
        private static readonly Regex CharaLoadPattern = new(
            @"chara_load\s*\(\s*[""'][^""']*[""']\s*,\s*[""']([^""']+)[""']",
            RegexOptions.Compiled);

        private void Awake()
        {
            _portraitView.Clear();
            _backgroundView.Clear();
            _runner.AddPrePlayHandler(PreloadAsync);
            _runner.AddPostPlayHandler(ReleaseAllHandles);
            _runner.AddSetupHandler(RegisterLuaFunctions);
        }

        private void OnDestroy()
        {
            ReleaseAllHandles();
        }

        private async UniTask PreloadAsync(System.Threading.CancellationToken ct)
        {
            if (_runner.Scenario == null)
            {
                return;
            }

            var addresses = new HashSet<string>();
            foreach (Match m in CharaLoadPattern.Matches(_runner.Scenario.Text))
            {
                addresses.Add(m.Groups[1].Value);
            }

            foreach (var addr in addresses)
            {
                await GetOrLoadSpriteAsync(addr, ct);
            }
        }

        private void ReleaseAllHandles()
        {
            DestroyCurrentBubble();
#if USE_ADDRESSABLE
            foreach (var handle in _spriteHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _spriteHandles.Clear();
            foreach (var handle in _audioHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _audioHandles.Clear();
            foreach (var handle in _bubbleHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _bubbleHandles.Clear();
#endif
        }

        private void RegisterLuaFunctions(MornLuaCore lua)
        {
            lua.RegisterAsync("wait_submit", async ct =>
            {
                await _runner.WaitAdvanceAsync(ct);
                if (!ct.IsCancellationRequested)
                {
                    PlayAdvanceSe();
                }
            });
            lua.RegisterAsync<string, string>("message", async (speaker, text, ct) =>
            {
                _portraitView.SetFocus(speaker);
                if (_currentBubble == null)
                {
                    Debug.LogWarning("[MornLuaNovelPlayer] bubble 未設定のため message を表示できません");
                    return;
                }

                _currentBubble.SetAdvanceIconVisible(false);
                _currentBubble.SetMessage(speaker, text);
                // リッチテキストタグを除いたプレーンテキストでタイプライタを駆動し、
                // 表示は maxVisibleCharacters で制御してタグ崩れを防ぐ
                var plainText = _currentBubble.BeginReveal();
                var typeSe = MornLuaGlobal.I.TypeSe;
                var typeInterval = Mathf.Max(1, MornLuaGlobal.I.TypeSeInterval);
                var lastPlayed = 0;
                await _runner.DOMessageAsync(
                    plainText,
                    (visibleCount, fastForward) =>
                    {
                        _currentBubble.SetVisibleCharacters(visibleCount);
                        if (typeSe != null && !fastForward && visibleCount > lastPlayed
                            && (lastPlayed == 0 || visibleCount - lastPlayed >= typeInterval))
                        {
                            _seSource.MornPlayOneShot(typeSe);
                            lastPlayed = visibleCount;
                        }
                    },
                    () => _currentBubble.SetAdvanceIconVisible(true),
                    PlayAdvanceSe,
                    ct);
                _currentBubble.SetAdvanceIconVisible(false);
            });
            lua.AddDefaultFunction("chara_load", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var key = ctx.GetArgument<string>(0);
                var addr = ctx.GetArgument<string>(1);
                var scale = ctx.ArgumentCount >= 3 ? (float)ctx.GetArgument<double>(2) : 1f;
                var sprite = await GetOrLoadSpriteAsync(addr, ct);
                _portraitView.Preload(key, sprite, scale);
                return 0;
            }));
            lua.AddDefaultFunction("chara_show", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var key = ctx.GetArgument<string>(0);
                var posTable = ctx.GetArgument<Lua.LuaTable>(1);
                var posX = (float)posTable[1].Read<double>();
                var posY = (float)posTable[2].Read<double>();
                var duration = ctx.ArgumentCount >= 3 ? (float)ctx.GetArgument<double>(2) : 0f;
                await _portraitView.ShowAsync(key, posX, posY, duration, ct);
                return 0;
            }));
            lua.AddDefaultFunction("chara_move", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var key = ctx.GetArgument<string>(0);
                var posTable = ctx.GetArgument<Lua.LuaTable>(1);
                var posX = (float)posTable[1].Read<double>();
                var posY = (float)posTable[2].Read<double>();
                var duration = ctx.ArgumentCount >= 3 ? (float)ctx.GetArgument<double>(2) : 0f;
                await _portraitView.MoveAsync(key, posX, posY, duration, ct);
                return 0;
            }));
            lua.RegisterAction<string>("chara_hide", key => _portraitView.Hide(key));
            lua.AddDefaultFunction("all_hide", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var duration = ctx.ArgumentCount >= 1 ? (float)ctx.GetArgument<double>(0) : 0f;
                var bubble = _currentBubble;
                var tasks = new List<UniTask>
                {
                    _portraitView.HideAllAsync(duration, ct),
                    _backgroundView.HideAsync(duration, ct),
                };
                if (bubble != null)
                {
                    tasks.Add(bubble.HideAsync(duration, ct));
                }

                await UniTask.WhenAll(tasks);
                DestroyCurrentBubble();
                return 0;
            }));
            lua.AddDefaultFunction("bubble_show", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var addr = ctx.GetArgument<string>(0);
                await ShowBubbleAsync(addr, ct);
                return 0;
            }));
            lua.RegisterAction("bubble_hide", DestroyCurrentBubble);
            lua.AddDefaultFunction("background", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var addr = ctx.GetArgument<string>(0);
                var duration = ctx.ArgumentCount >= 2 ? (float)ctx.GetArgument<double>(1) : 0f;
                var sprite = await GetOrLoadSpriteAsync(addr, ct);
                await _backgroundView.SetBackgroundAsync(sprite, duration, ct);
                return 0;
            }));
            lua.AddDefaultFunction("bgm", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var addr = ctx.GetArgument<string>(0);
                var duration = ctx.ArgumentCount >= 2 ? (float)ctx.GetArgument<double>(1) : 0f;
                var clip = await GetOrLoadAudioAsync(addr, ct);
                await PlayBgmAsync(clip, duration, ct);
                return 0;
            }));
            lua.AddDefaultFunction("bgm_stop", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var duration = ctx.ArgumentCount >= 1 ? (float)ctx.GetArgument<double>(0) : 0f;
                await StopBgmAsync(duration, ct);
                return 0;
            }));
            lua.AddDefaultFunction("se", new Lua.LuaFunction(async (ctx, ct) =>
            {
                var addr = ctx.GetArgument<string>(0);
                var clip = await GetOrLoadAudioAsync(addr, ct);
                if (clip != null)
                {
                    _seSource.MornPlayOneShot(clip);
                }

                return 0;
            }));
        }

        private async UniTask ShowBubbleAsync(string addr, System.Threading.CancellationToken ct)
        {
            if (_currentBubbleAddr == addr && _currentBubble != null)
            {
                _currentBubble.Clear();
                return;
            }

            DestroyCurrentBubble();
            var prefab = await GetOrLoadBubblePrefabAsync(addr, ct);
            if (prefab == null)
            {
                return;
            }

            var instance = Instantiate(prefab, _bubbleAnchor);
            _currentBubble = instance.GetComponent<MornLuaNovelBubble>();
            if (_currentBubble == null)
            {
                Debug.LogWarning($"[MornLuaNovelPlayer] Prefab '{addr}' に MornLuaNovelBubble が無い");
                Destroy(instance);
                return;
            }

            _currentBubble.Clear();
            _currentBubbleAddr = addr;
        }

        private void PlayAdvanceSe()
        {
            var se = MornLuaGlobal.I.AdvanceSe;
            if (se != null)
            {
                _seSource.MornPlayOneShot(se);
            }
        }

        private void DestroyCurrentBubble()
        {
            if (_currentBubble != null)
            {
                Destroy(_currentBubble.gameObject);
                _currentBubble = null;
            }

            _currentBubbleAddr = null;
        }

#if USE_ADDRESSABLE
        private async UniTask<Sprite> GetOrLoadSpriteAsync(string addr, System.Threading.CancellationToken ct)
        {
            if (string.IsNullOrEmpty(addr))
            {
                return null;
            }

            if (_spriteHandles.TryGetValue(addr, out var cached) && cached.IsValid())
            {
                return cached.Result;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(addr);
                await handle.Task.AsUniTask().AttachExternalCancellation(ct);
                _spriteHandles[addr] = handle;
                return handle.Result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MornLuaNovelPlayer] sprite load failed: addr='{addr}' error={e.Message}");
                return null;
            }
        }

        private async UniTask<AudioClip> GetOrLoadAudioAsync(string addr, System.Threading.CancellationToken ct)
        {
            if (string.IsNullOrEmpty(addr))
            {
                return null;
            }

            if (_audioHandles.TryGetValue(addr, out var cached) && cached.IsValid())
            {
                return cached.Result;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(addr);
                await handle.Task.AsUniTask().AttachExternalCancellation(ct);
                _audioHandles[addr] = handle;
                return handle.Result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MornLuaNovelPlayer] audio load failed: addr='{addr}' error={e.Message}");
                return null;
            }
        }

        private async UniTask<GameObject> GetOrLoadBubblePrefabAsync(string addr, System.Threading.CancellationToken ct)
        {
            if (string.IsNullOrEmpty(addr))
            {
                return null;
            }

            if (_bubbleHandles.TryGetValue(addr, out var cached) && cached.IsValid())
            {
                return cached.Result;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(addr);
                await handle.Task.AsUniTask().AttachExternalCancellation(ct);
                _bubbleHandles[addr] = handle;
                return handle.Result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MornLuaNovelPlayer] bubble prefab load failed: addr='{addr}' error={e.Message}");
                return null;
            }
        }
#else
        private UniTask<Sprite> GetOrLoadSpriteAsync(string _, System.Threading.CancellationToken __)
        {
            Debug.LogWarning("[MornLuaNovelPlayer] Addressables is not available.");
            return UniTask.FromResult<Sprite>(null);
        }

        private UniTask<AudioClip> GetOrLoadAudioAsync(string _, System.Threading.CancellationToken __)
        {
            Debug.LogWarning("[MornLuaNovelPlayer] Addressables is not available.");
            return UniTask.FromResult<AudioClip>(null);
        }

        private UniTask<GameObject> GetOrLoadBubblePrefabAsync(string _, System.Threading.CancellationToken __)
        {
            Debug.LogWarning("[MornLuaNovelPlayer] Addressables is not available.");
            return UniTask.FromResult<GameObject>(null);
        }
#endif

        private async UniTask PlayBgmAsync(AudioClip clip, float duration, System.Threading.CancellationToken ct)
        {
            if (clip == null)
            {
                return;
            }

            _bgmSource.MornPlay(clip);
            if (duration <= 0f)
            {
                return;
            }

            var target = _bgmSource.volume;
            _bgmSource.volume = 0f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, target, elapsed / duration);
                await UniTask.Yield(ct);
            }

            _bgmSource.volume = target;
        }

        private async UniTask StopBgmAsync(float duration, System.Threading.CancellationToken ct)
        {
            if (duration <= 0f)
            {
                _bgmSource.Stop();
                return;
            }

            var startVolume = _bgmSource.volume;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                await UniTask.Yield(ct);
            }

            _bgmSource.volume = 0f;
            _bgmSource.Stop();
        }
    }
}
#endif
