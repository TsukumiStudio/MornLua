#if USE_LUA
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MornLib
{
    /// <summary>
    /// 立ち絵ビュー。スロットは事前配置せず、speaker名で初めて呼ばれた瞬間にランタイム生成する。
    /// 発話中(<see cref="SetFocus"/>)のキャラをハイライト、それ以外をディムする。
    /// </summary>
    public sealed class MornLuaNovelPortraitView : MonoBehaviour
    {
        [SerializeField] private float _focusYOffset = 20f;
        [SerializeField] private float _unfocusYOffset = -10f;
        [SerializeField] private Color _focusColor = Color.white;
        [SerializeField] private Color _unfocusColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private float _lerpSpeed = 12f;

        private sealed class SlotState
        {
            public Image Image;
            public Vector2 BasePosition;
            public Vector2 TargetPosition;
            public Color TargetColor;
            public bool IsAlphaAnimating;
            public bool IsPositionAnimating;
        }

        private readonly Dictionary<string, SlotState> _slots = new();
        private string _focusedSpeaker;

        public void Preload(string speakerName, Sprite sprite, float scale)
        {
            var state = GetOrCreate(speakerName);
            state.Image.sprite = sprite;
            if (sprite != null)
            {
                state.Image.rectTransform.sizeDelta = sprite.rect.size * scale;
            }
        }

        /// <summary>
        /// 立ち絵を 0〜1 の正規化座標で配置する。
        /// duration 秒かけてフェードイン（0 で即時）。
        /// posX=0 → pivot が画面左、posX=1 → pivot が画面右。posY も同様（0=下、1=上）。
        /// </summary>
        public async UniTask ShowAsync(string speakerName, float posX, float posY, float duration, CancellationToken ct)
        {
            var state = GetOrCreate(speakerName);
            var parentRect = ((RectTransform)transform).rect;
            state.BasePosition = new Vector2(
                (posX - 0.5f) * parentRect.width,
                (posY - 0.5f) * parentRect.height);
            ApplyFocusToSlot(speakerName, state);
            state.Image.rectTransform.anchoredPosition = state.TargetPosition;
            state.Image.enabled = true;
            if (duration <= 0f)
            {
                state.Image.color = state.TargetColor;
                return;
            }

            state.IsAlphaAnimating = true;
            try
            {
                var endAlpha = state.TargetColor.a;
                var c = state.Image.color;
                c.a = 0f;
                state.Image.color = c;
                var elapsed = 0f;
                while (elapsed < duration && state.Image != null)
                {
                    elapsed += Time.deltaTime;
                    var alpha = Mathf.Clamp01(elapsed / duration) * endAlpha;
                    c = state.Image.color;
                    c.a = alpha;
                    state.Image.color = c;
                    await UniTask.Yield(ct);
                }

                if (state.Image != null)
                {
                    c = state.Image.color;
                    c.a = endAlpha;
                    state.Image.color = c;
                }
            }
            finally
            {
                state.IsAlphaAnimating = false;
            }
        }

        /// <summary>
        /// 立ち絵を duration 秒かけて指定座標へ移動。0 で即時。
        /// </summary>
        public async UniTask MoveAsync(string speakerName, float posX, float posY, float duration, CancellationToken ct)
        {
            if (!_slots.TryGetValue(speakerName, out var state) || state.Image == null)
            {
                return;
            }

            var parentRect = ((RectTransform)transform).rect;
            var endBase = new Vector2(
                (posX - 0.5f) * parentRect.width,
                (posY - 0.5f) * parentRect.height);
            if (duration <= 0f)
            {
                state.BasePosition = endBase;
                ApplyFocusToSlot(speakerName, state);
                state.Image.rectTransform.anchoredPosition = state.TargetPosition;
                return;
            }

            var startBase = state.BasePosition;
            state.IsPositionAnimating = true;
            try
            {
                var elapsed = 0f;
                while (elapsed < duration && state.Image != null)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    state.BasePosition = Vector2.Lerp(startBase, endBase, t);
                    ApplyFocusToSlot(speakerName, state);
                    state.Image.rectTransform.anchoredPosition = state.TargetPosition;
                    await UniTask.Yield(ct);
                }

                if (state.Image != null)
                {
                    state.BasePosition = endBase;
                    ApplyFocusToSlot(speakerName, state);
                    state.Image.rectTransform.anchoredPosition = state.TargetPosition;
                }
            }
            finally
            {
                state.IsPositionAnimating = false;
            }
        }

        public void Hide(string speakerName)
        {
            if (_slots.TryGetValue(speakerName, out var state) && state.Image != null)
            {
                state.Image.enabled = false;
            }
        }

        public void Clear()
        {
            foreach (var state in _slots.Values)
            {
                if (state.Image != null)
                {
                    state.Image.enabled = false;
                }
            }

            _focusedSpeaker = null;
        }

        /// <summary>表示中の全ての立ち絵を duration 秒かけてフェードアウトする。0 で即時。</summary>
        public async UniTask HideAllAsync(float duration, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                Clear();
                return;
            }

            var animating = new List<(SlotState state, float startAlpha)>();
            foreach (var s in _slots.Values)
            {
                if (s.Image != null && s.Image.enabled)
                {
                    animating.Add((s, s.Image.color.a));
                    s.IsAlphaAnimating = true;
                }
            }

            if (animating.Count == 0)
            {
                _focusedSpeaker = null;
                return;
            }

            try
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    foreach (var (s, sa) in animating)
                    {
                        if (s.Image == null)
                        {
                            continue;
                        }

                        var c = s.Image.color;
                        c.a = Mathf.Lerp(sa, 0f, t);
                        s.Image.color = c;
                    }

                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                foreach (var (s, _) in animating)
                {
                    if (s.Image != null)
                    {
                        s.Image.enabled = false;
                    }

                    s.IsAlphaAnimating = false;
                }

                _focusedSpeaker = null;
            }
        }

        public void SetFocus(string speakerName)
        {
            _focusedSpeaker = speakerName;
            foreach (var kvp in _slots)
            {
                ApplyFocusToSlot(kvp.Key, kvp.Value);
            }
        }

        private void ApplyFocusToSlot(string name, SlotState state)
        {
            var isFocused = string.IsNullOrEmpty(_focusedSpeaker) || name == _focusedSpeaker;
            var offsetY = isFocused ? _focusYOffset : _unfocusYOffset;
            state.TargetPosition = state.BasePosition + new Vector2(0f, offsetY);
            state.TargetColor = isFocused ? _focusColor : _unfocusColor;
        }

        private void Update()
        {
            var t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            foreach (var state in _slots.Values)
            {
                if (state.Image == null)
                {
                    continue;
                }

                if (!state.IsPositionAnimating)
                {
                    var rt = state.Image.rectTransform;
                    rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, state.TargetPosition, t);
                }

                if (!state.IsAlphaAnimating)
                {
                    state.Image.color = Color.Lerp(state.Image.color, state.TargetColor, t);
                }
            }
        }

        private SlotState GetOrCreate(string speakerName)
        {
            if (_slots.TryGetValue(speakerName, out var existing) && existing.Image != null)
            {
                return existing;
            }

            var go = new GameObject($"Portrait_{speakerName}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.enabled = false;
            image.raycastTarget = false;
            image.color = _focusColor;
            var state = new SlotState
            {
                Image = image,
                BasePosition = Vector2.zero,
                TargetPosition = Vector2.zero,
                TargetColor = _focusColor,
            };
            _slots[speakerName] = state;
            return state;
        }
    }
}
#endif
