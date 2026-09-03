using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Content
{
    public static class InvestigationContentLoader
    {
        private const string ResourcesPath = "CatchIfYouCan/InvestigationContentCatalog";

        private static InvestigationContentCatalog _cached;
        private static bool _missingReported;

        public static InvestigationContentCatalog LoadCatalog()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<InvestigationContentCatalog>(ResourcesPath);

            // A null catalog is indistinguishable from a catalog that happens to change
            // nothing: ApplyToGenerator returns early either way and the house still
            // generates. Reported once, so a missing asset is a line in the console rather
            // than a generator that quietly ignores its content settings forever.
            if (_cached == null && !_missingReported)
            {
                _missingReported = true;
                Core.CIYCLog.Warn(
                    "No InvestigationContentCatalog at Resources/" + ResourcesPath + ". " +
                    "House generation will run on its built-in defaults and every content " +
                    "override in that catalog is ignored.");
            }

            return _cached;
        }

        public static void ApplyToGenerator(ProceduralHouseGenerator generator)
        {
            if (generator == null)
                return;

            var catalog = LoadCatalog();
            if (catalog == null)
                return;

            generator.ApplyContentCatalog(catalog);
        }
    }
}
