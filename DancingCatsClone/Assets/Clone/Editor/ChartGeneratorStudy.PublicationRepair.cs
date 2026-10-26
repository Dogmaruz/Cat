using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    public static partial class ChartGeneratorStudy
    {
        // This is an operational publication permit, never a new inference/heldout assessment freeze.
        private const string PublicationRepairFreezeSha256 = "6e0ea0eb8a70959649027b8d48da9d0200157de2b6faa401a99e8f894d971981";
        private const string PublicationRepairPurpose = "Publication-only double JSON round-trip comparison repair; original assessment freeze and candidate outputs preserved.";
        private const string PublicationRepairGuardPath = "DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.PublicationRepair.cs";
        private static readonly string[] PublicationRepairAllowedPaths =
        {
            "DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.Publication.cs",
            "DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.Validation.cs"
        };
        [Serializable] private sealed class OriginalFreezeMetadata { public string frozenAtUtc, scope; }
        [Serializable] private sealed class PublicationSourceChange
        {
            public string path, beforeSha256, afterSha256;
            public bool normalizedLf;
        }
        [Serializable] private sealed class PublicationRepairProof
        {
            public int schemaVersion, doubleMaxUlps;
            public bool finiteDoubles, sameSignDoubles;
            public double absoluteEpsilon;
            public string floatComparison, discreteComparison;
            public string purpose, createdAtUtc, originalFreezeSha256, originalFrozenAtUtc, originalFreezeScope;
            public PublicationSourceChange[] changedSources;
            public FrozenHash guardSource;
            public FrozenHash[] candidates;
        }

        public static void ValidatePublicationFrozenInputs()
        {
            var m = LoadManifest();
            ValidateSelection(m);
            ValidateBaselineInventory(m);
            string freezePath = m.studyDirectory + "/freeze.json";
            Require(Hash(freezePath) == PublicationRepairFreezeSha256, "The original assessment freeze must remain byte-for-byte unchanged.");
            var freeze = Read<Freeze>(freezePath);
            Require(freeze != null && freeze.beforeValidation && DateTimeOffset.TryParse(freeze.frozenAtUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), "The original before-validation freeze is required.");
            ValidateFreezeCoverage(m, freeze);
            var proof = Read<PublicationRepairProof>(m.studyDirectory + "/publication-repair.json");
            ValidatePublicationRepairStructure(m, freeze, proof, Read<OriginalFreezeMetadata>(freezePath));
            string repo = Path.GetFullPath("..");
            var all = freeze.algorithm.Concat(freeze.measurements).Concat(freeze.baselines).Concat(freeze.manifests);
            foreach (var original in all)
            {
                var change = proof.changedSources.SingleOrDefault(c => c.path == original.path);
                Require(MatchesFrozenFile(change == null ? original : new FrozenHash { path = change.path, sha256 = change.afterSha256, normalizedLf = true }, repo),
                    "An original frozen input changed outside its exact publication utility repair: " + original.path);
            }
            Require(MatchesFrozenFile(proof.guardSource, repo), "The publication repair guard source differs from its proof.");
            Require(proof.candidates.All(e => MatchesFrozenFile(e, repo)), "An assessed candidate report, typed result or native timeline changed after the publication repair proof.");
            Require(m.sources.All(s => !File.Exists(Folder(m, s) + "/candidate.failure.json")), "A failed candidate rerun blocks publication of prior output.");
        }
        private static void ValidatePublicationRepairStructure(Manifest m, Freeze freeze, PublicationRepairProof proof, OriginalFreezeMetadata metadata)
        {
            Require(proof != null && proof.schemaVersion == 1 && proof.purpose == PublicationRepairPurpose
                && proof.doubleMaxUlps == 2 && proof.finiteDoubles && proof.sameSignDoubles && proof.absoluteEpsilon == 0
                && proof.floatComparison == "exact" && proof.discreteComparison == "exact"
                && proof.originalFreezeSha256 == PublicationRepairFreezeSha256 && metadata != null
                && proof.originalFrozenAtUtc == metadata.frozenAtUtc && proof.originalFreezeScope == metadata.scope
                && DateTimeOffset.TryParse(proof.createdAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                "Publication repair proof must preserve the original freeze identity, time and assessment scope.");
            Require(proof.changedSources != null && proof.changedSources.Length == PublicationRepairAllowedPaths.Length
                && proof.changedSources.All(c => c != null && c.normalizedLf && IsHash(c.beforeSha256) && IsHash(c.afterSha256) && c.beforeSha256 != c.afterSha256)
                && proof.changedSources.Select(c => c.path).Distinct(StringComparer.Ordinal).Count() == proof.changedSources.Length
                && PublicationRepairAllowedPaths.All(path => proof.changedSources.Any(c => c.path == path)),
                "Only the publisher and its validation utility may differ from the assessment freeze.");
            foreach (var change in proof.changedSources)
            {
                var original = freeze.algorithm.SingleOrDefault(e => e.path == change.path);
                Require(original != null && original.normalizedLf && original.sha256 == change.beforeSha256,
                    "Publication utility repair before-hashes must match the original frozen algorithm entries.");
            }
            Require(proof.guardSource != null && proof.guardSource.path == PublicationRepairGuardPath && proof.guardSource.normalizedLf && IsHash(proof.guardSource.sha256),
                "Pin the exact new publication repair guard source.");
            string prefix = Path.GetFileName(Path.GetFullPath(".")) + "/";
            var expected = m.sources.SelectMany(s => new[] { "/candidate.json", "/candidate.typed.json", "/candidate.generated.timeline.json" }
                .Select(suffix => prefix + Folder(m, s) + suffix)).ToArray();
            Require(proof.candidates != null && proof.candidates.Length == expected.Length && proof.candidates.All(e => e != null && !e.normalizedLf && IsHash(e.sha256))
                && proof.candidates.Select(e => e.path).Distinct(StringComparer.Ordinal).Count() == expected.Length
                && expected.All(path => proof.candidates.Any(e => e.path == path)), "Pin every already assessed candidate report/typed/native timeline as raw bytes.");
        }
    }
}
