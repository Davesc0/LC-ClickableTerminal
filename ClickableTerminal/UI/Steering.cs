using System.Collections.Generic;
using UnityEngine;

namespace ClickableTerminal
{
    // The parts of a page the cursor steps between. A step stays in its part while there is something that way, and only then goes over to the nearest part in that direction.
    internal enum Zone
    {
        Header,
        Bar,
        Tabs,
        List,
        Panel,
        Window,
        App
    }

    internal struct Spot
    {
        internal Vector2 Point;
        internal Rect Bounds;
        internal Zone Zone;
    }

    // Where a step with the d-pad, the stick or the arrow keys goes, worked out from the targets of the screen alone.
    internal class Steering
    {
        internal readonly List<Spot> Targets = new List<Spot>();
        private readonly Dictionary<Zone, Vector2> lastIn = new Dictionary<Zone, Vector2>();
        private readonly Dictionary<Zone, Rect> areas = new Dictionary<Zone, Rect>();
        private readonly Dictionary<Zone, Rect> given = new Dictionary<Zone, Rect>();
        private static readonly Vector3[] Corners = new Vector3[4];

        internal void Clear()
        {
            Targets.Clear();
            given.Clear();
        }

        // The whole area of a part, in the frame's coordinates. Without it a part is as big as its targets reach.
        internal void Area(RectTransform frame, RectTransform space, Rect area, Zone zone)
        {
            var min = frame.InverseTransformPoint(space.TransformPoint(area.min));
            var max = frame.InverseTransformPoint(space.TransformPoint(area.max));
            given[zone] = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        internal void Add(RectTransform frame, RectTransform rect, Zone zone, float along = 0.5f)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return;
            }

            var r = rect.rect;
            Add(frame, rect, new Vector2(Mathf.Lerp(r.xMin, r.xMax, along), r.center.y), zone);
        }

        // A target at a given point of the rectangle, in the rectangle's own coordinates.
        internal void Add(RectTransform frame, RectTransform rect, Vector2 point, Zone zone)
        {
            rect.GetWorldCorners(Corners);
            var min = frame.InverseTransformPoint(Corners[0]);
            var max = frame.InverseTransformPoint(Corners[2]);
            Targets.Add(new Spot
            {
                Point = frame.InverseTransformPoint(rect.TransformPoint(point)),
                Bounds = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y)),
                Zone = zone
            });
        }

        internal void Remember(int target)
        {
            lastIn[Targets[target].Zone] = Targets[target].Point;
        }

        internal int Nearest(Vector2 point, bool skipHeader)
        {
            var best = -1;
            for (var i = 0; i < Targets.Count; i++)
            {
                if (skipHeader && Targets[i].Zone == Zone.Header)
                {
                    continue;
                }

                if (best < 0 || (Targets[i].Point - point).sqrMagnitude < (Targets[best].Point - point).sqrMagnitude)
                {
                    best = i;
                }
            }

            return best;
        }

        internal Zone? ZoneAt(Vector2 point)
        {
            var nearest = Nearest(point, false);
            return nearest >= 0 ? Targets[nearest].Zone : (Zone?)null;
        }

        // The next target that way: first in the cursor's own part, then in the parts next to it. -1 when there is none. sameLine is how far apart two targets may be and still count as one line.
        internal int Step(Vector2 from, Vector2 direction, float sameLine)
        {
            var zone = ZoneAt(from);
            if (zone == null)
            {
                return -1;
            }

            // In the list a sideways step never slips up to the column heads: it goes over to the tabs or the panel.
            var at = Pick(from, direction, zone.Value, sameLine, zone.Value != Zone.List && zone.Value != Zone.App);
            if (at >= 0)
            {
                return at;
            }

            foreach (var next in NextZones(zone.Value, direction))
            {
                at = Enter(next.Zones, from, direction);
                if (at >= 0)
                {
                    return at;
                }
            }

            return -1;
        }

        private int Pick(Vector2 from, Vector2 direction, Zone zone, float sameLine, bool slanted)
        {
            var side = new Vector2(direction.y, direction.x);
            var vertical = direction.y != 0f;
            var nearest = float.MaxValue;

            if (vertical)
            {
                foreach (var target in Targets)
                {
                    var along = Vector2.Dot(target.Point - from, direction);
                    if (target.Zone == zone && along > sameLine && along < nearest)
                    {
                        nearest = along;
                    }
                }
            }

            var best = float.MaxValue;
            var found = -1;
            for (var i = 0; i < Targets.Count; i++)
            {
                if (Targets[i].Zone != zone)
                {
                    continue;
                }

                var offset = Targets[i].Point - from;
                var along = Vector2.Dot(offset, direction);
                var across = Mathf.Abs(Vector2.Dot(offset, side));
                var fits = vertical ? along > sameLine && along <= nearest + sameLine : along > 1f && across <= sameLine;
                var score = vertical ? across : along;
                if (fits && score < best)
                {
                    best = score;
                    found = i;
                }
            }

            if (found >= 0 || vertical || !slanted)
            {
                return found;
            }

            for (var i = 0; i < Targets.Count; i++)
            {
                if (Targets[i].Zone != zone)
                {
                    continue;
                }

                var offset = Targets[i].Point - from;
                var along = Vector2.Dot(offset, direction);
                var score = Mathf.Abs(Vector2.Dot(offset, side)) * 2f + along;
                if (along > sameLine && score < best)
                {
                    best = score;
                    found = i;
                }
            }

            return found;
        }

        // The parts that way, nearest first. Parts at about the same distance come as one group (the tabs, the list and the panel under the bar), and the step then goes to whichever of them is closest across.
        private class Group
        {
            internal float Gap;
            internal readonly List<Zone> Zones = new List<Zone>();
        }

        private List<Group> NextZones(Zone from, Vector2 direction)
        {
            areas.Clear();
            foreach (var target in Targets)
            {
                if (given.TryGetValue(target.Zone, out var whole))
                {
                    areas[target.Zone] = whole;
                    continue;
                }

                areas[target.Zone] = areas.TryGetValue(target.Zone, out var area)
                    ? Rect.MinMaxRect(Mathf.Min(area.xMin, target.Bounds.xMin), Mathf.Min(area.yMin, target.Bounds.yMin),
                        Mathf.Max(area.xMax, target.Bounds.xMax), Mathf.Max(area.yMax, target.Bounds.yMax))
                    : target.Bounds;
            }

            var groups = new List<Group>();
            if (!areas.TryGetValue(from, out var here))
            {
                return groups;
            }

            var vertical = direction.y != 0f;
            var found = new List<KeyValuePair<float, Zone>>();
            foreach (var pair in areas)
            {
                if (pair.Key == from)
                {
                    continue;
                }

                var there = pair.Value;
                float gap;
                if (vertical)
                {
                    gap = direction.y < 0f ? here.yMin - there.yMax : there.yMin - here.yMax;
                }
                else
                {
                    // Side by side only: the two parts have to share some height.
                    if (there.yMax <= here.yMin || there.yMin >= here.yMax)
                    {
                        continue;
                    }

                    gap = direction.x > 0f ? there.xMin - here.xMax : here.xMin - there.xMax;
                }

                if (gap > -2f)
                {
                    found.Add(new KeyValuePair<float, Zone>(gap, pair.Key));
                }
            }

            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var pair in found)
            {
                if (groups.Count == 0 || pair.Key - groups[groups.Count - 1].Gap >= 4f)
                {
                    groups.Add(new Group { Gap = pair.Key });
                }

                groups[groups.Count - 1].Zones.Add(pair.Value);
            }

            return groups;
        }

        // Into another part: where the cursor last was in it, else what is closest across from where it comes.
        private int Enter(List<Zone> into, Vector2 from, Vector2 direction)
        {
            if (into.Count == 1 && lastIn.TryGetValue(into[0], out var was))
            {
                for (var i = 0; i < Targets.Count; i++)
                {
                    if (Targets[i].Zone == into[0] && (Targets[i].Point - was).sqrMagnitude < 4f)
                    {
                        return i;
                    }
                }
            }

            var side = new Vector2(direction.y, direction.x);
            var best = float.MaxValue;
            var found = -1;
            for (var i = 0; i < Targets.Count; i++)
            {
                if (!into.Contains(Targets[i].Zone))
                {
                    continue;
                }

                var offset = Targets[i].Point - from;
                var along = Vector2.Dot(offset, direction);
                var score = Mathf.Abs(Vector2.Dot(offset, side)) + Mathf.Abs(along) * 0.3f + (along < 0f ? 100000f : 0f);
                if (score < best)
                {
                    best = score;
                    found = i;
                }
            }

            return found;
        }
    }
}
