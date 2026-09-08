namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// WAS hinter dem Portal steht — nicht, OB überhaupt etwas gebaut wird. Das entscheidet
    /// <c>InvestigationBootstrap.generateWorld</c>, und die beiden Fragen getrennt zu halten ist
    /// der Grund, warum es zwei Felder sind: „nichts bauen, um den Charakter anzusehen" und
    /// „einen kleinen Raum statt des Hauses bauen" sind verschiedene Absichten, und ein einzelner
    /// Schalter für beide hätte je nach Stellung zwei Bedeutungen.
    /// </summary>
    public enum MissionWorldKind
    {
        /// <summary>Ein einzelner kleiner Raum, weit ab von der Lobby. Zum Ausprobieren des Portals.</summary>
        TestRoom = 0,

        /// <summary>Das vollständige prozedural erzeugte Haus.</summary>
        House = 1,
    }
}
