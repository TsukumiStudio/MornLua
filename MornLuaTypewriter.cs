using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MornLib
{
    /// <summary>
    /// 1文字ずつ時間送りで文字列を表示するための共通モジュール。
    /// MornLuaNovelRunner やプロジェクト独自の文字送り処理から利用される。
    /// </summary>
    public static class MornLuaTypewriter
    {
        /// <summary>
        /// 文字が現れた瞬間に呼ばれるコールバック。
        /// </summary>
        /// <param name="visibleCount">現在表示中の文字数 (1始まり、0は空表示)。</param>
        /// <param name="fastForward">スキップによる一括反映なら true。SEを鳴らさないなどの分岐に使う。</param>
        public delegate void OnRevealCallback(int visibleCount, bool fastForward);

        /// <summary>
        /// 1文字ずつ <paramref name="text"/> を表示する。
        /// スキップは1文字ごとの待機中もフレーム単位でチェックされる。
        /// </summary>
        /// <param name="text">表示する文字列。</param>
        /// <param name="onReveal">表示が更新されたタイミングで呼ばれるコールバック。</param>
        /// <param name="getInterval">次の文字までの待機秒数を返す。文字ごとに変えたい場合に利用。</param>
        /// <param name="shouldSkip">true を返すと残りを一括表示してリターン。null なら最後まで再生。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public static async UniTask PlayAsync(
            string text,
            OnRevealCallback onReveal,
            Func<char, float> getInterval,
            Func<bool> shouldSkip = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                onReveal?.Invoke(0, false);
                return;
            }

            for (var i = 0; i < text.Length; i++)
            {
                onReveal?.Invoke(i + 1, false);

                if (shouldSkip != null && shouldSkip())
                {
                    if (i + 1 < text.Length)
                    {
                        onReveal?.Invoke(text.Length, true);
                    }

                    return;
                }

                if (i < text.Length - 1)
                {
                    var delay = getInterval?.Invoke(text[i]) ?? 0f;
                    if (await WaitOrSkipAsync(delay, shouldSkip, ct))
                    {
                        if (i + 1 < text.Length)
                        {
                            onReveal?.Invoke(text.Length, true);
                        }

                        return;
                    }
                }
            }
        }

        private static async UniTask<bool> WaitOrSkipAsync(float seconds, Func<bool> shouldSkip, CancellationToken ct)
        {
            if (seconds <= 0f)
            {
                return false;
            }

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (shouldSkip != null && shouldSkip())
                {
                    return true;
                }

                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            return false;
        }
    }
}
