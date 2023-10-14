using System;
using UnityEngine;

public class SkinManager : MonoBehaviour
{
    public Action<PlayerVisualModel> SkinChanged;

    [SerializeField] private PlayerVisualModel m_defaultVisualModels;

    private string filename = "playerSkin.dat";

    private PlayerVisualModel _currentVisualModel;
    public PlayerVisualModel CurrentVisualModel => _currentVisualModel;

    private void Awake()
    {
        LoadValue();
    }

    private void Start()
    {
        LoadValue();

        if (_currentVisualModel == null)
        {
            _currentVisualModel = m_defaultVisualModels;
        }

        ChangePlayerVisualModel(_currentVisualModel);
        
    }

    public void ChangePlayerVisualModel(PlayerVisualModel visualModel)
    {
        _currentVisualModel = visualModel;

        Save();

        SkinChanged?.Invoke(visualModel);
    }

    private void LoadValue()
    {
        Saver<PlayerVisualModel>.TryLoad(filename, ref _currentVisualModel);
    }

    private void Save()
    {
        Saver<PlayerVisualModel>.Save(filename, _currentVisualModel);
    }
}