using System;

public class UI_DailyRewardsPanel : UI_Panel
{
    public Action PanelOpened;

    protected override void OpenPanel()
    {
        base.OpenPanel();

        PanelOpened?.Invoke();
    }
}
