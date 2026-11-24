using System;

namespace RadioArchipelago.Editor
{
    /// <summary>Exact reviewed outputs of subsequent user-requested visual changes.</summary>
    internal static class IslandPreservationRevisions
    {
        private sealed class Revision
        {
            public readonly string Path, Before, After;
            public Revision(string path, string before, string after) { Path = path; Before = before; After = after; }
        }

        // Original receipts remain immutable. Both path and original digest must
        // match, and only this exact replacement digest is accepted.
        private static readonly Revision[] Revisions = {
            new Revision("Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss/emerald_abyss_island.fbx",
                "787658B95388A6DA6B8171496A92704F968BFD83AE9CACE2A119875BF1FCF204",
                "82B58962E8F81B165E3192A32BBE1AF81852CA3B2804870824AE0F4B6DF32BDD"),
            new Revision("Assets/RadioArchipelago/Content/Islands/EmeraldAbyss/RA_Emerald_Water.mat",
                "AED669B3F0BB2CB960B5E7586E0465EC2B1B7AC7B91D2AD64BF3835BBCCC299C",
                "CF2D5CD7BD529C46B7B0FD46F1CC46F322E6D95AB7E400B0F7A2619D64A335F9"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/RA_Coral_Foam.mat",
                "1D3FFDB7D7DCB64A25B2652A47491E94528C5CB15DF74959E38AEFB0E8CF8413",
                "33E735CB8E214B99BA27B3E8AF6E96C6BA4CB2E62BFE11D51E0FE804478892AC"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/RA_Coral_Water.mat",
                "FCE1F4ACFB753D5B06DC4687DF08689D7380C03DA24005EAB72CF5BBAD90D290",
                "2744C1F5619A27F9ABE06137A4D182C4F8BCA4702F4FD1DE8B8948B8A68448AB"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/RA_Coral_Waterfall.mat",
                "60213E678294E845607EE5BC82AF91B1F0C8C9F850AE24F8B8542AA58AF0FC61",
                "02A6B98C7EBD20485DA5310CBFDD16E732E2E19C6B1F1D5364E00F1E237D6917"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx",
                "4CC6A188004F508AE440E79A69BF80E3E1488ED45768F82B30871BE2B2A713FC",
                "6FB98555CB33402282D55F41EEB7E064C4D812D72083A999D85D7C478C9DA7B2"),
            new Revision("Assets/RadioArchipelago/Content/Islands/EmeraldAbyss/EmeraldAbyss.asset",
                "79C73B90AED32602DBBCDA83128BE8F12E4CF5497071F934911A152AD9DD6B45",
                "9CCFEA42DACFE3722CE9DC4BBDE82AC98EBFBC5684E56E1978DCFB5DB8DE8D2B"),
            new Revision("Assets/RadioArchipelago/Content/Islands/EmeraldAbyss/EmeraldAbyss.prefab",
                "68E1C2E464C9B3E29AC435E3ECAFE476C30FE8FEE9F1428CAC6588A9C56678A0",
                "4BD27C9335583170C214767115FF30D920CA0ECD860D785E175286FA96D3658D"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset",
                "82571CD553D540543DA4C5E93B0E594C3D2582A2D7FB3C07205DEBBEB687EEDA",
                "A40C507D10458FA8B2581E54BDF15251ED1A1BF3F1D83210CE7BCE2214C39858"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.prefab",
                "B130EC6AA3273EA74953196D394D8B588E27B302DB322A7258A438726A08B8FD",
                "8BCD0678637FDF834B3648BE11E9DE41EE1639B653FD18ACBAD99A243EDF8436"),
            new Revision("Assets/RadioArchipelago/Content/Islands/AmberWinds/AmberWinds.asset",
                "A8D2D2B8963F06C14078394B1F978CC395C0FF636E7D501323F6A0A194CA168B",
                "5E2AE06D279E5F6ACFBE9BDCB6DCC274F1C76122691ED9AA8B6A906485AC7F37"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset",
                "A40C507D10458FA8B2581E54BDF15251ED1A1BF3F1D83210CE7BCE2214C39858",
                "8D26E5719AE33CEC85A0AFA5368388E375E013A235D9F3E7AF11161EEEE3E4D9"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.prefab",
                "8BCD0678637FDF834B3648BE11E9DE41EE1639B653FD18ACBAD99A243EDF8436",
                "AD902479AD4966C7E0DFC2610D6CEB396111CEDB3F33E0F8EA7E1FF94675D5E1"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/RA_Coral_Waterfall.mat",
                "02A6B98C7EBD20485DA5310CBFDD16E732E2E19C6B1F1D5364E00F1E237D6917",
                "837691E6C67B665265DCB849FEE4E801CA1B854CA83118B9EBB4579B4D94FF13"),
            new Revision("Assets/RadioArchipelago/Content/Islands/MoonResonance/MoonResonance.asset",
                "FC29EBB9EA3E8286D099A69E68E4BE9D31CB128627EB757AC2EBA7485CD03109",
                "5A0BA9D7021E849ADF3EF2A8194B3F980DE8BAD450B5E0A03EC0FCE3677924CF"),
            new Revision("Assets/RadioArchipelago/Content/Islands/MoonResonance/MoonResonance.prefab",
                "90FD276E8D0B72BC17B19104EEB90D62E06E6E545193DE9AB6068AECBA57D785",
                "479FBA4AB09E5F66CBB33603D5BD6EE22EF3B2B6A0368EA1CDC7523DD01E423F"),
            new Revision("Assets/RadioArchipelago/Content/Islands/MoonResonance/RA_Moon_Waterfall.mat",
                "24D82FE5D1DAF334570BDBB1CC63B8B8D35C78B76D0244D80BBF786EB67A135C",
                "8FC2162806B0D4342C00D83840A55EC5C7D379DC640514D8022B197E313E4602"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx",
                "6FB98555CB33402282D55F41EEB7E064C4D812D72083A999D85D7C478C9DA7B2",
                "7B5617164BDF969D41221D9A83C5FF6C288DC91795B7C41353079AE654344CE2"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/MoonResonance/moon_resonance_island.fbx",
                "C2407E489E3EC0E85322E66897E8379F3828B143D09111781DF8944FC38912BE",
                "FBA3247C094803B1A51B519E528AFC3B3B1B139B40BF728320B4EF1941CD9C5F"),
            new Revision("Assets/RadioArchipelago/Content/Islands/AmberWinds/AmberWinds.prefab",
                "937FA0EDA98A3207B179F86D1BC386BC9E906E6FE91E34A6D7A1A220F56D1CB4",
                "3CBE03C8F1DAA7670301DCF7897EEE474060AB0037B07B2962CB9A4F99DC6CEF"),
            new Revision("Assets/RadioArchipelago/Content/Islands/AmberWinds/RA_Amber_Waterfall.mat",
                "F13D4EC8F6A1816EDECD7A17F57ECEE1ADD4B3BCF2C7D417E03E8F13B335CB86",
                "5CF74C4C5BDB073288F6B1CE720B699751D2C5BEB0FCF34DFA4EBBF37D742921"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.prefab",
                "AD902479AD4966C7E0DFC2610D6CEB396111CEDB3F33E0F8EA7E1FF94675D5E1",
                "E456A5BDF63BEA5DAC9EE91E9D71ADF1F34F5368FB646EDF2582FA20140425AC"),
            new Revision("Assets/RadioArchipelago/Content/Islands/CoralOasis/RA_Coral_Waterfall.mat",
                "837691E6C67B665265DCB849FEE4E801CA1B854CA83118B9EBB4579B4D94FF13",
                "4FC1469788F63C3CF8E2BA77582B20A02F73B24656C27C9B32194E460D738810"),
            new Revision("Assets/RadioArchipelago/Content/Islands/MoonResonance/MoonResonance.prefab",
                "479FBA4AB09E5F66CBB33603D5BD6EE22EF3B2B6A0368EA1CDC7523DD01E423F",
                "07DE444D9794F098132DF238BE8185AC349674C2112DC1AF8CA30341964A9070"),
            new Revision("Assets/RadioArchipelago/Content/Islands/MoonResonance/RA_Moon_Waterfall.mat",
                "8FC2162806B0D4342C00D83840A55EC5C7D379DC640514D8022B197E313E4602",
                "EF6043A71463825B31635A7637865269503D8123D4A9852D4652598DC1D8B78B"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/AmberWinds/amber_winds_island.fbx",
                "18CFB754BA06D63A01A95C069FF5D7DBC5142E7415B42C6F19BF48187E45305F",
                "F49534C8E60CCC133E674860C5EF91042478F3327F1956C5CE79A791E075CA43"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx",
                "7B5617164BDF969D41221D9A83C5FF6C288DC91795B7C41353079AE654344CE2",
                "F64E9BD7B0E51FF365169112C89AB5D04CC54A1E341815ABAF7573EA8C81170B"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/MoonResonance/moon_resonance_island.fbx",
                "FBA3247C094803B1A51B519E528AFC3B3B1B139B40BF728320B4EF1941CD9C5F",
                "D507CDCACC4A6288743F03513CDA0B1DE0496B6FFA9D7781465CED6D414A4331"),
            new Revision("Assets/RadioArchipelago/Art/AmberFlowWater.shader",
                "783894E63F03B852DD2F1A159A2824A77150E9D007C8D7AA2E7E9E53B36ABF39",
                "F821E1DA8E64B3DFDD7EE199F76562111B248ACC38555976464809A89DAAC3D4"),
            new Revision("Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx",
                "F64E9BD7B0E51FF365169112C89AB5D04CC54A1E341815ABAF7573EA8C81170B",
                "37E99875187A9E0E67A6405A042DBA3919E8B96377111FC54EC627EAA1CCB4D0"),
            new Revision("Assets/RadioArchipelago/Art/AmberFlowWater.shader",
                "F821E1DA8E64B3DFDD7EE199F76562111B248ACC38555976464809A89DAAC3D4",
                "A8E58D8A4F963F5AF051FFBAD19BBC60FE6247BD02290BA41C1B88DAF227714A"),
            new Revision("Assets/RadioArchipelago/Art/AmberFlowWater.shader",
                "A8E58D8A4F963F5AF051FFBAD19BBC60FE6247BD02290BA41C1B88DAF227714A",
                "CBE7820DEA0ECC4831418FA8B6ACE4950A52D480294A61D0EE9A71C8E1474AE7")
        };

        public static string ExpectedHash(string path, string originalHash)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string expected = originalHash;
            // Keep immutable historical receipts: subsequent explicitly reviewed
            // revisions extend an exact path+digest chain rather than rewriting
            // an older approved output or ignoring whole classes of assets.
            for (int step = 0; step < Revisions.Length; step++)
            {
                string next = null;
                foreach (Revision revision in Revisions)
                    if (string.Equals(expected, revision.Before, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(fullPath, System.IO.Path.GetFullPath(revision.Path), StringComparison.OrdinalIgnoreCase))
                    { next = revision.After; break; }
                if (next == null) return expected;
                expected = next;
            }
            throw new InvalidOperationException("Cycle in reviewed island asset revisions: " + path);
        }
    }
}
