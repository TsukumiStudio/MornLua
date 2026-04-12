#if USE_LUA
using System.Collections.Generic;
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

        public void Show(string speakerName, float x, float y)
        {
            var state = GetOrCreate(speakerName);
            var clampedX = Mathf.Clamp(x, -1f, 1f);
            var clampedY = Mathf.Clamp(y, -1f, 1f);
            var parentRect = ((RectTransform)transform).rect;
            state.BasePosition = new Vector2(
                clampedX * parentRect.width * 0.5f,
                clampedY * parentRect.height * 0.5f);
            ApplyFocusToSlot(speakerName, state);
            state.Image.rectTransform.anchoredPosition = state.TargetPosition;
            state.Image.color = state.TargetColor;
            state.Image.enabled = true;
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

                var rt = state.Image.rectTransform;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, state.TargetPosition, t);
                state.Image.color = Color.Lerp(state.Image.color, state.TargetColor, t);
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
