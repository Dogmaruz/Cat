using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class VisualModel : MonoBehaviour
{
    [SerializeField] private PlayerVisualModel m_visualModel;
    [SerializeField] private Image m_image;

    private UI_CharactersPanel _charactersPanel;

    private Button _button;

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

        // TODO Add images;
        //m_image.sprite = m_visualModel.Sprite;
        //m_image.color = Color.white;
        
    }

    private void ChangeModel()
    {
        _skinManager.ChangePlayerVisualModel(m_visualModel);
        _charactersPanel.CloseCharactersPanel();
    }
}