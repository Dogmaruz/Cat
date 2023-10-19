using UnityEngine;

public class UI_SmthNewObject : MonoBehaviour
{
    public void SetAvailability(bool state)
    {
        gameObject.SetActive(state);
    }
}