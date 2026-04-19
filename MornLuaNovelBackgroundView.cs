#if USE_LUA
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MornLib
{
    public sealed class MornLuaNovelBackgroundView : MonoBehaviour
    {
        [SerializeField] private Image _imageA;
        [SerializeField] private Image _imageB;
        private Image _current;
        private Image _next;

        public void Clear()
        {
            _current = _imageA;
            _next = _imageB;
            _imageA.sprite = null;
            _imageB.sprite = null;
            _imageA.color = new Color(1f, 1f, 1f, 0f);
            _imageB.color = new Color(1f, 1f, 1f, 0f);
            _imageA.enabled = false;
            _imageB.enabled = false;
        }

        public async UniTask SetBackgroundAsync(Sprite sprite, float duration, CancellationToken ct)
        {
            if (_current == null || _next == null)
            {
                Clear();
            }

            _next.sprite = sprite;
            _next.transform.SetAsLastSibling();
            _next.enabled = true;
            _next.color = new Color(1f, 1f, 1f, 0f);
            if (duration <= 0f)
            {
                _next.color = Color.white;
                _current.enabled = false;
            }
            else
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    _next.color = new Color(1f, 1f, 1f, t);
                    await UniTask.Yield(ct);
                }

                _next.color = Color.white;
                _current.enabled = false;
            }

            (_current, _next) = (_next, _current);
        }

        /// <summary>現在表示中の背景を duration 秒かけてフェードアウトする。0 で即時。</summary>
        public async UniTask HideAsync(float duration, CancellationToken ct)
        {
            if (_current == null || !_current.enabled)
            {
                Clear();
                return;
            }

            if (duration <= 0f)
            {
                Clear();
                return;
            }

            var startAlpha = _current.color.a;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var c = _current.color;
                c.a = Mathf.Lerp(startAlpha, 0f, t);
                _current.color = c;
                await UniTask.Yield(ct);
            }

            Clear();
        }
    }
}
#endif
