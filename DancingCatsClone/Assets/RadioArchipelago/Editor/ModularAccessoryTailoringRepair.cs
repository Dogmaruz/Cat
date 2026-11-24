using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Reproducible Editor-only repair and native review, without player builds.</summary>
    public static class ModularAccessoryTailoringRepair
    {
        public static void Quick()
        {
            ModularCourierAssetBuilder.SyncCorrections();
            ModularWardrobeChecks.Run();
            ModularTailoringReview.Minimal();
            Debug.Log("RADIO_ACCESSORY_TAILORING QUICK REVIEW COMPLETE");
        }

        public static void Final()
        {
            ModularWardrobeChecks.Run();
            ModularTailoringReview.Run();
            Debug.Log("RADIO_ACCESSORY_TAILORING FINAL REVIEW COMPLETE");
        }
    }
}
