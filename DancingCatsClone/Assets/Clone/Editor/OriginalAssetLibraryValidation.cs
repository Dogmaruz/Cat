using System;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class OriginalAssetLibraryValidation
    {
        public static void Run()
        {
            var library = Resources.Load<OriginalAssets>("OriginalAssets");
            if (library == null) throw new Exception("ORIGINAL_LIBRARY FAIL: missing generated library");
            var config = OriginalProgressionConfig.Load();
            var required = config.roles.Concat(config.trails).Select(r => r.headName)
                .Concat(config.dailyRewards.Select(r => r.headName))
                .Concat(config.loginRewardSets.SelectMany(s => s.Days).Select(r => r.headName))
                .Concat(config.houseRows.SelectMany(r => r.icon ?? Array.Empty<string>()))
                .Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            var missing = required.Where(n => library.Sprite(n) == null).ToArray();
            if (missing.Length > 0) throw new Exception("ORIGINAL_LIBRARY FAIL: missing original UI sprites: " + string.Join(", ", missing));
            if (library.sprites.Length < 595) throw new Exception("ORIGINAL_LIBRARY FAIL: standalone recovered Sprite assets were excluded");
            Debug.Log("ORIGINAL_LIBRARY PASS: " + library.sprites.Length + " sprites, " + required.Length + " configured cat/trail/reward/furniture heads resolve");
        }
    }
}
