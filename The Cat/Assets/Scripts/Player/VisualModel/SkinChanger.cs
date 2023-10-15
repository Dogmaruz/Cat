using UnityEngine;
using Zenject;

public class SkinChanger : MonoBehaviour
{
    private MovementController _movementController;

    private SkinManager _skinManager;

    [Inject]
    public void Construct(SkinManager skinManager)
    {
        _skinManager = skinManager;
    }

    private void Start()
    {
        _movementController = GetComponent<MovementController>();

        _skinManager.SkinChanged += OnSkinChanged;

        OnSkinChanged(_skinManager.CurrentVisualModel);
    }

    private void OnDestroy()
    {
        _skinManager.SkinChanged -= OnSkinChanged;
    }

    private void OnSkinChanged(PlayerVisualModel visualModel)
    {
        _movementController.PlayerTransform.GetComponent<MeshFilter>().mesh = visualModel.Mesh;
        _movementController.PlayerTransform.GetComponent<MeshRenderer>().material = visualModel.Material;
    }
}