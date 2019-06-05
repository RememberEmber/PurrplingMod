﻿using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PurrplingMod.Utils
{
    internal static partial class DialogueHelper
    {
        private static bool GetDialogueString(Dictionary<string, string> dialogues, string key, out string text)
        {
            var keys = from _key in dialogues.Keys
                       where _key.StartsWith(key + "$")
                       select _key;

            if (keys.Count() > 0)
            {
                int i = Game1.random.Next(keys.Count() + 1);

                if (i > 0 && dialogues.TryGetValue($"{key}${i}", out text))
                    return true;
            }

            if (dialogues.TryGetValue(key, out text))
                return true;

            text = key;

            return false;
        }

        public static bool GetDialogueString(NPC n, string key, out string text)
        {
            if (GetDialogueString(n.Dialogue, key, out text))
                return true;

            text = $"{n.Name}.{text}";

            return false;
        }

        public static string GetDialogueString(NPC n, string key)
        {
            GetDialogueString(n, key, out string text);
            return text;
        }

        private static bool FindLikeDialogueString(string[] variants, Tuple<bool, string>[] conditions, NPC n, string key, out string text, string suffix = "")
        {
            foreach (string variant in variants)
            {
                foreach (var condition in conditions)
                {
                    // Conditional variant
                    if (condition.Item1 && GetDialogueString(n, $"{key}{variant}{condition.Item2}{suffix}", out text))
                        return true;
                }

                // Common variant
                if (GetDialogueString(n, $"{key}{variant}{suffix}", out text))
                    return true;
            }

            text = key;

            return false;
        }

        public static bool GetVariousDialogueString(NPC n, string key, out string text)
        {
            Farmer f = Game1.player;
            Finder finder = new Finder()
            {
                Date = SDate.Now(),
                IsNight = Game1.isDarkOut(),
                IsMarried = Helper.IsSpouseMarriedToFarmer(n, f),
                FriendshipHeartLevel = f.getFriendshipHeartLevelForNPC(n.Name),
                Weather = Helper.GetCurrentWeatherName(),
            };

            // Generate possible dialogue keys
            finder.GenerateVariousKeys(key);

            // Try to find a relevant dialogue
            foreach (string k in finder.PossibleKeys)
                if (GetDialogueString(n, k, out text))
                    return true;

            text = key;
            return false;
        }

        public static bool GetVariousDialogueString(NPC n, string key, GameLocation l, out string text)
        {
            return GetVariousDialogueString(n, $"{key}_{l.Name}", out text);
        }

        public static bool GetBubbleString(Dictionary<string, string> bubbles, NPC n, GameLocation l, out string text)
        {
            if (GetDialogueString(bubbles, $"{l.Name}_{n.Name}", out text))
            {
                text = string.Format(text, Game1.player?.Name, n.Name);

                return true;
            }

            return false;
        }

        public static Dialogue GenerateDialogue(NPC n, string key, bool returnsNull = true)
        {
            if (GetVariousDialogueString(n, key, out string text) || !returnsNull)
                return new Dialogue(text, n);

            return null;
        }

        public static Dialogue GenerateDialogue(NPC n, GameLocation l, string key, bool returnsNull = true)
        {
            if (GetVariousDialogueString(n, key, l, out string text) || !returnsNull)
                return new Dialogue(text, n);

            return null;
        }

        public static void SetupCompanionDialogues(NPC n, Dictionary<string, string> dialogues)
        {
            foreach (var pair in dialogues)
                n.Dialogue[pair.Key] = pair.Value;
        }

        public static void DrawDialogue(Dialogue dialogue)
        {
            NPC speaker = dialogue.speaker;

            speaker.CurrentDialogue.Push(dialogue);
            Game1.drawDialogue(speaker);
        }
    }
}
