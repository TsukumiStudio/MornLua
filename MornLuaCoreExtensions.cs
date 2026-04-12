#if USE_LUA
using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lua;

namespace MornLib
{
    /// <summary>
    /// MornLuaCore に対する型付き関数登録ヘルパ。
    /// LuaFunctionExecutionContext / ValueTask&lt;int&gt; のボイラープレートを隠蔽し、
    /// プレーンなC#デリゲートで登録できるようにする。
    /// </summary>
    public static class MornLuaCoreExtensions
    {
        // ---------- 同期: Action ----------

        public static void RegisterAction(this MornLuaCore core, string name, Action action)
        {
            core.AddDefaultFunction(name, new LuaFunction((_, _2) =>
            {
                action?.Invoke();
                return new ValueTask<int>(0);
            }));
        }

        public static void RegisterAction<T1>(this MornLuaCore core, string name, Action<T1> action)
        {
            core.AddDefaultFunction(name, new LuaFunction((ctx, _) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                action?.Invoke(a1);
                return new ValueTask<int>(0);
            }));
        }

        public static void RegisterAction<T1, T2>(this MornLuaCore core, string name, Action<T1, T2> action)
        {
            core.AddDefaultFunction(name, new LuaFunction((ctx, _) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                var a2 = ctx.GetArgument<T2>(1);
                action?.Invoke(a1, a2);
                return new ValueTask<int>(0);
            }));
        }

        public static void RegisterAction<T1, T2, T3>(this MornLuaCore core, string name, Action<T1, T2, T3> action)
        {
            core.AddDefaultFunction(name, new LuaFunction((ctx, _) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                var a2 = ctx.GetArgument<T2>(1);
                var a3 = ctx.GetArgument<T3>(2);
                action?.Invoke(a1, a2, a3);
                return new ValueTask<int>(0);
            }));
        }

        // ---------- 非同期: Func<...,CancellationToken,UniTask> ----------

        public static void RegisterAsync(this MornLuaCore core, string name, Func<CancellationToken, UniTask> handler)
        {
            core.AddDefaultFunction(name, new LuaFunction(async (_, ct) =>
            {
                if (handler != null)
                {
                    await handler(ct);
                }

                return 0;
            }));
        }

        public static void RegisterAsync<T1>(this MornLuaCore core, string name, Func<T1, CancellationToken, UniTask> handler)
        {
            core.AddDefaultFunction(name, new LuaFunction(async (ctx, ct) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                if (handler != null)
                {
                    await handler(a1, ct);
                }

                return 0;
            }));
        }

        public static void RegisterAsync<T1, T2>(this MornLuaCore core, string name, Func<T1, T2, CancellationToken, UniTask> handler)
        {
            core.AddDefaultFunction(name, new LuaFunction(async (ctx, ct) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                var a2 = ctx.GetArgument<T2>(1);
                if (handler != null)
                {
                    await handler(a1, a2, ct);
                }

                return 0;
            }));
        }

        public static void RegisterAsync<T1, T2, T3>(this MornLuaCore core, string name, Func<T1, T2, T3, CancellationToken, UniTask> handler)
        {
            core.AddDefaultFunction(name, new LuaFunction(async (ctx, ct) =>
            {
                var a1 = ctx.GetArgument<T1>(0);
                var a2 = ctx.GetArgument<T2>(1);
                var a3 = ctx.GetArgument<T3>(2);
                if (handler != null)
                {
                    await handler(a1, a2, a3, ct);
                }

                return 0;
            }));
        }
    }
}
#endif
