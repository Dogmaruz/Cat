using UnityEngine;

namespace RadioArchipelago
{
    public interface IGamePresentation
    {
        Canvas UICanvas { get; }
        bool IsBlockingInput { get; }
        void Initialize();
        void OpenPanel(string name);
        void ClosePopup();
    }
}
