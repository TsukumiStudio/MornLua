#if USE_LUA
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MornLib
{
    /// <summary>
    /// 吹き出し Prefab のルートに付与するコンポーネント。
    /// 名前 / セリフ / 文字送りアイコン / CanvasGroup を公開し、Player から差し込まれる。
    /// 文字送り待機中は AdvanceIcon が MornLuaGlobal.AdvanceIconBlinkPeriod 秒周期で点滅する。
    /// </summary>
    public sealed class MornLuaNovelBubble : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Image _advanceIcon;
        private bool _isAdvanceIconVisible;

        private void Awake()
        {
            var font = MornLuaGlobal.I.DefaultFont;
            if (font != null)
            {
                _nameText.font = font;
                _messageText.font = font;
            }

            _canvasGroup.alpha = 1f;
            Clear();
        }

        private void Update()
        {
            if (!_isAdvanceIconVisible)
            {
                return;
            }

            var period = MornLuaGlobal.I.AdvanceIconBlinkPeriod;
            if (period <= 0f)
            {
                return;
            }

            var phase = Mathf.Repeat(Time.time, period) / period;
            var alpha = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);
            var c = _advanceIcon.color;
            c.a = alpha;
            _advanceIcon.color = c;
        }

        public void SetMessage(string speakerName, string text)
        {
            _nameText.text = speakerName;
            _messageText.text = text;
        }

        public void SetAdvanceIconVisible(bool visible)
        {
            _isAdvanceIconVisible = visible;
            _advanceIcon.enabled = visible;
            if (!visible)
            {
                var c = _advanceIcon.color;
                c.a = 1f;
                _advanceIcon.color = c;
            }
        }

        public void Clear()
        {
            _nameText.text = string.Empty;
            _messageText.text = string.Empty;
            SetAdvanceIconVisible(false);
        }

        /// <summary>duration 秒かけて CanvasGroup を alpha 0 にフェードアウトする。</summary>
        public async UniTask HideAsync(float duration, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            var startAlpha = _canvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(ct);
            }

            _canvasGroup.alpha = 0f;
        }
    }
}
#endif
