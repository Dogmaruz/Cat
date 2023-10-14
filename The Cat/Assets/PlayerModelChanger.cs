using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class PlayerModelChanger : MonoBehaviour
{
    [SerializeField] private Image m_image;

    private UI_CharactersPanel _charactersPanel;

    private Button _button;
    private Color _color;

    private SkinManager _skinManager;

    [Inject]
    public void Construct(SkinManager skinManager)
    {
        _skinManager = skinManager;
    }

    private void Start()
    {
        _charactersPanel = transform.parent.parent.GetComponent<UI_CharactersPanel>(); //TODO Need refactoring!!!!

        _button = GetComponentInChildren<Button>();
        _button.onClick.AddListener(ChangeModel);

        _color = m_image.color;
    }

    private void ChangeModel()
    {
        _skinManager.ChangePlayerColor(_color);
        _charactersPanel.CloseCharactersPanel();
    }
}
