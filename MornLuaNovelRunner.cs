#if USE_LUA
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lua.Unity;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MornLib
{
    /// <summary>
    /// Luaシナリオを汎用的にノベルとして再生するMonoBehaviour。
    /// Lua関数の実装は持たず、外部から <see cref="AddSetupHandler"/> で登録する。
    ///
    /// 典型的な使い方:
    /// <code>
    /// _runner.AddSetupHandler(lua =>
    /// {
    ///     lua.RegisterAsync&lt;string, string&gt;("message", async (speaker, text, ct) =>
    ///     {
    ///         await _runner.DOMessageAsync(text, (n, _) => _view.SetMessage(speaker, n &gt;= text.Length ? text : text.Substring(0, n)), ct);
    ///     });
    /// });
    /// </code>
    /// </summary>
    public sealed class MornLuaNovelRunner : MonoBehaviour
    {
        [SerializeField] private LuaAsset _scenario;
        [SerializeField] private Selectable _advanceButton;
        [SerializeField] private bool _autoPlayOnStart = true;
        [SerializeField] private bool _focusAdvanceButtonOnStart = true;
        [SerializeField, Min(0f)] private float _defaultCharInterval = 0.04f;

        private readonly List<Action<MornLuaCore>> _setupHandlers = new();
        private readonly List<Func<CancellationToken, UniTask>> _prePlayHandlers = new();
        private readonly List<Action> _postPlayHandlers = new();
        private UniTaskCompletionSource _advanceTcs;
        private CancellationTokenSource _playCts;
        private int _playVersion;

        public LuaAsset Scenario
        {
            get => _scenario;
            set => _scenario = value;
        }

        public float DefaultCharInterval => _defaultCharInterval;

        public bool IsPlaying => _playCts != null && !_playCts.IsCancellationRequested;

        /// <summary>
        /// Lua実行前に呼ばれる初期化ハンドラを登録する。
        /// 渡される <see cref="MornLuaCore"/> に <c>RegisterAction</c> / <c>RegisterAsync</c> 拡張で関数登録する。
        /// </summary>
        public void AddSetupHandler(Action<MornLuaCore> handler)
        {
            if (handler == null)
            {
                return;
            }

            _setupHandlers.Add(handler);
        }

        public void RemoveSetupHandler(Action<MornLuaCore> handler)
        {
            _setupHandlers.Remove(handler);
        }

        /// <summary>
        /// Lua 実行前に呼ばれる非同期ハンドラを登録する。事前ロード等に使う。
        /// </summary>
        public void AddPrePlayHandler(Func<CancellationToken, UniTask> handler)
        {
            if (handler == null)
            {
                return;
            }

            _prePlayHandlers.Add(handler);
        }

        /// <summary>
        /// Lua 実行終了（成功・キャンセル問わず）に呼ばれるハンドラを登録する。事後解放等に使う。
        /// </summary>
        public void AddPostPlayHandler(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            _postPlayHandlers.Add(handler);
        }

        private void Advance()
        {
            _advanceTcs?.TrySetResult();
        }

        /// <summary>
        /// 1メッセージ分の文字送り＋クリック待ちを行う。<c>message</c> 系Luaハンドラから呼ぶことを想定。
        /// クリックで Phase1 (タイプライター) をスキップ → 全文表示。再クリックで次行へ。
        /// </summary>
        public async UniTask DOMessageAsync(
            string text,
            MornLuaTypewriter.OnRevealCallback onReveal,
            Action onTypewriterFinished,
            Action onAdvanceConfirmed,
            CancellationToken ct)
        {
            _advanceTcs = new UniTaskCompletionSource();
            using (ct.Register(() => _advanceTcs?.TrySetCanceled()))
            {
                try
                {
                    await MornLuaTypewriter.PlayAsync(
                        text,
                        onReveal,
                        _ => _defaultCharInterval,
                        () => _advanceTcs.UnsafeGetStatus() != UniTaskStatus.Pending,
                        ct);

                    onReveal?.Invoke(text?.Length ?? 0, false);

                    if (_advanceTcs.UnsafeGetStatus() == UniTaskStatus.Succeeded)
                    {
                        _advanceTcs = new UniTaskCompletionSource();
                    }

                    onTypewriterFinished?.Invoke();
                    await _advanceTcs.Task;
                    onAdvanceConfirmed?.Invoke();
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        /// <summary>クリック1回（Advance）を待機する。</summary>
        public async UniTask WaitAdvanceAsync(CancellationToken ct)
        {
            _advanceTcs = new UniTaskCompletionSource();
            using (ct.Register(() => _advanceTcs?.TrySetCanceled()))
            {
                try
                {
                    await _advanceTcs.Task;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private void Start()
        {
            if (_advanceButton != null)
            {
                _advanceButton.OnSubmitAsObservable()
                    .Subscribe(_ => Advance())
                    .AddTo(this);
                _advanceButton.OnPointerClickAsObservable()
                    .Subscribe(_ => Advance())
                    .AddTo(this);
                if (_focusAdvanceButtonOnStart && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(_advanceButton.gameObject);
                }
            }

            var advanceAction = MornLuaGlobal.I.AdvanceAction;
            if (advanceAction != null && advanceAction.action != null)
            {
                advanceAction.action.Enable();
                advanceAction.action.performed += OnAdvanceActionPerformed;
            }

            MornDebugCore.RegisterGUI(MornLuaGlobal.I.DebugMenuKey, DrawDebugMenu, this);

            if (_autoPlayOnStart && !IsPlaying)
            {
                PlayAsync().Forget();
            }
        }

        private void OnDestroy()
        {
            var advanceAction = MornLuaGlobal.I.AdvanceAction;
            if (advanceAction != null && advanceAction.action != null)
            {
                advanceAction.action.performed -= OnAdvanceActionPerformed;
            }

            CancelCurrentPlay();
        }

        private void OnAdvanceActionPerformed(InputAction.CallbackContext _)
        {
            Advance();
        }

        private void DrawDebugMenu()
        {
            if (_scenario == null)
            {
                GUILayout.Label("Scenario未割り当て");
                return;
            }

            GUILayout.Label($"Scenario: {_scenario.name}");
            GUILayout.Label($"State: {(IsPlaying ? "Playing" : "Idle")}");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("最初から再生"))
                {
                    PlayAsync().Forget();
                }

                if (GUILayout.Button("停止"))
                {
                    CancelCurrentPlay();
                }
            }
        }

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (_scenario == null)
            {
                return;
            }

            CancelCurrentPlay();
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(ct, this.GetCancellationTokenOnDestroy());
            var playVersion = ++_playVersion;
            var playCt = _playCts.Token;
            try
            {
                _advanceTcs = null;
                foreach (var handler in _prePlayHandlers)
                {
                    await handler(playCt);
                }

                var lua = new MornLuaCore();
                foreach (var handler in _setupHandlers)
                {
                    handler(lua);
                }

                await lua.DoFileAsync(_scenario, ct: playCt);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
            }
            finally
            {
                foreach (var handler in _postPlayHandlers)
                {
                    handler();
                }

                if (_playVersion == playVersion)
                {
                    _playCts?.Dispose();
                    _playCts = null;
                }
            }
        }

        private void CancelCurrentPlay()
        {
            _playVersion++;
            _playCts?.Cancel();
            _playCts?.Dispose();
            _playCts = null;
        }
    }
}
#endif
