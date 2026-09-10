using System.Collections.Generic;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>Ein achsenparalleles Rechteck in der Bodenebene des Raums, in Raumkoordinaten.</summary>
    public struct FootRect
    {
        public float MinX, MinZ, MaxX, MaxZ;

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public float Area => Mathf.Max(0f, Width) * Mathf.Max(0f, Depth);
        public Vector2 Centre => new Vector2((MinX + MaxX) * 0.5f, (MinZ + MaxZ) * 0.5f);

        public static FootRect FromCentre(Vector2 centre, float width, float depth)
        {
            return new FootRect
            {
                MinX = centre.x - width * 0.5f,
                MaxX = centre.x + width * 0.5f,
                MinZ = centre.y - depth * 0.5f,
                MaxZ = centre.y + depth * 0.5f,
            };
        }

        public bool Overlaps(FootRect other, float tolerance = 0.001f)
        {
            return MinX < other.MaxX - tolerance && MaxX > other.MinX + tolerance &&
                   MinZ < other.MaxZ - tolerance && MaxZ > other.MinZ + tolerance;
        }

        public bool Contains(FootRect other, float tolerance = 0.001f)
        {
            return other.MinX >= MinX - tolerance && other.MaxX <= MaxX + tolerance &&
                   other.MinZ >= MinZ - tolerance && other.MaxZ <= MaxZ + tolerance;
        }

        public override string ToString() =>
            "[" + MinX.ToString("F2") + ".." + MaxX.ToString("F2") + " x " +
            MinZ.ToString("F2") + ".." + MaxZ.ToString("F2") + "]";
    }

    /// <summary>
    /// Eine Fläche, die für etwas anderes reserviert ist.
    ///
    /// <para>
    /// <see cref="MaxHeight"/> ist der Grund, warum das kein blosses Verbot ist. Vor einem
    /// Fenster darf eine Kommode stehen und ein Kleiderschrank nicht - dieselbe Fläche, zwei
    /// Antworten, abhängig von der Höhe des Stücks. Eine Zone mit MaxHeight 0 ist ganz gesperrt;
    /// eine mit 0.90 lässt alles durch, was unter der Fensterbrüstung bleibt.
    /// </para>
    /// </summary>
    public struct ReservedZone
    {
        public FootRect Rect;
        public float MaxHeight;
        public string Reason;

        public bool Blocks(FootRect candidate, float candidateHeight)
        {
            if (!Rect.Overlaps(candidate))
                return false;

            return MaxHeight <= 0.001f || candidateHeight > MaxHeight + 0.001f;
        }
    }

    /// <summary>Ein Stück Wand, an dem etwas mit dem Rücken stehen kann.</summary>
    public struct WallRun
    {
        public SocketDirection Direction;

        /// <summary>Mitte des Abschnitts in Raumkoordinaten, auf der Wandlinie.</summary>
        public Vector2 Centre;

        /// <summary>Nutzbare Länge entlang der Wand.</summary>
        public float Length;

        /// <summary>Einheitsvektor von der Wand in den Raum hinein.</summary>
        public Vector2 Inward;

        /// <summary>Gierwinkel, bei dem das Vorderteil eines Möbels nach <see cref="Inward"/> zeigt.</summary>
        public float Yaw;

        public bool HasWindow;
        public bool HasDoor;
    }

    /// <summary>
    /// Was in diesem Raum tatsächlich frei ist - und was schon vergeben.
    ///
    /// <para>
    /// <b>Achsenparallel, und das ist eine Entscheidung, keine Vereinfachung.</b> Möbel werden
    /// ausschliesslich in Vielfachen von 90 Grad gedreht, weil ein Haus so gebaut ist und weil
    /// der Auftrag verlangt, nicht jeden Gegenstand zufällig zu verdrehen. Bei 90 Grad IST die
    /// gedrehte Grundfläche wieder achsenparallel - Breite und Tiefe tauschen die Plätze. Der
    /// Footprint wird deshalb LOKAL am Modell gemessen und beim Drehen getauscht, statt eine
    /// Welt-AABB des gedrehten Objekts zu nehmen, die bei jedem anderen Winkel zu gross wäre.
    /// </para>
    ///
    /// <para>
    /// Reine Geometrie: kein <c>Physics</c>-Aufruf, keine Collider, kein Frame-Timing. Dieselbe
    /// Begründung wie in <see cref="PropSpawner"/> - eine Abfrage der lebenden Physikszene hängt
    /// davon ab, wann <c>SyncTransforms</c> lief, und macht die Einrichtung vom Zeitpunkt
    /// abhängig statt vom Seed.
    /// </para>
    /// </summary>
    public sealed class RoomFootprint
    {
        /// <summary>Abstand, den ein an die Wand gestelltes Möbel zur Wandfläche hält.</summary>
        public const float WallGap = 0.03f;

        /// <summary>
        /// Die Breite, die ein Laufweg mindestens haben muss.
        ///
        /// Abgeleitet aus dem Spieler-Collider (<see cref="Player.PlayerFactory.CapsuleRadius"/>)
        /// plus einer Sicherheitsmarge auf jeder Seite. Nicht getippt: ändert sich der Radius,
        /// ändert sich das hier mit, statt still falsch zu werden.
        /// </summary>
        public const float WalkwaySafetyMargin = 0.15f;

        public static float WalkwayWidth =>
            Player.PlayerFactory.CapsuleRadius * 2f + WalkwaySafetyMargin * 2f;

        public Vector3 Size { get; }
        public FootRect Interior { get; }
        public List<WallRun> Walls { get; } = new List<WallRun>(4);

        private readonly List<ReservedZone> _reserved = new List<ReservedZone>(8);
        private readonly List<FootRect> _occupied = new List<FootRect>(24);

        public IReadOnlyList<ReservedZone> Reserved => _reserved;
        public IReadOnlyList<FootRect> Occupied => _occupied;

        public RoomFootprint(RoomShellInfo shell)
        {
            Size = shell.Size;

            float halfX = Size.x * 0.5f - ModularRoomBuilder.WallThickness * 0.5f - WallGap;
            float halfZ = Size.z * 0.5f - ModularRoomBuilder.WallThickness * 0.5f - WallGap;

            Interior = new FootRect { MinX = -halfX, MaxX = halfX, MinZ = -halfZ, MaxZ = halfZ };

            BuildWalls(shell, halfX, halfZ);
            ReserveDoorways(shell, halfX, halfZ);
            ReserveWindows(shell, halfX, halfZ);
            ReserveWalkways(shell, halfX, halfZ);
        }

        // ---------------------------------------------------------------- Wände

        private void BuildWalls(RoomShellInfo shell, float halfX, float halfZ)
        {
            // Kanonische Reihenfolge, nicht die eines HashSet: welche Wand ein Bett bekommt,
            // muss vom Seed abhängen und nicht davon, in welcher Reihenfolge etwas aufgezählt
            // wurde.
            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                SocketDirection dir = Directions.Cardinal[d];
                var run = new WallRun
                {
                    Direction = dir,
                    HasDoor = shell.HasDoor(dir),
                    HasWindow = shell.HasWindow(dir),
                };

                switch (dir)
                {
                    case SocketDirection.North:
                        run.Centre = new Vector2(0f, halfZ);
                        run.Length = halfX * 2f;
                        run.Inward = new Vector2(0f, -1f);
                        run.Yaw = 180f;
                        break;
                    case SocketDirection.South:
                        run.Centre = new Vector2(0f, -halfZ);
                        run.Length = halfX * 2f;
                        run.Inward = new Vector2(0f, 1f);
                        run.Yaw = 0f;
                        break;
                    case SocketDirection.East:
                        run.Centre = new Vector2(halfX, 0f);
                        run.Length = halfZ * 2f;
                        run.Inward = new Vector2(-1f, 0f);
                        run.Yaw = 270f;
                        break;
                    default:
                        run.Centre = new Vector2(-halfX, 0f);
                        run.Length = halfZ * 2f;
                        run.Inward = new Vector2(1f, 0f);
                        run.Yaw = 90f;
                        break;
                }

                Walls.Add(run);
            }
        }

        // ---------------------------------------------------------------- Sperrzonen

        /// <summary>
        /// Die Tür, ihr voller Schwenkbereich und die Fläche, auf der man hereinkommt.
        ///
        /// <para>
        /// Der Schwenkbereich ist so breit wie das Blatt und so tief wie das Blatt, denn genau
        /// diesen Viertelkreis überstreicht es - konservativ als Quadrat, weil ein Möbelstück in
        /// der Ecke des Quadrats die Tür genauso blockiert wie eines mitten davor. Auf BEIDEN
        /// Seiten der Öffnung reserviert, weil ein Türblatt in den einen oder den anderen Raum
        /// schwenkt und welcher es ist, hier nicht bekannt ist.
        /// </para>
        /// </summary>
        private void ReserveDoorways(RoomShellInfo shell, float halfX, float halfZ)
        {
            float swing = ModularRoomBuilder.DoorWidth - ModularDoorFactory.LeafClearance
                          + ModularDoorFactory.LeafThickness;
            float width = ModularRoomBuilder.DoorWidth + swing;

            for (int i = 0; i < Walls.Count; i++)
            {
                WallRun wall = Walls[i];
                if (!wall.HasDoor)
                    continue;

                // Von der Wandlinie in den Raum hinein, so tief wie das Blatt lang ist.
                Vector2 centre = wall.Centre + wall.Inward * (swing * 0.5f);

                bool alongX = wall.Direction == SocketDirection.North ||
                              wall.Direction == SocketDirection.South;

                FootRect rect = alongX
                    ? FootRect.FromCentre(centre, width, swing)
                    : FootRect.FromCentre(centre, swing, width);

                _reserved.Add(new ReservedZone
                {
                    Rect = rect,
                    MaxHeight = 0f,
                    Reason = "Tuerschwenkbereich " + wall.Direction,
                });
            }
        }

        /// <summary>
        /// Vor einem Fenster darf etwas Niedriges stehen und nichts Hohes.
        ///
        /// Deshalb eine Zone mit einer Höhengrenze statt eines Verbots: eine Kommode unter dem
        /// Fenster ist richtig eingerichtet, ein Kleiderschrank davor ist ein zugestelltes
        /// Fenster. Die Grenze ist die Brüstung, also genau die Höhe, ab der ein Möbel anfängt,
        /// die Scheibe zu verdecken.
        /// </summary>
        private void ReserveWindows(RoomShellInfo shell, float halfX, float halfZ)
        {
            for (int i = 0; i < Walls.Count; i++)
            {
                WallRun wall = Walls[i];
                if (!wall.HasWindow)
                    continue;

                const float depth = 0.45f;
                Vector2 centre = wall.Centre + wall.Inward * (depth * 0.5f);

                bool alongX = wall.Direction == SocketDirection.North ||
                              wall.Direction == SocketDirection.South;

                FootRect rect = alongX
                    ? FootRect.FromCentre(centre, ModularRoomBuilder.WindowWidth + 0.3f, depth)
                    : FootRect.FromCentre(centre, depth, ModularRoomBuilder.WindowWidth + 0.3f);

                _reserved.Add(new ReservedZone
                {
                    Rect = rect,
                    MaxHeight = ModularRoomBuilder.WindowSill,
                    Reason = "Fenster " + wall.Direction,
                });
            }
        }

        /// <summary>
        /// Ein freier Weg von jeder Tür zur Raummitte.
        ///
        /// <para>
        /// Über die Mitte, weil damit JEDE Tür mit JEDER anderen verbunden ist, ohne dass für n
        /// Türen n² Korridore nötig wären. Ein Raum, in dem alle Zugänge die Mitte erreichen, ist
        /// durchquerbar - das ist genau die Eigenschaft, die gefordert ist.
        /// </para>
        /// </summary>
        private void ReserveWalkways(RoomShellInfo shell, float halfX, float halfZ)
        {
            float half = WalkwayWidth * 0.5f;

            for (int i = 0; i < Walls.Count; i++)
            {
                WallRun wall = Walls[i];
                if (!wall.HasDoor)
                    continue;

                bool alongX = wall.Direction == SocketDirection.North ||
                              wall.Direction == SocketDirection.South;

                FootRect rect = alongX
                    ? new FootRect
                    {
                        MinX = -half, MaxX = half,
                        MinZ = Mathf.Min(0f, wall.Centre.y), MaxZ = Mathf.Max(0f, wall.Centre.y),
                    }
                    : new FootRect
                    {
                        MinX = Mathf.Min(0f, wall.Centre.x), MaxX = Mathf.Max(0f, wall.Centre.x),
                        MinZ = -half, MaxZ = half,
                    };

                _reserved.Add(new ReservedZone
                {
                    Rect = rect,
                    MaxHeight = 0f,
                    Reason = "Laufweg " + wall.Direction + " zur Mitte",
                });
            }
        }

        // ---------------------------------------------------------------- Belegung

        /// <summary>
        /// Ob ein Stück dieser Grundfläche und Höhe hier stehen darf.
        /// </summary>
        /// <param name="reason">Bei Ablehnung: warum, für die Diagnose.</param>
        public bool Fits(FootRect candidate, float height, out string reason)
        {
            if (!Interior.Contains(candidate))
            {
                reason = "ragt aus dem Raum " + Interior + " heraus";
                return false;
            }

            for (int i = 0; i < _reserved.Count; i++)
            {
                if (_reserved[i].Blocks(candidate, height))
                {
                    reason = "steht in der Sperrzone '" + _reserved[i].Reason + "'";
                    return false;
                }
            }

            for (int i = 0; i < _occupied.Count; i++)
            {
                if (_occupied[i].Overlaps(candidate))
                {
                    reason = "ueberschneidet ein schon platziertes Stueck bei " + _occupied[i];
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public void Occupy(FootRect rect) => _occupied.Add(rect);

        /// <summary>Wie viel Bodenfläche der Raum überhaupt hat.</summary>
        public float InteriorArea => Interior.Area;

        /// <summary>Wie viel davon nach den Sperrzonen noch nutzbar ist - eine Schätzung.</summary>
        public float UsableArea
        {
            get
            {
                float blocked = 0f;
                for (int i = 0; i < _reserved.Count; i++)
                {
                    if (_reserved[i].MaxHeight <= 0.001f)
                        blocked += _reserved[i].Rect.Area;
                }

                return Mathf.Max(0f, Interior.Area - blocked);
            }
        }
    }
}
