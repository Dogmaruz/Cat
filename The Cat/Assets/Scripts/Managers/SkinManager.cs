using System;
using System.Collections.Generic;
using UnityEngine;

public class SkinManager : MonoBehaviour
{
    [Serializable]
    public class SkinAvailability
    {
        public int SkinIndex;
        public bool IsPurchased;

        public SkinAvailability(int index)
        {
            SkinIndex = index;
        }
    }

    public Action<int> SkinChanged;

    private List<SkinAvailability> _skinsData;
    public List<SkinAvailability> SkinsData => _skinsData;

    private string _currentSkinFilename = "playerCurrentSkin.dat";
    private string _allSkinsFilename = "allSkins.dat";

    private int _currentSkinIndex = 0;
    public int CurrentSkinIndex => _currentSkinIndex;

    private void Start()
    {
        LoadData();

        if (_skinsData == null)
        {
            SetDefaultData();
        }

        ChangePlayerVisualModel(_currentSkinIndex);
    }

    public void ChangePlayerVisualModel(int index)
    {
        _currentSkinIndex = index;

        SaveCurrentSkinIndex();

        SkinChanged?.Invoke(_currentSkinIndex);
    }

    private void SetDefaultData()
    {
        _skinsData = new List<SkinAvailability>();

        var resources = Resources.LoadAll("PlayerSkins/Materials");
        int count = resources.Length;

        for (int i = 0; i < count; i++) 
        {
            _skinsData.Add(new SkinAvailability(i));
        }

        ResetData();
    }

    private void LoadData()
    {
        Saver<int>.TryLoad(_currentSkinFilename, ref _currentSkinIndex);
        Saver<List<SkinAvailability>>.TryLoad(_allSkinsFilename, ref _skinsData);
    }

    private void SaveCurrentSkinIndex()
    {
        Saver<int>.Save(_currentSkinFilename, _currentSkinIndex);
    }

    public void SaveSkinsData()
    {
        Saver<List<SkinAvailability>>.Save(_allSkinsFilename, _skinsData);
    }

    public void ResetData()
    {
        foreach (var data in _skinsData)
        {
            data.IsPurchased = false;

            if (data.SkinIndex == 0)
            {
                data.IsPurchased = true;
            }
        }

        SaveSkinsData();
    }

    private void DeleteData()
    {
        FileHandler.Reset(_currentSkinFilename);
        FileHandler.Reset(_allSkinsFilename);
    }
}