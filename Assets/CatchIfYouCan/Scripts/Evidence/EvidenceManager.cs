using System;
using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Core;
using ServiceLocator = CatchIfYouCan.Core.ServiceLocator;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Evidence
{
    [Serializable]
    public class JournalEntry
    {
        public string Id;
        public string Title;
        public string Body;

        // Unity cannot serialize Nullable<T>, so this field is runtime-only. Journal
        // entries are never written to a save file or an inspector, so the nullable
        // reads better here than a sentinel enum value would.
        [NonSerialized] public EvidenceType? RelatedEvidence;
        public long TimestampUtcTicks;

        public JournalEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            TimestampUtcTicks = DateTime.UtcNow.Ticks;
        }
    }

    public class EvidenceManager : SingletonBehaviour<EvidenceManager>
    {
        private readonly HashSet<EvidenceType> _foundEvidence = new HashSet<EvidenceType>();
        private readonly List<PhotoResult> _photos = new List<PhotoResult>();
        private readonly List<JournalEntry> _journalEntries = new List<JournalEntry>();

        public IReadOnlyCollection<EvidenceType> FoundEvidence => _foundEvidence;
        public IReadOnlyList<PhotoResult> Photos => _photos;
        public IReadOnlyList<JournalEntry> JournalEntries => _journalEntries;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                ServiceLocator.Unregister<EvidenceManager>();
            base.OnDestroy();
        }

        public void ResetMission()
        {
            _foundEvidence.Clear();
            _photos.Clear();
            _journalEntries.Clear();
        }

        public bool HasEvidence(EvidenceType type) => _foundEvidence.Contains(type);

        public bool RegisterEvidence(EvidenceType type)
        {
            if (!_foundEvidence.Add(type))
                return false;

            GameEvents.EvidenceDetected(type);
            CIYCLog.Info($"Evidence registered: {type}");
            return true;
        }

        public void AddPhoto(PhotoResult photo)
        {
            if (photo == null)
                return;

            _photos.Add(photo);
            GameEvents.PhotoTaken(photo.Stars);
            CIYCLog.Info($"Photo saved ({photo.Stars} stars): {photo.Caption}");
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            if (entry == null)
                return;

            _journalEntries.Add(entry);

            // A journal entry is a record, not proof. It does not touch evidence truth at all.
            //
            // It used to call RegisterEvidence directly, which is how the EVP recorder proved
            // EVP Response against ghosts that do not make one. AH routed it through the
            // validator instead, which closed the hole but kept the shape: a caller with a
            // string and an enum could still start a confirmation. Now that each evidence type
            // has exactly one declared observing device (EvidenceAuthority), a journal entry
            // has no device and therefore no standing - so rather than submit an observation
            // that is always refused, it does not submit one. The device that measured the
            // thing has already said so through Observe; this writes down that it happened.
            CIYCLog.Info($"Journal entry added: {entry.Title}");
        }

        public void AddJournalEntry(string title, string body, EvidenceType? relatedEvidence = null)
        {
            AddJournalEntry(new JournalEntry
            {
                Title = title,
                Body = body,
                RelatedEvidence = relatedEvidence
            });
        }
    }
}
