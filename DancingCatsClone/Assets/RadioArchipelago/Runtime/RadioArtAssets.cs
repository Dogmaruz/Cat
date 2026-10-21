using UnityEngine;

namespace RadioArchipelago
{
    [CreateAssetMenu(menuName = "Radio Archipelago/Interface art")]
    public sealed class RadioArtAssets : ScriptableObject
    {
        public Sprite menuBackground;
        public Sprite brandLogo;
        public Font font;
        public Sprite settingsIcon, mapIcon, workshopIcon, playIcon, previousIcon, nextIcon;
        public Sprite closeIcon, pauseIcon, soundIcon, radioIcon;
        public Sprite roundedPanel, roundedButton;
    }
}
