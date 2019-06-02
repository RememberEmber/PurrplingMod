﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;

namespace PurrplingMod.Utils
{
    internal static class Helper
    {
        public static bool IsNPCAtTile(GameLocation location, Vector2 tile, NPC whichNPC = null)
        {
            NPC npc = location.isCharacterAtTile(tile);

            if (whichNPC != null && npc != null)
            {
                return whichNPC.Name == npc.Name;
            }

            return npc != null;
        }

        public static bool SpouseHasBeenKissedToday(NPC spouse)
        {
            return (bool)spouse
                .GetType()
                .GetField("hasBeenKissedToday",
                          BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spouse);
        }

        public static bool IsSpouseMarriedToFarmer(NPC spouse, Farmer farmer)
        {
            return farmer.spouse != null
                   && farmer.spouse.Equals(spouse.Name)
                   && farmer.isMarried()
                   && spouse.isMarried()
                   && spouse.getSpouse()?.spouse == spouse.Name;
        }

        public static bool CanRequestDialog(Farmer farmer, NPC npc)
        {
            // Can't request dialogue if giftable object is in farmer's hands or npc has current dialogues
            bool forbidden = (farmer.ActiveObject != null && farmer.ActiveObject.canBeGivenAsGift()) || npc.CurrentDialogue.Count > 0;

            if (!forbidden && IsSpouseMarriedToFarmer(npc, farmer))
            {
                // Kiss married spouse first if she/he facing kissable
                bool kissedToday = SpouseHasBeenKissedToday(npc);
                forbidden = !kissedToday && npc.FacingDirection == 3 || !kissedToday && npc.FacingDirection == 1;
            }

            return !forbidden;
        }

        public static List<Point> NearPoints(Point p, int distance)
        {
            List<Point> points = new List<Point>();
            for (int x = p.X - distance; x <= p.X + distance; x++)
            {
                for (int y = p.Y - distance; y <= p.Y + distance; y++)
                {
                    if (x == p.X && y == p.Y)
                        continue;
                    points.Add(new Point(x, y));
                }
            }

            return points;
        }

        public static List<Point> SortPointsByNearest(List<Point> nearPoints, Point startTilePoint)
        {
            List<Tuple<Point, float>> nearPointsWithDistance = Helper.MapNearPointsWithDistance(nearPoints, startTilePoint);

            nearPointsWithDistance.Sort(delegate (Tuple<Point, float> p1, Tuple<Point, float> p2) {
                if (p1.Item2 == p2.Item2)
                {
                    return 0;
                }

                return -1 * p1.Item2.CompareTo(p2.Item2);
            });

            return nearPointsWithDistance.ConvertAll<Point>(
                new Converter<Tuple<Point, float>, Point>(
                    delegate (Tuple<Point, float> tp)
                    {
                        return tp.Item1;
                    }
                )
            );
        }

        public static float Distance(Point p1, Point p2)
        {
            return Utility.distance(p1.X, p2.X, p1.Y, p2.Y);
        }
        private static List<Tuple<Point, float>> MapNearPointsWithDistance(List<Point> nearPoints, Point startTilePoint)
        {
            List<Tuple<Point, float>> nearPointsWithDistance = new List<Tuple<Point, float>>();

            foreach (Point nearPoint in nearPoints)
            {
                nearPointsWithDistance.Add(new Tuple<Point, float>(nearPoint, Utility.distance(nearPoint.X, startTilePoint.X, nearPoint.Y, startTilePoint.Y)));
            }

            return nearPointsWithDistance;
        }
    }
}
