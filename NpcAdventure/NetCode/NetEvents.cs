﻿using Microsoft.Xna.Framework;
using NpcAdventure.AI.Controller;
using NpcAdventure.Loader;
using NpcAdventure.StateMachine;
using NpcAdventure.StateMachine.State;
using NpcAdventure.StateMachine.StateFeatures;
using NpcAdventure.Story;
using NpcAdventure.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static NpcAdventure.AI.AI_StateMachine;
using static NpcAdventure.StateMachine.CompanionStateMachine;
using static NpcAdventure.Story.GameMasterState;

namespace NpcAdventure.NetCode
{
    class NetEvents
    {
        private CompanionManager companionManager;

        private IMultiplayerHelper helper;

        private Dictionary<String, NetEventProcessor> messages;

        private IManifest modManifest;

        public NetEvents(IMultiplayerHelper helper)
        {
            this.helper = helper;

        }

        public void SetUp(IManifest manifest, CompanionManager manager, IContentLoader loader, GameMaster gameMaster)
        {
            this.companionManager = manager;
            this.modManifest = manifest;

            this.messages = new Dictionary<string, NetEventProcessor>()
            {
                {DialogEvent.EVENTNAME, new NetEventShowDialogue(manager)},
                {CompanionRequestEvent.EVENTNAME, new NetEventRecruitNPC(manager)},
                {PlayerWarpedEvent.EVENTNAME, new NetEventPlayerWarped(manager)},
                {DialogueRequestEvent.EVENTNAME, new NetEventDialogueRequest(manager)},
                {QuestionEvent.EVENTNAME, new NetEventQuestionRequest(manager, this, loader)},
                {QuestionResponse.EVENTNAME, new NetEventQuestionResponse(manager)},
                {CompanionChangedState.EVENTNAME, new NetEventCompanionChangedState(manager)},
                {CompanionStateRequest.EVENTNAME, new NetEventCompanionStateRequest(manager, this)},
                {CompanionDismissEvent.EVENTNAME, new NetEventDismissNPC(manager)},
                {SendBagEvent.EVENTNAME, new NetEventBagSent(manager)},
                {AIChangeState.EVENTNAME, new NetEventAIChangeState(manager)},
                {ShowHUDMessageHealed.EVENTNAME, new NetEventHUDMessageHealed(manager, loader)},
                {CompanionAttackAnimation.EVENTNAME, new NetEventCompanionAttackAnimation(manager)},
                {GameMasterStateSyncRequest.EVENTNAME, new NetEventGameMasterStateSyncRequest(gameMaster, this)},
                {GameMasterStateSyncResponse.EVENTNAME, new NetEventGameMasterStateSyncResponse(gameMaster)},
            };
        }

        private abstract class NetEventProcessor
        {
            public abstract void Process(NpcSyncEvent myEvent, Farmer owner);
            public abstract NpcSyncEvent Decode(ModMessageReceivedEventArgs e);
        }

        private class NetEventShowDialogue : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventShowDialogue(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override void Process(NpcSyncEvent npcEvent, Farmer owner)
            {
                DialogEvent dialogEvent = (DialogEvent)npcEvent;

                NPC n = this.manager.PossibleCompanions[dialogEvent.otherNpc].Companion;
                DialogueHelper.DrawDialogue(new Dialogue(DialogueHelper.GetSpecificDialogueText(n, owner, dialogEvent.Dialog), n));
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                DialogEvent myEvent = e.ReadAs<DialogEvent>();
                return myEvent;
            }
        }

        private class NetEventRecruitNPC : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventRecruitNPC(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionRequestEvent reqEvent = (CompanionRequestEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[reqEvent.otherNpc].currentState;
                if (n is AvailableState availableState)
                {
                    availableState.Recruit(owner);
                }
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionRequestEvent>();
            }
        }

        private class NetEventDismissNPC : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventDismissNPC(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionDismissEvent cde = (CompanionDismissEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[cde.otherNpc].currentState;
                if (n is RecruitedState rs)
                {
                    rs.StateMachine.Dismiss(owner, true);
                }
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionDismissEvent>();
            }
        }

        private class NetEventPlayerWarped : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventPlayerWarped(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<PlayerWarpedEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                PlayerWarpedEvent pwe = (PlayerWarpedEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[pwe.npc].currentState;
                if (n is RecruitedState recruitedState && n.GetByWhom().uniqueMultiplayerID == owner.uniqueMultiplayerID)
                {
                    NpcAdventureMod.GameMonitor.Log("Dispatching player warped to a recruited state...");

                    GameLocation from = Game1.getLocationFromName(pwe.warpedFrom);
                    GameLocation to = Game1.getLocationFromName(pwe.warpedTo);
                    recruitedState.PlayerHasWarped(from, to);
                }
            }
        }

        private class NetEventDialogueRequest : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventDialogueRequest(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<DialogueRequestEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                DialogueRequestEvent dre = (DialogueRequestEvent)myEvent;
                (this.manager.PossibleCompanions[dre.npc].currentState as IActionPerformer).PerformAction(owner, owner.currentLocation);
            }
        }

        private class NetEventQuestionRequest : NetEventProcessor
        {
            private CompanionManager manager;
            private NetEvents netbus;
            private IContentLoader contentLoader;

            public NetEventQuestionRequest(CompanionManager manager, NetEvents events, IContentLoader loader)
            {
                this.manager = manager;
                this.netbus = events;
                this.contentLoader = loader;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<QuestionEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                QuestionEvent qe = (QuestionEvent)myEvent;
                Response[] responses = new Response[qe.answers.Length];
                for (int i = 0; i < qe.answers.Length; i++)
                {
                    string response = this.contentLoader.LoadString("Strings/Strings:" + qe.question + "." + qe.answers[i]);
                    responses[i] = new Response(qe.answers[i], response);
                }

                NPC n = this.manager.PossibleCompanions[qe.npc].Companion;

                string question = this.contentLoader.LoadString("Strings/Strings:" + qe.question);
                owner.currentLocation.createQuestionDialogue(question, responses, (_, answer) => {
                    this.netbus.FireEvent(new QuestionResponse(qe.question, answer, n), owner);
                }, n);
            }
        }

        private class NetEventQuestionResponse : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventQuestionResponse(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<QuestionResponse>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                QuestionResponse qr = (QuestionResponse)myEvent;

                IDialogueDetector detector = this.manager.PossibleCompanions[qr.npc].currentState as IDialogueDetector;
                if (detector != null) {
                    detector.OnDialogueSpeaked(qr.question, qr.response);
                }
            }
        }

        private class NetEventCompanionStateRequest : NetEventProcessor
        {
            private CompanionManager manager;
            private NetEvents netBus;

            public NetEventCompanionStateRequest(CompanionManager manager, NetEvents netBus)
            {
                this.manager = manager;
                this.netBus = netBus;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionStateRequest>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                foreach (var csmkv in this.manager.PossibleCompanions)
                {
                    this.netBus.FireEvent(new CompanionChangedState(csmkv.Value.Companion, csmkv.Value.CurrentStateFlag, csmkv.Value.currentState.GetByWhom()), owner);
                }
            }
        }

        private class NetEventAIChangeState : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventAIChangeState(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<AIChangeState>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                AIChangeState aics = (AIChangeState)myEvent;
                RecruitedState state = this.manager.PossibleCompanions[aics.npc].currentState as RecruitedState;
                if (state != null)
                {
                    state.ai.ChangeStateLocal(aics.newState);
                }
            }
        }

        private class NetEventHUDMessageHealed : NetEventProcessor
        {
            private CompanionManager manager;
            private IContentLoader loader;
            public NetEventHUDMessageHealed(CompanionManager manager, IContentLoader loader)
            {
                this.manager = manager;
                this.loader = loader;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<ShowHUDMessageHealed>();
            }
            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                ShowHUDMessageHealed message = myEvent as ShowHUDMessageHealed;
                Game1.addHUDMessage(new HUDMessage(this.loader.LoadString($"Strings/Strings:{message.message}", this.manager.PossibleCompanions[message.npc].Companion.displayName, message.health)));
            }
        }

        private class NetEventBagSent : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventBagSent(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<SendBagEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                SendBagEvent sce = myEvent as SendBagEvent;
                if (this.manager.PossibleCompanions[sce.npc].currentState.GetByWhom() != owner)
                {
                    NpcAdventureMod.GameMonitor.Log("Somebody who has not set this state tried setting the bag of this NPC! I'm skipping this.", LogLevel.Warn);
                    return;
                }

                MemoryStream stream = new MemoryStream();
                byte[] data = Convert.FromBase64String(sce.chestContents);
                stream.Write(data, 0, data.Length);
                stream.Seek(0, SeekOrigin.Begin);
                BinaryReader reader = new BinaryReader(stream);
                this.manager.PossibleCompanions[sce.npc].Bag.NetFields.ReadFull(reader, new Netcode.NetVersion());
                reader.Dispose();
                NpcAdventureMod.GameMonitor.Log("Received bag of " + sce.npc + " with " + this.manager.PossibleCompanions[sce.npc].Bag.items.Count + " items");
                foreach (Item item in this.manager.PossibleCompanions[sce.npc].Bag.items)
                {
                    NpcAdventureMod.GameMonitor.Log("Item " + item.Name + " #" + item.Stack);
                }
            }
        }

        private class NetEventCompanionAttackAnimation : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventCompanionAttackAnimation(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionAttackAnimation>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionAttackAnimation caa = (CompanionAttackAnimation)myEvent;
                RecruitedState rs = manager.PossibleCompanions[caa.npc].currentState as RecruitedState;
                if (rs != null)
                {
                    FightController fc = rs.ai.CurrentController as FightController;
                    if (fc != null)
                    {
                        fc.AnimateMeLocal();
                    }
                }
            }
        }

        private class NetEventCompanionChangedState : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventCompanionChangedState(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionChangedState>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionChangedState ccs = (CompanionChangedState)myEvent;
                CompanionStateMachine n = manager.PossibleCompanions[ccs.npc];
                switch (ccs.NewState)
                {
                    case StateFlag.AVAILABLE:
                        n.MakeLocalAvailable(ccs.GetByWhom());
                        break;
                    case StateFlag.RECRUITED:
                        n.RecruitLocally(ccs.GetByWhom());
                        break;
                    case StateFlag.RESET:
                        n.ResetLocalStateMachine(ccs.GetByWhom());
                        break;
                    case StateFlag.UNAVAILABLE:
                        n.MakeLocalUnavailable(ccs.GetByWhom());
                        break;
                    default:

                        break;
                }
            }
        }

        public void Register(IModEvents events)
        {
            events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
            events.Specialized.LoadStageChanged += this.OnLoadStageChanged;
            events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
            events.Multiplayer.PeerContextReceived += this.PeerContextReceived;
        }

        private void PeerContextReceived(object sender, PeerContextReceivedEventArgs e)
        {
            bool foundRemoteMod = false;
            foreach (var mod in e.Peer.Mods)
            {
                if (mod.ID == this.modManifest.UniqueID && mod.Version == this.modManifest.Version)
                    foundRemoteMod = true;
            }

            if (!foundRemoteMod)
            {
                NpcAdventureMod.GameMonitor.Log("Disconnecting remote player as he doesn't have this mod in the same version!");
                // TODO disconnect the remote when I figure out how :V
            }
        }

        private void OnPeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            Farmer farmer = Game1.getFarmerMaybeOffline(e.Peer.PlayerID);
            if (farmer == null)
                return;

            this.companionManager.PlayerDisconnected(farmer);
        }

        private void OnLoadStageChanged(object sender, LoadStageChangedEventArgs e)
        {
            if (Context.IsMultiplayer && e.NewStage == StardewModdingAPI.Enums.LoadStage.Ready && !Game1.IsMasterGame)
            {
                this.FireEvent(new CompanionStateRequest());
            }
        }

        public void FireEvent(NpcSyncEvent myEvent, Farmer toWhom = null, bool isBroadcast = false) {
            if (isBroadcast)
            {
                foreach (Farmer farmer in Game1.getOnlineFarmers())
                    FireEvent(myEvent, farmer);
                return;
            }

            if (toWhom == null)
                toWhom = Game1.MasterPlayer;

            if (Context.IsMultiplayer && toWhom != Game1.player)
            {
                NpcAdventureMod.GameMonitor.Log("Sending message " + myEvent.Name + " to network", LogLevel.Info);
                helper.SendMessage<NpcSyncEvent>(myEvent, myEvent.Name, new string[] { this.modManifest.UniqueID }, new long[] { toWhom.uniqueMultiplayerID });
            }
            else
            {
                NpcAdventureMod.GameMonitor.Log("Delivering message" + myEvent.Name);
                NetEventProcessor eventProcessor = this.messages[myEvent.Name];
                eventProcessor.Process(myEvent, Game1.player);
            }
        }

        private void OnMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            NetEventProcessor processor = messages[e.Type];
            NpcSyncEvent npcEvent = processor.Decode(e);
            Farmer owner = Game1.getFarmer(e.FromPlayerID);
            NpcAdventureMod.GameMonitor.Log("Received message " + npcEvent.Name + " from " + owner.Name, LogLevel.Info);
            processor.Process(npcEvent, owner);
        }

        public abstract class NpcSyncEvent
        {
            public String Name;

            public NpcSyncEvent() { }

            public NpcSyncEvent(String Name)
            {
                this.Name = Name;
            }

        }

        public class DialogueRequestEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "dialogueRequestEvent";

            public string npc;
            public DialogueRequestEvent(NPC n) : base(EVENTNAME)
            {
                this.npc = n.Name;
            }
        }

        public class DialogEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "showDialogue";

            public string Dialog;
            public string otherNpc;

            public DialogEvent() { }

            public DialogEvent(string dialog, NPC otherNpc) : base(EVENTNAME)
            {
                this.Dialog = dialog;
                this.otherNpc = otherNpc.Name;
            }
        }

        public class QuestionEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "questionRequest";

            public string[] answers;
            public string question;
            public string npc;

            public QuestionEvent() : base()
            {

            }

            public QuestionEvent(string dialog, NPC otherNpc, string[] answers) : base(EVENTNAME)
            {
                this.question = dialog;
                this.npc = otherNpc.Name;
                this.answers = answers;
            }
        }

        public class QuestionResponse : NpcSyncEvent
        {
            public const string EVENTNAME = "questionResponse";
            public string question;
            public string response;
            public string npc;
            public QuestionResponse()
            {

            }

            public QuestionResponse(string question, string response, NPC n) : base(EVENTNAME)
            {
                this.question = question;
                this.npc = n.Name;
                this.response = response;
            }
        }


        public class CompanionRequestEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "companionRequest";
            public string otherNpc;

            public CompanionRequestEvent() { }
            public CompanionRequestEvent(NPC otherNpc) : base(EVENTNAME) {
                this.otherNpc = otherNpc.Name;
            }

        }

        public class GameMasterStateSyncRequest : NpcSyncEvent
        {
            public const string EVENTNAME = "gameMasterRequestStateSync";

            public GameMasterStateSyncRequest() : base(EVENTNAME) { }
        }

        public class GameMasterStateSyncResponse : NpcSyncEvent
        {
            public const string EVENTNAME = "gameMasterRequestState";
            public long FarmerID;

            public bool isEligible;
            public HashSet<string> recruited;
            public HashSet<int> completedQuests;

            public GameMasterStateSyncResponse()
            {
            }

            public GameMasterStateSyncResponse(long farmer, PlayerState state) : base(EVENTNAME)
            {
                this.FarmerID = farmer;
                this.recruited = state.recruited;
                this.completedQuests = state.completedQuests;
                this.isEligible = state.isEligible;
            }
        }

        public class CompanionDismissEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "companionDismiss";
            public string otherNpc;

            public CompanionDismissEvent() { }

            public CompanionDismissEvent(NPC otherNpc) : base(EVENTNAME)
            {
                this.otherNpc = otherNpc.Name;
            }
        }

        public class PlayerWarpedEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "playerWarped";
            public string warpedFrom;
            public string warpedTo;
            public string npc;
            public PlayerWarpedEvent() { }

            public PlayerWarpedEvent(NPC n, GameLocation from, GameLocation to) : base(EVENTNAME)
            {
                this.warpedFrom = from.NameOrUniqueName;
                this.warpedTo = to.NameOrUniqueName;
                this.npc = n.Name;
            }
        }

        public class CompanionChangedState : NpcSyncEvent
        {
            public const string EVENTNAME = "companionChangedState";
            public StateFlag NewState;
            public long byWhom;
            public string npc;

            public Farmer GetByWhom()
            {
                if (this.byWhom == 0)
                    return null;

                return Game1.getFarmer(this.byWhom);
            }

            public CompanionChangedState() { }

            public CompanionChangedState(NPC n, StateFlag NewState, Farmer byWhom) : base(EVENTNAME)
            {
                this.npc = n.Name;
                this.NewState = NewState;
                if (byWhom != null)
                    this.byWhom = byWhom.uniqueMultiplayerID;
            }
        }

        public class CompanionStateRequest : NpcSyncEvent
        {
            public const string EVENTNAME = "companionStateRequest";

            public CompanionStateRequest() : base(EVENTNAME) { }

        }

        public class SendBagEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "sendChestEvent";
            public string npc;
            public string chestContents;

            public SendBagEvent() { }

            public SendBagEvent(NPC n, Chest chest) : base(EVENTNAME)
            {
                MemoryStream stream = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(stream);
                chest.NetFields.WriteFull(writer);
                writer.Dispose();
                this.chestContents = Convert.ToBase64String(stream.ToArray());

                NpcAdventureMod.GameMonitor.Log("Trying to write " + chestContents);
                this.npc = n.Name;
            }
        }

        public class AIChangeState : NpcSyncEvent
        {
            public const string EVENTNAME = "aiChangeState";

            public string npc;
            public State newState;

            public AIChangeState() { }

            public AIChangeState(NPC n, State newState) : base(EVENTNAME)
            {
                this.npc = n.Name;
                this.newState = newState;
            }
        }

        public abstract class ShowHUDMessage : NpcSyncEvent
        { 
            public string npc;
            public string message;
            public int type;

            public ShowHUDMessage() { }

            public ShowHUDMessage(string eventname, NPC n, string message, int type) : base(eventname)
            {
                this.npc = n.Name;
                this.message = message;
                this.type = type;
            }

            public abstract object[] gatherArgs();

        }

        public class ShowHUDMessageHealed : ShowHUDMessage
        {
            public const string EVENTNAME = "showHudMessageHealed";

            public int health;

            public ShowHUDMessageHealed() { }

            public ShowHUDMessageHealed(NPC n, int health) : base(EVENTNAME, n, "healed", HUDMessage.health_type)
            {
                this.health = health;
            }

            public override object[] gatherArgs()
            {
                return new object[] { this.npc, this.health };
            }
        }

        public class CompanionAttackAnimation : NpcSyncEvent
        {
            public const string EVENTNAME = "companionAttackAnimation";

            public string npc;

            public CompanionAttackAnimation() { }

            public CompanionAttackAnimation(NPC n) : base(EVENTNAME)
            {
                this.npc = n.Name;
            }
        }

        private class NetEventGameMasterStateSyncRequest : NetEventProcessor
        {
            private GameMaster gameMaster;
            private NetEvents netEvents;

            public NetEventGameMasterStateSyncRequest(GameMaster gameMaster, NetEvents netEvents)
            {
                this.gameMaster = gameMaster;
                this.netEvents = netEvents;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<GameMasterStateSyncRequest>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                foreach(var foo in gameMaster.Data.EligiblePlayers)
                {
                    this.netEvents.FireEvent(new GameMasterStateSyncResponse(foo.Key, foo.Value), owner);
                }
            }
        }

        private class NetEventGameMasterStateSyncResponse : NetEventProcessor
        {
            private GameMaster gameMaster;

            public NetEventGameMasterStateSyncResponse(GameMaster gameMaster)
            {
                this.gameMaster = gameMaster;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<GameMasterStateSyncResponse>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                if (owner == Game1.player)
                    return; // do not update local data

                GameMasterStateSyncResponse gms = (GameMasterStateSyncResponse)myEvent;
                this.gameMaster.Data.SetPlayerStateFromNetwork(Game1.getFarmerMaybeOffline(gms.FarmerID), new PlayerState
                {
                    completedQuests = gms.completedQuests,
                    isEligible = gms.isEligible,
                    recruited = gms.recruited
                });

                NpcAdventureMod.GameMonitor.Log("Setting player " + Game1.getFarmerMaybeOffline(gms.FarmerID).Name + " iseligible to " + gms.isEligible + " with recruited " + gms.recruited.Count);
            }
        }
    }
}