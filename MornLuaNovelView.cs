#if USE_LUA
using TMPro;
using UnityEngine;

namespace MornLib
{
    public sealed class MornLuaNovelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _speakerText;
        [SerializeField] private TMP_Text _dialogueText;

        public void SetMessage(string speakerName, string text)
        {
            _speakerText.text = speakerName;
            _dialogueText.text = text;
        }

        public void Clear()
        {
            _speakerText.text = string.Empty;
            _dialogueText.text = string.Empty;
        }
    }
}
#endif
