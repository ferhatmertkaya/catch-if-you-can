using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Content
{
    public static class InvestigationContentLoader
    {
        private const string ResourcesPath = "CatchIfYouCan/InvestigationContentCatalog";

        private static InvestigationContentCatalog _cached;

        public static InvestigationContentCatalog LoadCatalog()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<InvestigationContentCatalog>(ResourcesPath);
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
