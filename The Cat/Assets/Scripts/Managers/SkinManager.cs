using System;
using UnityEngine;

public class SkinManager : MonoBehaviour
{
    [Serializable]
    public class SkinAvailability
    {
        public PlayerVisualModel m_VisualModel;
        public bool isPurchased;
    }

    public Action<PlayerVisualModel> SkinChanged;

    [SerializeField] private PlayerVisualModel m_defaultVisualModels;
    [SerializeField] private SkinAvailability[] m_SkinsData;
    public SkinAvailability[] SkinsData => m_SkinsData;

    private string _currentSkinFilename = "playerCurrentSkin.dat";
    private string _allSkinsFilename = "allSkins.dat";

    private PlayerVisualModel _currentVisualModel;
    public PlayerVisualModel CurrentVisualModel => _currentVisualModel;

    private void Start()
    {
        LoadData();

        if (_currentVisualModel == null)
        {
            _currentVisualModel = m_defaultVisualModels;
        }

        ChangePlayerVisualModel(_currentVisualModel);
    }

    public void ChangePlayerVisualModel(PlayerVisualModel visualModel)
    {
        _currentVisualModel = visualModel;

        SaveCurrentModel();

        SkinChanged?.Invoke(visualModel);
    }

    private void LoadData()
    {
        Saver<PlayerVisualModel>.TryLoad(_currentSkinFilename, ref _currentVisualModel);
        Saver<SkinAvailability[]>.TryLoad(_allSkinsFilename, ref m_SkinsData);
    }

    private void SaveCurrentModel()
    {
        Saver<PlayerVisualModel>.Save(_currentSkinFilename, _currentVisualModel);
    }

    public void SaveSkinsData()
    {
        Saver<SkinAvailability[]>.Save(_allSkinsFilename, m_SkinsData);
    }

    public void ResetData()
    {
        for (int i = 1; i < m_SkinsData.Length; i++)
        {
            m_SkinsData[i].isPurchased = false;
        }

        SaveSkinsData();
    }
}