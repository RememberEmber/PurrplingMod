﻿using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using System.Collections.Generic;

namespace PurrplingMod
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        public Point myLastLocationTile;
        public Point abbyLastPositionTile;
        public Point targetPositionTile;
        public int standingTimeout = 100;
        public FollowController followController;
        public DialogueDriver dialogueDriver;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.followController = new FollowController();
            this.dialogueDriver = new DialogueDriver(helper);

            this.dialogueDriver.DialogueChanged += this.DialogueDriver_DialogueChanged;
            this.dialogueDriver.DialogueEnded += this.DialogueDriver_DialogueEnded;
            this.dialogueDriver.SpeakerChanged += this.DialogueDriver_SpeakerChanged;

            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Player.Warped += this.Player_Warped;
            helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
            helper.Events.GameLoop.UpdateTicking += this.GameLoop_UpdateTicking;

        }

        private void DialogueDriver_SpeakerChanged(object sender, SpeakerChangedArgs e)
        {
            this.Monitor.Log($"Speaker changed from {e.PreviousSpeaker?.Name} to {e.CurrentSpeaker?.Name}");
        }

        private void DialogueDriver_DialogueEnded(object sender, DialogueEndedArgs e)
        {
            this.Monitor.Log($"Dialogue with {e.PreviousDialogue.speaker.Name} ended");
        }

        private void DialogueDriver_DialogueChanged(object sender, DialogueChangedArgs e)
        {
            this.Monitor.Log($"Current dialogue - {e.CurrentDialogue?.speaker.Name} says: '{e.CurrentDialogue?.getCurrentDialogue()}'");
        }

        private void GameLoop_UpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            this.followController.Update(e);
        }

        private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            GameLocation location = Game1.player.currentLocation;
            Farmer player = Game1.player;
            this.myLastLocationTile = player.getTileLocationPoint();
            this.followController.leader = player;
            this.spawnAbigailHere(location, player.getTileLocationPoint());
        }

        private void spawnAbigailHere(GameLocation location, Point locationTilePoint)
        {
            NPC abigail = Game1.getCharacterFromName("Abigail");

            if (abigail != null && abigail.currentLocation != location)
            {
                abigail.controller = null;
                abigail.Halt();
                abigail.currentLocation.characters.Remove(abigail);
                abigail.currentLocation = location;
                location.addCharacter(abigail);
                abigail.setTilePosition(locationTilePoint);
                abigail.setNewDialogue("Meow", true);
                abigail.setNewDialogue("Kekekeke", true);
                this.abbyLastPositionTile = abigail.getTileLocationPoint();
                this.followController.follower = abigail;
            }
        }

        private void Player_Warped(object sender, WarpedEventArgs e)
        {
            if (e.NewLocation is MineShaft)
                return;

            //this.followController.leader = e.Player;
            //this.spawnAbigailHere(e.NewLocation, e.Player.getTileLocationPoint());
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            // print button presses to the console window
            // this.Monitor.Log($"{Game1.player.Name} pressed {e.Button}.");
        }
    }
}