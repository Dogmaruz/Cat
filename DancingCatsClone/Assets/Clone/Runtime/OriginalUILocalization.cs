using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace DancingCats.Clone
{
    /// <summary>Original LanguageController text IDs recovered from serialized prefab pointers.</summary>
    public sealed class OriginalUILocalization : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public Row[] items; }
        [Serializable] private sealed class Row
        {
            public int id;
            public string English, Russian, Vietnamese, Indonesian, Portuguese, Spanish, Japanese,
                Korean, German, Italian, French, Turkish, Arabic;
            public string InLanguage(int language)
            {
                switch (language)
                {
                    case 1: return Russian; case 2: return Vietnamese; case 3: return Indonesian;
                    case 4: return Portuguese; case 5: return Spanish; case 6: return Japanese;
                    case 7: return Korean; case 8: return German; case 9: return Italian;
                    case 10: return French; case 11: return Turkish; case 12: return Arabic;
                    default: return English;
                }
            }
        }
        private sealed class Binding { public string path; public int id; }
        private static Dictionary<int, Row> catalog;
        private static Dictionary<string, List<Binding>> bindings;
        [SerializeField] private int textId, language;
        [SerializeField] private string sourceValue;
        [SerializeField] private bool dynamicValue;
        private Text target;

        public static void Bind(GameObject root, string prefabName, int language)
        {
            Load();
            if (!bindings.TryGetValue(prefabName, out var entries)) return;
            foreach (var entry in entries)
            {
                var node = string.IsNullOrEmpty(entry.path) ? root.transform : root.transform.Find(entry.path);
                var text = node != null ? node.GetComponent<Text>() : null;
                if (text == null) throw new InvalidOperationException("Original localized Text is missing: " + prefabName + "/" + entry.path);
                var label = text.GetComponent<OriginalUILocalization>() ?? text.gameObject.AddComponent<OriginalUILocalization>();
                label.target = text; label.textId = entry.id; label.language = Mathf.Clamp(language, 0, 12);
                label.sourceValue = text.text; label.dynamicValue = false; label.Refresh();
            }
        }

        public static void Apply(GameObject root, int language)
        {
            foreach (var label in root.GetComponentsInChildren<OriginalUILocalization>(true))
            {
                label.language = Mathf.Clamp(language, 0, 12); label.Refresh();
            }
        }

        public static void SetText(Text text, string value)
        {
            value = value ?? string.Empty;
            var label = text.GetComponent<OriginalUILocalization>();
            if (label == null) { text.text = value; return; }
            Load();
            bool known = catalog.TryGetValue(label.textId, out var row);
            label.dynamicValue = !string.Equals(value, label.sourceValue, StringComparison.Ordinal) &&
                (!known || !string.Equals(value.Trim(), row.English?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (label.dynamicValue) text.text = value; else label.Refresh();
        }

        private void Refresh()
        {
            if (dynamicValue) return;
            Load();
            if (target == null) target = GetComponent<Text>();
            if (target == null || !catalog.TryGetValue(textId, out var row)) return;
            string value = row.InLanguage(language);
            target.text = string.IsNullOrEmpty(value) ? row.English : value;
        }

        private static void Load()
        {
            if (catalog != null) return;
            var resource = Resources.Load<TextAsset>("json/language_table");
            if (resource == null) throw new InvalidOperationException("Original language table is missing.");
            // JsonUtility needs an array; remove only the numeric keys of the native root dictionary.
            string json = Regex.Replace(resource.text.Trim(), @"(?<=[{,])\s*""\d+""\s*:\s*(?=\{)", string.Empty);
            var data = JsonUtility.FromJson<Catalog>("{\"items\":[" + json.Substring(1, json.Length - 2) + "]}");
            catalog = new Dictionary<int, Row>();
            foreach (var row in data.items) catalog.Add(row.id, row);
            bindings = new Dictionary<string, List<Binding>>(StringComparer.Ordinal);
            foreach (string line in BindingData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.TrimEnd('\r').Split('|');
                if (!bindings.TryGetValue(parts[0], out var list)) bindings.Add(parts[0], list = new List<Binding>());
                list.Add(new Binding { path = parts[1], id = int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) });
            }
        }

        private const string BindingData = @"BuildLoading|BG/Text|124
CatHouseBuilding_UIPanel|UIListView_CatHouseBuilding/Content/Top/Image (1)/Text|130
CatHouseBuilding_UIPanel|UIListView_CatHouseBuilding/Content/4/Image/Text|129
CatHouseBuilding_UIPanel|UIListView_CatHouseBuilding/Content/3/Image/Text|128
CatHouseBuilding_UIPanel|UIListView_CatHouseBuilding/Content/2/Image/Text|127
CatHouseBuilding_UIPanel|UIListView_CatHouseBuilding/Content/1/Image/Text|126
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_1/GoldButton/GoldTips/Text|97
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_1/AdButton/AdTips/Text|96
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_1/FreeButton/Text|137
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_2/GoldButton/GoldTips/Text|97
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_2/AdButton/Text|134
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_2/AdButton/AdTips/Text|96
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_2/FreeButton/Text|137
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_3/GoldButton/GoldTips/Text|97
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_3/AdButton/Text|134
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_3/AdButton/AdTips/Text|96
CatHouseBuilding_UIPanel|Down/Image/Parent/DownItem_3/FreeButton/Text|137
CatHouseBuilding_UIPanel|RoleList/Image/UIListView__RoleList/Content/CatsItem/SelectButton/Text|137
CatHouseBuilding_UIPanel|RoleList/Image/UIListView__RoleList/Content/CatsItem/AdButton/Tips/Text|96
CatHouseBuilding_UIPanel|RoleList/Image/UIListView__RoleList/Content/CatsItem/GoldButton/Tips (1)/Text|97
CatHouseBuilding_UIPanel|RoleList/Image/Lock/Text|140
CatHouseBuilding_UIPanel|RoleList/Image/Lock/GoldUnlockButton/Tips/Text|97
CatHouseBuilding_UIPanel|RoleList/Image/Lock/AdUnlockButton/Tips/Text|96
ChallengeLevel_UIPanel|BG/Text (1)|65
ChallengeLevel_UIPanel|BG/Title/Title/Text (1)|64
ChallengeLevel_UIPanel|BG/Title/TitleOver/Text (1)|67
ChallengeLevel_UIPanel|BG/Win/CloseButton (1)/Text|61
ChallengeLevel_UIPanel|BG/Win/AdButton (1)/Text|62
ChallengeLevel_UIPanel|BG/Win/AdButton (1)/Tips/Text|96
ChallengeLevel_UIPanel|BG/Win/OverCloseButton/Text|63
ChallengeLevel_UIPanel|BG/Start/CloseButton/Text|63
ChallengeLevel_UIPanel|BG/Start/AdButton/Text|66
ChallengeLevel_UIPanel|BG/Start/AdButton/Tips/Text|96
ChallengeLevel_UIPanel|BG/Start/Start/Text|66
ChallengeLevel_UIPanel|GET/Crystal/Crystal/Title/Title (1)/Text (1)|67
ChallengeLevel_UIPanel|GET/Crystal/DoubleRewardButton/Text|62
ChallengeLevel_UIPanel|GET/Crystal/DoubleRewardButton/Tips/Text|96
ChallengeLevel_UIPanel|GET/Crystal/GetButton/Text|61
DailyReward_UIPanel|Animation/BG/CloseButton/CloseButton (1)/Text|37
DailyReward_UIPanel|Animation/BG/PlayVideoButton/Text|35
DailyReward_UIPanel|Animation/BG/PlayVideoButton/Tips/Text|96
DailyReward_UIPanel|Animation/BG/Text (1)|34
DailyReward_UIPanel|Animation/BG/OverText|36
DailyReward_UIPanel|Animation/BG/Title/Text (1)|33
Death_UIPanel|BG/CAT/Text (2)|113
Death_UIPanel|BG/Continue/ContinueButton/Text|80
Death_UIPanel|BG/Continue/ContinueButton/Text (1)|94
Death_UIPanel|BG/Continue/ContinueButton/ContinueTips/Text|96
Death_UIPanel|BG/Guide/CAT/Text (3)|113
Death_UIPanel|BG/Guide/Continue/ContinueButton/Text|35
Death_UIPanel|BG/Guide/Continue/ContinueButton/ContinueTips/Text|96
Death_UIPanel|BG/Guide/Continue/ContinueButton/ContinueTips/Text|96
Death_UIPanel|BG/NoThanksButton/Text|37
Gold_UIPanel|BG/Image/Text (1)|50
Gold_UIPanel|BG/GetButton/Text|51
Gold_UIPanel|BG/GetButton/Tips/Text|96
GuideHolder|GuideOne/Image (2)/Text|119
GuideHolder|GuideTwo/Image (2)/Text|131
GuideHolder|GuideThree/Image (2)/Text|132
GuideHolder|GuideFour/Image (2)/Text|135
GuideHolder|GuideFive/Image (2)/Text|136
GuideHolder|GuideSix/GameObject/Image (2)/Text|138
GuideHolder|GuideSix/GameObject/Button/Text|139
InternalStart_UIPanel|Animation/BG_ 2/Image (1)/Expert/Text (1)|109
InternalStart_UIPanel|Animation/BG_ 2/Image (1)/Hard/Text (1)|110
InternalStart_UIPanel|Animation/BG_ 2/Image (1)/Normal/Text (1)|111
InternalStart_UIPanel|Animation/PlayButton/Text|6
InternalStart_UIPanel|L/ActivitesButton/Tips/Text|96
InternalStart_UIPanel|L/UIToggleRoot_Pattern/Normal/Label|19
InternalStart_UIPanel|L/UIToggleRoot_Pattern/Endless/Label|20
InternalStart_UIPanel|L/MusicButton/Text|15
InternalStart_UIPanel|L/MusicButton/Point/Text (2)|16
InternalStart_UIPanel|R/ShopButton/Text|17
InternalStart_UIPanel|GuideInternalStart_UIPanel/Image/Text|7
InternalStart_UIPanel|GuideInternalStart_UIPanel/Image/Text (3)|9
InternalStart_UIPanel|GuideInternalStart_UIPanel/Image/GuideButton/Text|11
InternalStart_UIPanel|GuideInternalStart_UIPanel/GuideCloseButton/Text|11
LoginReward_UIPanel|Title/Text (3)|58
LoginReward_UIPanel|RewardButton/Text|61
LoginReward_UIPanel|DoubleRewardButton/Text|62
LoginReward_UIPanel|DoubleRewardButton/Tips/Text|96
LoginReward_UIPanel|CloseButton/Text|63
LoginReward_UIPanel|LoginRewardItem_Role/Image/Text (2)|60
MusicLevelItem|Difficulty/Hard/Text|110
MusicLevelItem|Difficulty/Expert/Text|109
MusicLevelItem|Lcok/Coin/Tips (1)/Text|97
MusicLevelItem|Lcok/AD/Tips/Text|96
MusicLevelItem|Unlock/Score/Text|85
MusicLevelItem|Tap/New/Text (1)|28
MusicLevelItem|Tap/Hot/Text (1)|27
MusicList_UIPanel|BG/SONGS|21
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Difficulty/Hard/Text|110
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Difficulty/Expert/Text|109
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Lcok/Coin/Tips (1)/Text|97
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Lcok/AD/Tips/Text|96
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Unlock/Score/Text|85
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Tap/New/Text (1)|28
MusicList_UIPanel|Scroller/placeholder/MusicLevelItem/Tap/Hot/Text (1)|27
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle/Label|22
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (5)/Label|23
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (1)/Label|24
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (6)/Label|25
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (2)/Label|26
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (3)/Label|27
MusicList_UIPanel|UIListView__MusicList/Content/UIToggleRoot_MusicList/UIToggle (4)/Label|28
NewRoleShop_UIPanel|BG/Tip/Text|142
NewRoleShop_UIPanel|BG/GridView_TopToBottom/Viewport/Content/RoleItem/SelectButton/Text|137
NewRoleShop_UIPanel|BG/GridView_TopToBottom/Viewport/Content/RoleItem/AdButton/Tips (1)/Text|96
NewRoleShop_UIPanel|BG/GridView_TopToBottom/Viewport/Content/RoleItem/GoldButton/Tips/Text|97
NewRoleShop_UIPanel|BG/GridView_TopToBottom/Viewport/Content/RoleItem/Text (2)|16
NewRoleShop_UIPanel|BG/UIToggleRoot_RoleList/UIToggle_Role/Label|22
NewRoleShop_UIPanel|BG/UIToggleRoot_RoleList/UIToggle_Trailing/Label|22
NewSkin_UIPanel|NextLevel/Text|90
NewSkin_UIPanel|NoThanks/Text|92
NewSkin_UIPanel|Unlock/Text|91
NewSkin_UIPanel|Unlock/UnlockTap/Text|96
NewSkin_UIPanel|Image/Text (1)|88
NewSkin_UIPanel|AD/AdTap/Text|96
PlayDice_UIPanel|Button_Bg/Text|57
PlayDice_UIPanel|Button_Bg/FreeButton/Image (1)/Text (2)|52
PlayDice_UIPanel|Button_Bg/AdButton/Tips/Text|96
PlayDice_UIPanel|Button_Bg/AdButton/Roll/Text (2)|52
PlayDice_UIPanel|RoleUI/BG/Title/Text (2)|54
PlayDice_UIPanel|RoleUI/Button/Image (3)/Text (2)|51
PlayDice_UIPanel|Coins/BG/Title/Text (2)|38
PlayDice_UIPanel|Coins/Button/Image (3)/Text (2)|51
Push_UIPanel|Image/Role/Text|54
Push_UIPanel|Image/AdButton/Text|39
Push_UIPanel|Image/AdButton/Tips/Text|96
Push_UIPanel|Image/Title/Text (1)|38
RateUs|BG/Title/Image/Text (1)|72
RateUs|BG/Text|73
RateUs|BG/NOT NOW/Text|74
RateUs|BG/SUBMIT/Text|75
RateUs|GET/Crystal/DoubleRewardButton/Tips/Text|96
RateUs|GET/Crystal/GetButton/Text|61
Settlement_UIPanel|UIListView/Cell/Difficulty/Hard/Text|110
Settlement_UIPanel|UIListView/Cell/Difficulty/Expert/Text|109
Settlement_UIPanel|UIListView/Cell/Lcok/Coin/Tips (1)/Text|97
Settlement_UIPanel|UIListView/Cell/Lcok/AD/Tips/Text|96
Settlement_UIPanel|UIListView/Cell/Unlock/Score/Text|85
Settlement_UIPanel|UIListView/Cell/Tap/New/Text (1)|28
Settlement_UIPanel|UIListView/Cell/Tap/Hot/Text (1)|27
Settlement_UIPanel|MoveContens/Image/Best/Best/Text (1)|86
Settlement_UIPanel|MoveContens/Image/Text (1)|85
Settlement_UIPanel|Button/Text|95
Set_UIPanel|BG/BG/Image/Text|40
Set_UIPanel|BG/BG/GameObject/MusicButton/Icon/Text|41
Set_UIPanel|BG/BG/GameObject/VoiceButton/Icon/Text|42
Set_UIPanel|BG/BG/GameObject/ShockButton/Icon/Text|43
Set_UIPanel|BG/BG/GameObject/PostButton/Icon/Text|44
Set_UIPanel|BG/BG/GameObject/PostButton/On/Image (1)/Text|45
Set_UIPanel|BG/BG/GameObject/PostButton/On/Image (1)/Text (1)|46
Set_UIPanel|BG/BG/GameObject/PostButton/Off/Image (1)/Text (2)|46
Set_UIPanel|BG/BG/GameObject/PostButton/Off/Image (1)/Text (3)|45
Set_UIPanel|BG/BG/GameObject/Language/Icon/Text|112
Stop_UIPanel|StopWindow/Text|78
Stop_UIPanel|StopWindow/Quit/Text|79
Stop_UIPanel|StopWindow/Resume/Text|80
Stop_UIPanel|PromptWindow/BG/No/Text|83
Stop_UIPanel|PromptWindow/BG/Yes/Text|84
Stop_UIPanel|PromptWindow/BG/Text_1|81
Stop_UIPanel|PromptWindow/BG/Text_2|82
MusicSettings|BG/Text|47
MusicSettings|BG/Text (1)|48
MusicSettings|Ctrl/Back/Text|49";
    }
}
