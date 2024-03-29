﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public abstract class LogEvent {
        private static readonly Regex EVENT_SPLIT_EXP = new Regex(@"[.;]\s+", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static readonly Regex CHARACTER_SPLIT_EXP = new Regex(@"([,]|(and))\s+", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private const string CHARACTER_NAME = @"([^,}{@]+)|([@][^,}{@]+)|([@][{][^,}{@]+[}])";
        private static readonly string CHARACTER_LIST = $"({CHARACTER_NAME})(([,]|(and))\\s+({CHARACTER_NAME}))*";
        private static readonly string CHARACTER_SPEC = $"(?<characters>(everyone)|(everybody)|({CHARACTER_LIST}))";
        private static readonly Regex CHARACTER_SET_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(is|are) level (?<level>\\d+)( with (?<xp>\\d+)\\s?xp)?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_SET_XP_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+has (?<xp>\\d+)\\s?xp?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_JOIN_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+joined( (the )?party)?( at level (?<level>\\d+))?( with (?<xp>\\d+)\\s?xp)?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_ADJUST_LEVEL_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(?<gainloss>(gained)|(lost)) (?<level>(a)|(\\d+)) level(s?)$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_ADJUST_XP_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(?<gainloss>(gained)|(lost)) (?<xp>\\d+)\\s?xp$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_DEPART_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(departed)|(retired)|(left( (the )?party)?)$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        public LogReference reference;

        public LogEvent(LogReference reference) {
            this.reference = reference;
        }

        public static List<LogEvent> parse(LogReference reference) {
            List<LogEvent> events = new List<LogEvent>();
            foreach (string chunk in EVENT_SPLIT_EXP.Split(reference.line)) {
                LogEvent evt = parse_event(chunk.Trim(), reference);
                if (evt is not null) {
                    events.Add(evt);
                }
            }
            return events;
        }

        protected static LogEvent parse_event(string s, LogReference reference) {
            Match match = CHARACTER_SET_EXP.Match(s);
            if (!match.Success) {
                match = CHARACTER_JOIN_EXP.Match(s);
            }
            if (match.Success) {
                int level = 0, xp = 0;
                if (match.Groups["level"].Success) {
                    level = int.Parse(match.Groups["level"].Value);
                }
                if (match.Groups["xp"].Success) {
                    xp = int.Parse(match.Groups["xp"].Value);
                }
                return new CharacterSetEvent(reference, parse_characters(match.Groups["characters"].Value), level, xp);
            }
            match = CHARACTER_SET_XP_EXP.Match(s);
            if (match.Success) {
                int xp = int.Parse(match.Groups["xp"].Value);
                return new CharacterSetEvent(reference, parse_characters(match.Groups["characters"].Value), 0, xp);
            }
            match = CHARACTER_ADJUST_LEVEL_EXP.Match(s);
            if (match.Success) {
                int level;
                if (match.Groups["level"].Value == "a") {
                    level = 1;
                }
                else {
                    level = int.Parse(match.Groups["level"].Value);
                }
                if (match.Groups["gainloss"].Value == "lost") {
                    level = -level;
                }
                return new CharacterAdjustEvent(reference, parse_characters(match.Groups["characters"].Value), level, 0);
            }
            match = CHARACTER_ADJUST_XP_EXP.Match(s);
            if (match.Success) {
                int xp = int.Parse(match.Groups["xp"].Value);
                if (match.Groups["gainloss"].Value == "lost") {
                    xp = -xp;
                }
                return new CharacterAdjustEvent(reference, parse_characters(match.Groups["characters"].Value), 0, xp);
            }
            match = CHARACTER_DEPART_EXP.Match(s);
            if (match.Success) {
                return new CharacterDepartEvent(reference, parse_characters(match.Groups["characters"].Value));
            }
            //TODO: handle other event types
            return null;
        }

        protected static HashSet<string> parse_characters(string s) {
            HashSet<string> characters = new HashSet<string>();
            foreach (string chunk in CHARACTER_SPLIT_EXP.Split(s)) {
                string trimmed = chunk.Trim();
                if (trimmed == "") {
                    continue;
                }
                characters.Add(chunk);
            }
            return characters;
        }

        public abstract void apply(LogModel model, CampaignState state);

        protected static string get_full_character_name(LogModel model, CampaignState state, string name) {
            if ((name.StartsWith("@{")) && (name.EndsWith("}"))) {
                name = name[2..^1];
            }
            else if (name.StartsWith("@")) {
                name = name[1..];
            }
            if ((model.characters.ContainsKey(name)) || (state.characters.ContainsKey(name))) {
                return name;
            }
            Regex exp = new Regex($"\\b{name}\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            int matchCount = 0;
            string nameMatch = null;
            foreach (string charName in model.characters.Keys) {
                if (!exp.IsMatch(charName)) {
                    continue;
                }
                if (charName == nameMatch) {
                    continue;
                }
                nameMatch = charName;
                matchCount += 1;
            }
            foreach (string charName in state.characters.Keys) {
                if (!exp.IsMatch(charName)) {
                    continue;
                }
                if (charName == nameMatch) {
                    continue;
                }
                nameMatch = charName;
                matchCount += 1;
            }
            if (matchCount == 1) {
                return nameMatch;
            }
            return name;
        }
    }

    public abstract class CharacterEvent : LogEvent {
        // base class for events which operate on one or more characters; characters null => all current party
        public HashSet<string> characters;

        public CharacterEvent(LogReference reference, HashSet<string> characters) : base(reference) {
            this.characters = characters;
        }
    }

    public class CharacterSetEvent : CharacterEvent {
        public int level;
        public int xp;

        public CharacterSetEvent(
            LogReference reference, HashSet<string> characters, int level = 0, int xp = 0
        ) : base(reference, characters) {
            // add character(s) to party and set level and xp to specified values; level==0 => leave level alone
            // "X is level Y with Zxp" => CharacterSetEvent(X, Y, Z)
            // "X has Yxp" => CharacterSetEvent(X, 0, Y)
            // "X joined the party" => CharacterSetEvent(X, 0, 0)
            this.level = level;
            this.xp = xp;
        }

        public override void apply(LogModel model, CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(model, state, name);
                if (state.characters.ContainsKey(fullName)) {
                    if (this.level > 0) {
                        state.characters[fullName].level = this.level;
                    }
                    state.characters[fullName].xp = this.xp;
                }
                else {
                    state.characters[fullName] = new CharacterState(this.level, this.xp);
                }
                state.characters[fullName].references.Add(this.reference);
            }
        }
    }

    public class CharacterAdjustEvent : CharacterEvent {
        public int level;
        public int xp;

        public CharacterAdjustEvent(
            LogReference reference, HashSet<string> characters, int level = 0, int xp = 0
        ) : base(reference, characters) {
            // adjust character(s) level and xp by specified values; xp==0 => standard rules for xp adjustment based on level change
            // "X gained Yxp" => CharacterAdjustEvent(X, 0, Y)
            // "X lost Yxp" => CharacterAdjustEvent(X, 0, -Y)
            // "X gained a level" => CharacterAdjustEvent(X, 1, 0); xp -= previous_level * 1000
            // "X lost a level" => CharacterAdjustEvent(X, -1, 0); xp = new_level * 500 (i.e. half-way from new level to previous level)
            this.level = level;
            this.xp = xp;
        }

        public override void apply(LogModel model, CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(model, state, name);
                if (!state.characters.ContainsKey(fullName)) {
                    continue;
                }
                CharacterState character = state.characters[fullName];
                character.references.Add(this.reference);
                if (this.xp != 0) {
                    // explicit xp adjustment
                    character.level += this.level;
                    character.xp += this.xp;
                    continue;
                }
                // use standard rules for xp adjustment based on level change
                if (this.level > 0) {
                    // level gain: each level-up costs 1000xp * previous level
                    character.xp -= (character.level * 1000) + (this.level * (this.level - 1)) * 500;
                    character.level += this.level;
                }
                else if (this.level < 0) {
                    // level loss: set xp to midpoint of new level
                    character.level += this.level;
                    character.xp = this.level * 500;
                }
            }
        }
    }

    public class CharacterDepartEvent : CharacterEvent {
        // remove character(s) from the party
        // "X left the party" => CharacterDepartEvent()
        public CharacterDepartEvent(LogReference reference, HashSet<string> characters) : base(reference, characters) { }

        public override void apply(LogModel model, CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(model, state, name);
                if (state.characters.ContainsKey(fullName)) {
                    state.characters.Remove(fullName);
                }
            }
        }
    }
}