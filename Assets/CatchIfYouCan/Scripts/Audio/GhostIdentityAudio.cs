using System.Collections.Generic;

namespace CatchIfYouCan.Audio
{
    public static class GhostIdentityAudio
    {
        private static readonly Dictionary<string, GhostAudioIdentity> Map = new Dictionary<string, GhostAudioIdentity>();

        static GhostIdentityAudio()
        {
            Register("THE WANDERER", "Ghost.Wanderer.Presence", "Ghost.Wanderer.Move", "Ghost.Wanderer.Hunt", "Ghost.Wanderer.Whisper");
            Register("THE WHISPER", "Ghost.Whisper.Presence", "Ghost.Whisper.Drift", "Ghost.Whisper.Hunt", "Ghost.Whisper.Close");
            Register("THE WATCHER", "Ghost.Watcher.Presence", "Ghost.Watcher.Glide", "Ghost.Watcher.Hunt", "Ghost.Watcher.Breath");
            Register("THE MIMICER", "Ghost.Mimicer.Presence", "Ghost.Mimicer.Copy", "Ghost.Mimicer.Hunt", "Ghost.Mimicer.Echo");
            Register("THE HOLLOW", "Ghost.Hollow.Presence", "Ghost.Hollow.Void", "Ghost.Hollow.Hunt", "Ghost.Hollow.Moan");
            Register("THE KNOCKER", "Ghost.Knocker.Presence", "Ghost.Knocker.Step", "Ghost.Knocker.Hunt", "Ghost.Knocker.Rap");
            Register("THE SHADEBORN", "Ghost.Shadeborn.Presence", "Ghost.Shadeborn.Shift", "Ghost.Shadeborn.Hunt", "Ghost.Shadeborn.Hiss");
            Register("THE STATIC", "Ghost.Static.Presence", "Ghost.Static.Glitch", "Ghost.Static.Hunt", "Ghost.Static.Burst");
            Register("THE CRAWLER", "Ghost.Crawler.Presence", "Ghost.Crawler.Scrape", "Ghost.Crawler.Hunt", "Ghost.Crawler.Breath");
            Register("THE WEEPING ONE", "Ghost.Weeping.Presence", "Ghost.Weeping.Shuffle", "Ghost.Weeping.Hunt", "Ghost.Weeping.Sob");
        }

        public static GhostAudioIdentity Resolve(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return DefaultIdentity();
            if (Map.TryGetValue(displayName.ToUpperInvariant(), out var id))
                return id;
            return DefaultIdentity();
        }

        private static void Register(string name, string presence, string move, string hunt, string whisper)
        {
            Map[name] = new GhostAudioIdentity(presence, move, hunt, whisper);
        }

        private static GhostAudioIdentity DefaultIdentity()
        {
            return new GhostAudioIdentity(
                "Ghost.Generic.Presence",
                "Ghost.Generic.Move",
                "Ghost.Generic.Hunt",
                "Ghost.Whisper.Close");
        }
    }

    public readonly struct GhostAudioIdentity
    {
        public readonly string PresenceEventId;
        public readonly string MovementEventId;
        public readonly string HuntEventId;
        public readonly string WhisperEventId;

        public GhostAudioIdentity(string presence, string move, string hunt, string whisper)
        {
            PresenceEventId = presence;
            MovementEventId = move;
            HuntEventId = hunt;
            WhisperEventId = whisper;
        }
    }
}
