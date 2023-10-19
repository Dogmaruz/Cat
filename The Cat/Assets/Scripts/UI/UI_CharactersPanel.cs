using System;

public class UI_CharactersPanel : UI_Panel
{
    public Action PanelOpened;

    /*
    [SerializeField] private ImpactEffect m_NotEnoughCoinsEffect;
    public ImpactEffect NotEnoughCoinsEffect => m_NotEnoughCoinsEffect;
    */
    

    protected override void OpenPanel()
    {
        base.OpenPanel();

        PanelOpened?.Invoke();
    }
}