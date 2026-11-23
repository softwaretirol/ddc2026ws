using ConferenceApp.Shared;

namespace ConferenceApp.Client.Components;

/// <summary>
/// Ein Abschnitt der Zeitachse eines Agenda-Tages: entweder Programm (maßstabsgetreu)
/// oder eine Pause (Zeitraum ohne Session in irgendeinem Raum), die gestaucht dargestellt wird.
/// </summary>
public sealed record TimelineSegment(TimeOnly Start, TimeOnly End, bool IsGap)
{
    public TimeSpan Duration => End - Start;

    /// <summary>Beschriftung für Pausen, z. B. „Pause · 11:15–13:00“.</summary>
    public string Label => $"Pause · {Format.Range(Start, End)}";

    public bool Contains(TimeOnly t) => t >= Start && t < End;
}

/// <summary>
/// Zeitachse eines Agenda-Tages aus Segmenten. Lücken von mindestens <see cref="MinGap"/>,
/// in denen in keinem Raum eine Session läuft, werden zu Pausen-Segmenten; kürzere Lücken
/// bleiben Teil des umgebenden Programm-Segments. Wird von Raster und Liste gemeinsam genutzt,
/// damit beide dieselben Pausen zeigen.
/// </summary>
public sealed class AgendaTimeline
{
    /// <summary>Ab dieser Länge gilt ein Zeitraum ohne Session als Pause.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(30);

    private AgendaTimeline(IReadOnlyList<TimelineSegment> segments) => Segments = segments;

    /// <summary>Alle Segmente in zeitlicher Reihenfolge, lückenlos aneinandergereiht.</summary>
    public IReadOnlyList<TimelineSegment> Segments { get; }

    /// <summary>Nur die Pausen-Segmente.</summary>
    public IEnumerable<TimelineSegment> Gaps => Segments.Where(s => s.IsGap);

    public TimeOnly Start => Segments.Count > 0 ? Segments[0].Start : default;
    public TimeOnly End => Segments.Count > 0 ? Segments[^1].End : default;

    /// <summary>
    /// Baut die Zeitachse für die Sessions eines Tages. Optional lässt sich der Bereich nach vorne
    /// (<paramref name="from"/>) bzw. hinten (<paramref name="to"/>) erweitern, etwa auf volle Stunden;
    /// dieser Rand zählt nie als Pause.
    /// </summary>
    public static AgendaTimeline Build(IEnumerable<SessionDto> sessions, TimeOnly? from = null, TimeOnly? to = null)
    {
        // Belegte Zeiträume über alle Räume zusammenführen.
        var busy = new List<(TimeOnly Start, TimeOnly End)>();
        foreach (var s in sessions.Where(s => s.EndTime > s.StartTime).OrderBy(s => s.StartTime))
        {
            if (busy.Count > 0 && s.StartTime <= busy[^1].End)
            {
                if (s.EndTime > busy[^1].End) busy[^1] = (busy[^1].Start, s.EndTime);
            }
            else
            {
                busy.Add((s.StartTime, s.EndTime));
            }
        }

        if (busy.Count == 0) return new AgendaTimeline([]);

        var segments = new List<TimelineSegment>();
        var start = from is { } f && f < busy[0].Start ? f : busy[0].Start;
        var segStart = start;
        for (var i = 1; i < busy.Count; i++)
        {
            var gapStart = busy[i - 1].End;
            var gapEnd = busy[i].Start;
            if (gapEnd - gapStart < MinGap) continue;

            segments.Add(new TimelineSegment(segStart, gapStart, false));
            segments.Add(new TimelineSegment(gapStart, gapEnd, true));
            segStart = gapEnd;
        }

        var end = to is { } t && t > busy[^1].End ? t : busy[^1].End;
        segments.Add(new TimelineSegment(segStart, end, false));
        return new AgendaTimeline(segments);
    }

    /// <summary>Segment, in dem der Zeitpunkt liegt (Ende exklusiv); null außerhalb der Zeitachse.</summary>
    public TimelineSegment? SegmentAt(TimeOnly t) => Segments.FirstOrDefault(s => s.Contains(t));

    /// <summary>Liegt der Zeitpunkt in einer Pause?</summary>
    public bool IsInGap(TimeOnly t) => SegmentAt(t)?.IsGap == true;
}
