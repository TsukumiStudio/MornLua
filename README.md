# MornLua

## 概要

Luaスクリプト実行エンジンの統合ライブラリ。ゲームロジックをLuaで記述可能。

## 依存関係

| 種別 | 名前 |
|------|------|
| 外部パッケージ | Lua（USE_LUA定義時）, UniTask |
| Mornライブラリ | MornLib |

## 使い方

### 基本的な使用方法

```csharp
// MornLuaCoreインスタンス生成
var lua = new MornLuaCore();

// デフォルト関数が自動登録: print, warn, error, wait, coroutine

// カスタム関数の追加
lua.AddDefaultFunction("myFunc", new LuaFunction(...));
lua.AddDefaultModule("module_path");

// Luaスクリプト実行
await lua.DoStringAsync("print('Hello Lua')", ct);
await lua.DoFileAsync("path/to/script.lua", ct);
```

### 有効化

`USE_LUA` プリプロセッサシンボルを定義することで有効化されます。
