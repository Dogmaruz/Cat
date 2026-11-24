using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class WardrobeCatalogCuration20261105
    {
        public static void RemoveDiadem()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Curate the catalog in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog == null) throw new InvalidDataException("Courier catalog is missing.");
            int before = catalog.equipment.Length;
            catalog.equipment = catalog.equipment.Where(e => e.id != "hat_SignalDiadem").ToArray();
            if (catalog.equipment.Count(e => e.kind == CourierEquipmentKind.Hat) != 7)
                throw new InvalidDataException("Expected seven retained headwear styles.");
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("WARDROBE_CATALOG: removed " + (before-catalog.equipment.Length) + " diadem entry; original art retained as an unused source.");
        }
    }
}
