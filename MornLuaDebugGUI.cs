#if USE_LUA && UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lua.Unity;
using UnityEditor;
using UnityEngine;

namespace MornLib
{
    /// <summary>EditorOnly: 全LuaAssetを自動リストアップし、MornDebugにボタンで再生メニューを登録する</summary>
    public sealed class MornLuaDebugGUI : MonoBehaviour
    {
        [SerializeField] private string _menuKey = "ノベルテスト/ノベル一覧";
        private readonly List<LuaAsset> _luaAssets = new();

        private void Start()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LuaAsset));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<LuaAsset>(path);
                if (asset != null)
                {
                    _luaAssets.Add(asset);
                }
            }

            _luaAssets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            MornDebugCore.RegisterGUI(_menuKey, DrawGUI, this);
        }

        private void DrawGUI()
        {
            GUILayout.Label($"全 {_luaAssets.Count} 件");
            foreach (var asset in _luaAssets)
            {
                if (asset == null)
                {
                    continue;
                }

                if (GUILayout.Button(asset.name))
                {
                    MornLuaGlobal.PlayLuaAsync(asset).Forget();
                }
            }
        }
    }
}
#endif
