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

        OnSkinChanged(_skinManager.CurrentSkinIndex);
    }

    private void OnDestroy()
    {
        _skinManager.SkinChanged -= OnSkinChanged;
    }

    private void OnSkinChanged(int index)
    {
        // TODO Add meshes to Resources
        //_movementController.PlayerTransform.GetComponent<MeshFilter>().mesh = Resources.Load<Mesh>("PlayerSkins/Meshes/Skin_mesh_" + index);
        _movementController.PlayerTransform.GetComponent<MeshRenderer>().material = Resources.Load<Material>("PlayerSkins/Materials/Skin_mat_" + index);
    }
}