using UnityEngine;
using Zenject;

public class CheckPosition : MonoBehaviour
{

    MovementController _cat;

    [Inject]
    public void Construct(MovementController cat)
    {
        _cat = cat;
    }

    void LateUpdate()
    {
        transform.position = new Vector3(_cat.Cat.transform.position.x, transform.position.y, _cat.Cat.transform.position.z);
    }
}
