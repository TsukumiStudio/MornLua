using UnityEngine;

namespace MornLib
{
    [CreateAssetMenu(fileName = nameof(MornLuaGlobal), menuName = "Morn/" + nameof(MornLuaGlobal))]
    public sealed class MornLuaGlobal : MornGlobalBase<MornLuaGlobal>
    {
        protected override string ModuleName => "MornLua";
    }
}