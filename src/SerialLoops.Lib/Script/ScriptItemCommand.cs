﻿using HaruhiChokuretsuLib.Archive.Event;
using HaruhiChokuretsuLib.Util;
using QuikGraph;
using QuikGraph.Algorithms.Observers;
using QuikGraph.Algorithms.Search;
using SerialLoops.Lib.Items;
using SerialLoops.Lib.Script.Parameters;
using SerialLoops.Lib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using static HaruhiChokuretsuLib.Archive.Event.EventFile;

namespace SerialLoops.Lib.Script
{
    public class ScriptItemCommand
    {
        public ScriptCommandInvocation Invocation { get; set; }
        public CommandVerb Verb { get; set; }
        public List<ScriptParameter> Parameters { get; set; }
        public ScriptSection Section { get; set; }
        public EventFile Script { get; set; }
        public int Index { get; set; }
        public Project Project { get; set; }

        public static ScriptItemCommand FromInvocation(ScriptCommandInvocation invocation, ScriptSection section, int index, EventFile eventFile, Project project, ILogger log)
        {
            return new()
            {
                Invocation = invocation,
                Verb = (CommandVerb)Enum.Parse(typeof(CommandVerb), invocation.Command.Mnemonic),
                Parameters = GetScriptParameters(invocation, section, eventFile, project, log),
                Section = section,
                Index = index,
                Script = eventFile,
                Project = project,
            };
        }

        public ScriptItemCommand()
        {
        }
        public ScriptItemCommand(ScriptSection section, EventFile script, int index, Project project, CommandVerb verb, params ScriptParameter[] parameters)
        {
            Section = section;
            Script = script;
            Index = index;
            Project = project;
            Verb = verb;
            Parameters = parameters.ToList();

            List<short> shortParams = parameters.SelectMany(p =>
            {
                return p.Type switch
                {
                    ScriptParameter.ParameterType.CHARACTER => p.GetValues(project.MessInfo),
                    ScriptParameter.ParameterType.CONDITIONAL or ScriptParameter.ParameterType.DIALOGUE or ScriptParameter.ParameterType.OPTION or ScriptParameter.ParameterType.SCRIPT_SECTION => p.GetValues(script),
                    _ => p.GetValues(),
                };
            }).ToList();
            shortParams.AddRange(new short[16 - shortParams.Count]);
            Invocation = new(CommandsAvailable.First(c => c.Mnemonic == verb.ToString())) { Parameters = shortParams };
        }
        public ScriptItemCommand(CommandVerb verb, params ScriptParameter[] parameters)
        {
            Verb = verb;
            Parameters = parameters.ToList();
        }

        public List<ScriptItemCommand> WalkCommandGraph(Dictionary<ScriptSection, List<ScriptItemCommand>> commandTree, AdjacencyGraph<ScriptSection, ScriptSectionEdge> graph)
        {
            List<ScriptItemCommand> commands = new();

            Func<ScriptSectionEdge, double> weightFunction = new((ScriptSectionEdge edge) =>
            {
                return 1;
            });

            if (Section != commandTree.Keys.First())
            {
                DepthFirstSearchAlgorithm<ScriptSection, ScriptSectionEdge> dfs = new(graph);
                var observer = new VertexPredecessorRecorderObserver<ScriptSection, ScriptSectionEdge>();
                using (observer.Attach(dfs))
                {
                    dfs.Compute(commandTree.Keys.First());
                }
                bool success = observer.TryGetPath(Section, out IEnumerable<ScriptSectionEdge> path);

                if (!success)
                {
                    return null;
                }

                foreach (ScriptSectionEdge edge in path)
                {
                    commands.AddRange(commandTree[edge.Source]);
                }
            }
            commands.AddRange(commandTree[Section].TakeWhile(c => c.Index != Index));
            commands.Add(this);

            return commands;
        }

        private static List<ScriptParameter> GetScriptParameters(ScriptCommandInvocation invocation, ScriptSection section, EventFile eventFile, Project project, ILogger log)
        {
            List<ScriptParameter> parameters = new();

            for (int i = 0; i < invocation.Parameters.Count; i++)
            {
                short parameter = invocation.Parameters[i];
                switch ((CommandVerb)Enum.Parse(typeof(CommandVerb), invocation.Command.Mnemonic))
                {
                    case CommandVerb.DIALOGUE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new DialogueScriptParameter("Dialogue", GetDialogueLine(parameter, eventFile)));
                                break;
                            case 1:
                                parameters.Add(new SpriteScriptParameter("Sprite", (CharacterSpriteItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Character_Sprite && parameter == ((CharacterSpriteItem)i).Index)));
                                break;
                            case 2:
                                parameters.Add(new SpriteEntranceScriptParameter("Sprite Entrance Transition", parameter));
                                break;
                            case 3:
                                parameters.Add(new SpriteExitScriptParameter("Sprite Exit/Move Transition", parameter));
                                break;
                            case 4:
                                parameters.Add(new SpriteShakeScriptParameter("Sprite Shake", parameter));
                                break;
                            case 5:
                                parameters.Add(new VoicedLineScriptParameter("Voice Line", (VoicedLineItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Voice && parameter == ((VoicedLineItem)i).Index)));
                                break;
                            case 6:
                                parameters.Add(new DialoguePropertyScriptParameter("Text Voice Font", (CharacterItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Character && ((CharacterItem)i).MessageInfo.Character == project.MessInfo.MessageInfos[parameter].Character)));
                                break;
                            case 7:
                                parameters.Add(new DialoguePropertyScriptParameter("Text Speed", (CharacterItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Character && ((CharacterItem)i).MessageInfo.Character == project.MessInfo.MessageInfos[parameter].Character)));
                                break;
                            case 8:
                                parameters.Add(new TextEntranceEffectScriptParameter("Text Entrance Effect", parameter));
                                break;
                            case 9:
                                parameters.Add(new ShortScriptParameter("Sprite Layer", parameter));
                                break;
                            case 10:
                                parameters.Add(new BoolScriptParameter("Don't Clear Text", parameter == 1));
                                break;
                            case 11:
                                parameters.Add(new BoolScriptParameter("Disable Lip Flap", parameter == 1));
                                break;

                        }
                        break;
                    case CommandVerb.KBG_DISP:
                        if (i == 0)
                        {
                            parameters.Add(new BgScriptParameter("\"Kinetic\" Background", (BackgroundItem)project.Items.First(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).Id == parameter), kinetic: true));
                        }
                        break;
                    case CommandVerb.PIN_MNL:
                        if (i == 0)
                        {
                            parameters.Add(new DialogueScriptParameter("Dialogue", GetDialogueLine(parameter, eventFile)));
                        }
                        break;
                    case CommandVerb.BG_DISP:
                    case CommandVerb.BG_DISP2:
                        if (i == 0)
                        {
                            ItemDescription bgItem = project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).Id == parameter)
                                ?? project.Items.First(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).BackgroundType == HaruhiChokuretsuLib.Archive.Data.BgType.TEX_BG);
                            parameters.Add(new BgScriptParameter("Background", (BackgroundItem)bgItem, kinetic: false));
                        }
                        break;
                    case CommandVerb.SCREEN_FADEIN:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ShortScriptParameter("Fade Time (Frames)", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Fade In Percentage", parameter));
                                break;
                            case 2:
                                parameters.Add(new ScreenScriptParameter("Location", parameter));
                                break;
                            case 3:
                                parameters.Add(new ColorMonochromeScriptParameter("Color", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.SCREEN_FADEOUT:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ShortScriptParameter("Fade Time (Frames)", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Fade Out Percentage", parameter));
                                break;
                            case 2:
                                parameters.Add(new ColorScriptParameter("Custom Color", parameter));
                                break;
                            case 3:
                                ((ColorScriptParameter)parameters.Last()).SetGreen(parameter);
                                break;
                            case 4:
                                ((ColorScriptParameter)parameters.Last()).SetBlue(parameter);
                                break;
                            case 5:
                                parameters.Add(new ScreenScriptParameter("Location", parameter));
                                break;
                            case 6:
                                parameters.Add(new ColorMonochromeScriptParameter("Color", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.SCREEN_FLASH:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ShortScriptParameter("Fade In Time (Frames)", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Hold Time (Frames)", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Fade Out Time (Frames)", parameter));
                                break;
                            case 3:
                                parameters.Add(new ColorScriptParameter("Color", parameter));
                                break;
                            case 4:
                                ((ColorScriptParameter)parameters.Last()).SetGreen(parameter);
                                break;
                            case 5:
                                ((ColorScriptParameter)parameters.Last()).SetBlue(parameter);
                                break;
                        }
                        break;
                    case CommandVerb.SND_PLAY:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new SfxScriptParameter("Sound", (SfxItem)project.Items.First(s => s.Type == ItemDescription.ItemType.SFX && ((SfxItem)s).Index == parameter)));
                                break;
                            case 1:
                                parameters.Add(new SfxModeScriptParameter("Mode", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Volume", parameter));
                                break;
                            case 3:
                                //parameters.Add(new ShortScriptParameter("crossfadeDupe", parameter));
                                break;
                            case 4:
                                parameters.Add(new ShortScriptParameter("Crossfade Time (Frames)", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.BGM_PLAY:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new BgmScriptParameter("Music", (BackgroundMusicItem)project.Items.First(i => i.Type == ItemDescription.ItemType.BGM && ((BackgroundMusicItem)i).Index == parameter)));
                                break;
                            case 1:
                                parameters.Add(new BgmModeScriptParameter("Mode", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Volume", parameter));
                                break;
                            case 3:
                                parameters.Add(new ShortScriptParameter("Fade In Time (Frames)", parameter));
                                break;
                            case 4:
                                parameters.Add(new ShortScriptParameter("Fade Out Time (Frames)", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.VCE_PLAY:
                        if (i == 0)
                        {
                            parameters.Add(new VoicedLineScriptParameter("Voice Line", (VoicedLineItem)project.Items.First(i => i.Type == ItemDescription.ItemType.Voice && parameter == ((VoicedLineItem)i).Index)));
                        }
                        break;
                    case CommandVerb.FLAG:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new FlagScriptParameter("Flag", parameter));
                                break;
                            case 1:
                                parameters.Add(new BoolScriptParameter("Set/Clear", parameter == 1));
                                break;
                        }
                        break;
                    case CommandVerb.TOPIC_GET:
                        if (i == 0)
                        {
                            parameters.Add(new TopicScriptParameter("Topic", parameter));
                        }
                        break;
                    case CommandVerb.TOGGLE_DIALOGUE:
                        if (i == 0)
                        {
                            parameters.Add(new BoolScriptParameter("Show", parameter == 1));
                        }
                        break;
                    case CommandVerb.SELECT:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new OptionScriptParameter("Option 1", eventFile.ChoicesSection.Objects[parameter]));
                                break;
                            case 1:
                                parameters.Add(new OptionScriptParameter("Option 2", eventFile.ChoicesSection.Objects[parameter]));
                                break;
                            case 2:
                                parameters.Add(new OptionScriptParameter("Option 3", eventFile.ChoicesSection.Objects[parameter]));
                                break;
                            case 3:
                                parameters.Add(new OptionScriptParameter("Option 4", eventFile.ChoicesSection.Objects[parameter]));
                                break;
                            case 4:
                                parameters.Add(new ShortScriptParameter("unknown08", parameter));
                                break;
                            case 5:
                                parameters.Add(new ShortScriptParameter("unknown0A", parameter));
                                break;
                            case 6:
                                parameters.Add(new ShortScriptParameter("unknown0C", parameter));
                                break;
                            case 7:
                                parameters.Add(new ShortScriptParameter("unknown0E", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.SCREEN_SHAKE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ShortScriptParameter("Duration (Frames)", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Horizontal Intensity", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Vertical Intensity", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.GOTO:
                        if (i == 0)
                        {
                            if (parameter == 0)
                            {
                                parameter = eventFile.LabelsSection.Objects.FirstOrDefault(l => l.Id != 00)?.Id ?? 0;
                                if (parameter == 0)
                                {
                                    log.LogError($"Adding GOTO command in section {section.Name} failed: no section with a label exists");
                                }
                            }
                            try
                            {
                                parameters.Add(new ScriptSectionScriptParameter("Script Section", eventFile.ScriptSections.First(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                            }
                            catch (InvalidOperationException)
                            {
                                log.LogWarning($"Failed to evaluate script section for GOTO command in section {section.Name}: references a non-existent section. Resetting!");
                                parameter = eventFile.LabelsSection.Objects.FirstOrDefault(l => l.Id != 00)?.Id ?? 0;
                                if (parameter == 0)
                                {
                                    log.LogError($"Adding GOTO command in section {section.Name} failed: no section with a label exists!");
                                }
                                else
                                {
                                    parameters.Add(new ScriptSectionScriptParameter("Script Section", eventFile.ScriptSections.First(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                }
                            }
                        }
                        break;
                    case CommandVerb.SCENE_GOTO:
                    case CommandVerb.SCENE_GOTO2:
                        if (i == 0)
                        {
                            parameters.Add(new ConditionalScriptParameter("Scene", eventFile.ConditionalsSection.Objects[parameter]));
                        }
                        break;
                    case CommandVerb.WAIT:
                        if (i == 0)
                        {
                            parameters.Add(new ShortScriptParameter("Wait Time (Frames)", parameter));
                        }
                        break;
                    case CommandVerb.VGOTO:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ConditionalScriptParameter("Conditional", eventFile.ConditionalsSection.Objects[parameter]));
                                break;
                            // 1 is unused
                            case 2:
                                if (parameter == 0)
                                {
                                    parameter = eventFile.LabelsSection.Objects.FirstOrDefault(l => l.Id != 00)?.Id ?? 0;
                                    if (parameter == 0)
                                    {
                                        log.LogError($"Adding VGOTO command in section {section.Name} failed: no section with a label exists");
                                    }
                                }
                                try
                                {
                                    parameters.Add(new ScriptSectionScriptParameter("Script Section", eventFile.ScriptSections.First(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                }
                                catch (InvalidOperationException)
                                {
                                    log.LogWarning($"Failed to evaluate script section for VGOTO command in section {section.Name}: references a non-existent section. Resetting!");
                                    parameter = eventFile.LabelsSection.Objects.FirstOrDefault(l => l.Id != 00)?.Id ?? 0;
                                    if (parameter == 0)
                                    {
                                        log.LogError($"Adding GOTO command in section {section.Name} failed: no section with a label exists!");
                                    }
                                    else
                                    {
                                        parameters.Add(new ScriptSectionScriptParameter("Script Section", eventFile.ScriptSections.First(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                    }
                                }
                                break;
                        }
                        break;
                    case CommandVerb.HARUHI_METER:
                        switch (i)
                        {
                            // 0 is unused
                            case 1:
                                parameters.Add(new ShortScriptParameter("Add", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Set", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.HARUHI_METER_NOSHOW:
                        if (i == 0)
                        {
                            parameters.Add(new ShortScriptParameter("Add", parameter));
                        }
                        break;
                    case CommandVerb.PALEFFECT:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new PaletteEffectScriptParameter("Mode", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Time (Frames)", parameter));
                                break;
                            case 2:
                                parameters.Add(new BoolScriptParameter("Unknown", parameter > 0));
                                break;
                        }
                        break;
                    case CommandVerb.BG_FADE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new BgScriptParameter("Background", (BackgroundItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).Id == parameter), kinetic: false));
                                break;
                            case 1:
                                parameters.Add(new BgScriptParameter("Background (Temp/CG)", (BackgroundItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).Id == parameter), kinetic: false));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Fade Time (Frames)", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.TRANS_OUT:
                    case CommandVerb.TRANS_IN:
                        if (i == 0)
                        {
                            parameters.Add(new TransitionScriptParameter("Transition", parameter));
                        }
                        break;
                    case CommandVerb.SET_PLACE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new BoolScriptParameter("Display?", parameter == 1));
                                break;
                            case 1:
                                parameters.Add(new PlaceScriptParameter("Place", (PlaceItem)project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Place && ((PlaceItem)i).Index == parameter)));
                                break;
                        }
                        break;
                    case CommandVerb.ITEM_DISPIMG:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ItemScriptParameter("Item", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("X", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Y", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.LOAD_ISOMAP:
                        if (i == 0)
                        {
                            parameters.Add(new MapScriptParameter("Map", (MapItem)project.Items.First(i => i.Type == ItemDescription.ItemType.Map && (parameter) == ((MapItem)i).Map.Index)));
                        }
                        break;
                    case CommandVerb.INVEST_START:
                        switch (i)
                        {
                            case 0:
                            case 1:
                            case 2:
                            case 3:
                                parameters.Add(new ShortScriptParameter($"unknown0{i}", parameter));
                                break;
                            case 4:
                                parameters.Add(new ScriptSectionScriptParameter("End Script Section", eventFile.ScriptSections.First(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                break;
                        }
                        break;
                    case CommandVerb.CHIBI_EMOTE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ChibiScriptParameter("Chibi", (ChibiItem)project.Items.First(i => i.Type == ItemDescription.ItemType.Chibi && (parameter) == ((ChibiItem)i).ChibiIndex)));
                                break;
                            case 1:
                                parameters.Add(new ChibiEmoteScriptParameter("Emote", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.SKIP_SCENE:
                        if (i == 0)
                        {
                            parameters.Add(new ShortScriptParameter("Scenes to Skip", parameter));
                        }
                        break;
                    case CommandVerb.MODIFY_FRIENDSHIP:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new FriendshipLevelScriptParameter("Character", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Modify by", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.CHIBI_ENTEREXIT:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ChibiScriptParameter("Chibi", (ChibiItem)project.Items.First(i => i.Type == ItemDescription.ItemType.Chibi && (parameter) == ((ChibiItem)i).ChibiIndex)));
                                break;
                            case 1:
                                parameters.Add(new ChibiEnterExitScriptParameter("Enter/Exit", parameter));
                                break;
                            case 2:
                                parameters.Add(new ShortScriptParameter("Delay (Frames)", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.GLOBAL2D:
                        if (i == 0)
                        {
                            parameters.Add(new ShortScriptParameter("Value", parameter));
                        }
                        break;
                    case CommandVerb.CHESS_LOAD:
                        if (i == 0)

                        {
                            parameters.Add(new ChessFileScriptParameter("Chess File", parameter));
                        }
                        break;
                    case CommandVerb.CHESS_VGOTO:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ScriptSectionScriptParameter("Clear Block", eventFile.ScriptSections.FirstOrDefault(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                break;
                            case 1:
                                parameters.Add(new ScriptSectionScriptParameter("Miss Block", eventFile.ScriptSections.FirstOrDefault(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                break;
                            case 2:
                                parameters.Add(new ScriptSectionScriptParameter("Miss 2 Block", eventFile.ScriptSections.FirstOrDefault(s => s.Name == eventFile.LabelsSection.Objects.First(l => l.Id == parameter).Name.Replace("/", ""))));
                                break;
                        }
                        break;
                    case CommandVerb.CHESS_MOVE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ChessSpaceScriptParameter("White Space Begin", parameter));
                                break;
                            case 1:
                                parameters.Add(new ChessSpaceScriptParameter("White Space End", parameter));
                                break;
                            case 2:
                                parameters.Add(new ChessSpaceScriptParameter("Black Space Begin", parameter));
                                break;
                            case 3:
                                parameters.Add(new ChessSpaceScriptParameter("Black Space End", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.CHESS_TOGGLE_GUIDE:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new ChessPieceScriptParameter("Piece 1", parameter));
                                break;
                            case 1:
                                parameters.Add(new ChessPieceScriptParameter("Piece 2", parameter));
                                break;
                            case 2:
                                parameters.Add(new ChessPieceScriptParameter("Piece 3", parameter));
                                break;
                            case 3:
                                parameters.Add(new ChessPieceScriptParameter("Piece 4", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.CHESS_TOGGLE_HIGHLIGHT:
                        if (parameter != -1)
                        {
                            parameters.Add(new ChessSpaceScriptParameter($"Highlight Space {i}", parameter));
                        }
                        break;
                    case CommandVerb.CHESS_TOGGLE_CROSS:
                        if (parameter != -1)
                        {
                            parameters.Add(new ChessSpaceScriptParameter($"Cross Space {i}", parameter));
                        }
                        break;
                    case CommandVerb.EPHEADER:
                        if (i == 0)
                        {
                            parameters.Add(new EpisodeHeaderScriptParameter("Episode Header", parameter));
                        }
                        break;
                    case CommandVerb.CONFETTI:
                        if (i == 0)

                        {
                            parameters.Add(new BoolScriptParameter("Visible?", parameter == 1));
                        }
                        break;
                    case CommandVerb.BG_DISPCG:
                        switch (i)
                        {
                            case 0:
                                ItemDescription cgItem = project.Items.FirstOrDefault(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).Id == parameter)
                                    ?? project.Items.First(i => i.Type == ItemDescription.ItemType.Background && ((BackgroundItem)i).BackgroundType == HaruhiChokuretsuLib.Archive.Data.BgType.TEX_CG);

                                parameters.Add(new BgScriptParameter("Background", (BackgroundItem)cgItem, kinetic: false));
                                break;
                            case 1:
                                parameters.Add(new BoolScriptParameter("Display from Bottom", parameter == 1));
                                break;
                        }
                        break;
                    case CommandVerb.BG_SCROLL:
                        switch (i)
                        {
                            case 0:
                                parameters.Add(new BgScrollDirectionScriptParameter("Scroll Direction", parameter));
                                break;
                            case 1:
                                parameters.Add(new ShortScriptParameter("Scroll Speed", parameter));
                                break;
                        }
                        break;
                    case CommandVerb.WAIT_CANCEL:
                        if (i == 0)
                        {
                            parameters.Add(new ShortScriptParameter("Wait Time (Frames)", parameter));
                        }
                        break;
                }
            }

            return parameters;
        }

        public override string ToString()
        {
            string str = $"{Verb}";
            if (Verb == CommandVerb.DIALOGUE)
            {
                str += $" {((DialogueScriptParameter)Parameters[0]).Line.Text.GetSubstitutedString(Project)[0..Math.Min(((DialogueScriptParameter)Parameters[0]).Line.Text.Length, 10)]}...";
            }
            else if (Verb == CommandVerb.GOTO)
            {
                str += $" {((ScriptSectionScriptParameter)Parameters[0]).Section.Name}";
            }
            else if (Verb == CommandVerb.VGOTO)
            {
                str += $" {((ConditionalScriptParameter)Parameters[0]).Conditional}, {((ScriptSectionScriptParameter)Parameters[1]).Section.Name}";
            }
            return str;
        }

        private static DialogueLine GetDialogueLine(short index, EventFile eventFile)
        {
            return eventFile.DialogueSection.Objects[index];
        }

        public ScriptItemCommand Clone()
        {
            return new()
            {
                Invocation = Invocation,
                Verb = Verb,
                Parameters = Parameters.Select(p => p.Clone(Project, Script)).ToList(),
                Section = Section,
                Index = Index,
                Script = Script,
                Project = Project,
            };
        }

    }
}
