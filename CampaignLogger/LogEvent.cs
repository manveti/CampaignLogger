﻿using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CampaignLogger {
    public abstract class LogEvent {
        protected const string EVERY_CHARACTER = "__everyone__";
        private static readonly Regex EVENT_SPLIT_EXP = new Regex(@"[.;]\s+", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static readonly Regex TOPIC_EXP = new Regex(
            @"([#](?<name>[^][,;.:}{# ]+))|([#][{](?<name>[^][}{#]+)[}])",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_SPLIT_EXP = new Regex(
            @"((\s*[,]?\s+and)|[,])\s+",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private const string CHARACTER_WILDCARD = "(everyone)|(everybody)";
        private static readonly Regex CHARACTER_WILDCARD_EXP = new Regex(
            CHARACTER_WILDCARD, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private const string CHARACTER_NAME_EXPLICIT = @"([@][^][,;.:}{@ 0-9][^][,;.:}{@ ]*)|([@][{][^][}{@]+[}])";
        private static readonly string CHARACTER_NAME = $"([^,}}{{@]+)|{CHARACTER_NAME_EXPLICIT}";
        private static readonly Regex CHARACTER_NAME_EXP = new Regex(
            CHARACTER_NAME_EXPLICIT, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly string CHARACTER_LIST = $"({CHARACTER_NAME})(([,]|(and))\\s+({CHARACTER_NAME}))*";
        private static readonly string CHARACTER_SPEC = $"(?<characters>{CHARACTER_WILDCARD}|({CHARACTER_LIST}))";
        private const string BIGNUM_SPEC = @"\d+([km]?)";
        private static readonly Regex CHARACTER_SET_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(is|are) level (?<level>\\d+)( with (?<xp>{BIGNUM_SPEC})\\s?xp)?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_SET_XP_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+has (?<xp>{BIGNUM_SPEC})\\s?xp?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_JOIN_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+joined( (the )?party)?( at level (?<level>\\d+))?( with (?<xp>{BIGNUM_SPEC})\\s?xp)?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_ADJUST_LEVEL_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(?<gainloss>(gained)|(lost)) (?<level>(a)|(\\d+)) level(s?)$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_ADJUST_XP_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(?<gainloss>(gained)|(lost)) (?<xp>{BIGNUM_SPEC})\\s?xp$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex CHARACTER_DEPART_EXP = new Regex(
            $"^{CHARACTER_SPEC}\\s+(departed)|(retired)|(left( (the )?party)?)$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private const string EVENT_TIMESTAMP_PATTERN = @"(in\s+(?<interval>\S.*)|((on|at)\s+)?(?<timestamp>\S.*))";
        private static readonly Regex EVENT_TIMESTAMP_EXP = new Regex(
            $"^({EVENT_TIMESTAMP_PATTERN})$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );
        private static readonly Regex DUE_TIMESTAMP_EXP = new Regex(
            $"^((due)|(by)|(due\\s+by))\\s+(?<due>{EVENT_TIMESTAMP_PATTERN})$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        public LogReference reference;

        public LogEvent(LogReference reference) {
            this.reference = reference;
        }

        public static List<LogEvent> parse(LogReference reference) {
            List<LogEvent> events = new List<LogEvent>();
            HashSet<string> characters = new HashSet<string>(), needRefChars = new HashSet<string>();
            HashSet<string> topics = new HashSet<string>();
            HashSet<StateReference> stateRefs = new HashSet<StateReference>();
            foreach (string chunk in LogParser.split_line(reference.line)) {
                LogEvent evt = parse_event(chunk.Trim(), reference);
                if (evt is not null) {
                    events.Add(evt);
                    // make note of referenced elements
                    switch (evt) {
                    case CharacterEvent charEvt:
                        if (characters is null) {
                            break;
                        }
                        if (charEvt.characters is null) {
                            characters = null;
                        }
                        else {
                            characters.UnionWith(charEvt.characters);
                        }
                        break;
                    //TODO: inventory state references
                    case EventEvent eventEvt:
                        stateRefs.Add(new StateReference(StateReference.ReferenceType.Event, eventEvt.name));
                        break;
                    case TaskEvent taskEvt:
                        stateRefs.Add(new StateReference(StateReference.ReferenceType.Task, taskEvt.name));
                        break;
                    }
                }
            }
            // add topic references
            foreach (Match match in TOPIC_EXP.Matches(reference.line)) {
                topics.Add(match.Groups["name"].Value);
            }
            foreach (string topic in topics) {
                stateRefs.Add(new StateReference(StateReference.ReferenceType.Topic, topic));
            }
            if (characters is not null) {
                foreach (Match match in CHARACTER_NAME_EXP.Matches(reference.line)) {
                    string name = trim_character_name(match.Value);
                    if (!characters.Contains(name)) {
                        needRefChars.Add(name);
                    }
                    characters.Add(name);
                }
            }
            if (needRefChars.Count > 0) {
                events.Add(new CharacterReferenceEvent(reference, needRefChars));
            }
            if (characters is null) {
                characters = new HashSet<string> {
                    EVERY_CHARACTER
                };
            }
            if (topics.Count > 0) {
                foreach (string name in characters) {
                    stateRefs.Add(new StateReference(StateReference.ReferenceType.Character, name));
                }
                events.Add(new TopicReferenceEvent(reference, topics, stateRefs));
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
                    xp = parse_bignum(match.Groups["xp"].Value);
                }
                return new CharacterSetEvent(reference, parse_characters(match.Groups["characters"].Value), level, xp);
            }
            match = CHARACTER_SET_XP_EXP.Match(s);
            if (match.Success) {
                int xp = parse_bignum(match.Groups["xp"].Value);
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
                int xp = parse_bignum(match.Groups["xp"].Value);
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
            MarkupFunction function = LogParser.parse_function(s);
            if (function is not null) {
                LogEvent evt = parse_function(reference, function);
                if (evt is not null) {
                    return evt;
                }
            }
            return null;
        }

        protected static HashSet<string> parse_characters(string s) {
            HashSet<string> characters = new HashSet<string>();
            foreach (string chunk in LogParser.split_line(s, CHARACTER_SPLIT_EXP)) {
                string trimmed = chunk.Trim();
                if (trimmed == "") {
                    continue;
                }
                if (CHARACTER_WILDCARD_EXP.IsMatch(chunk)) {
                    // got a wildcard ("everyone" or "everybody"); return null for "all current characters"
                    return null;
                }
                characters.Add(chunk);
            }
            return characters;
        }

        protected static int parse_bignum(string s) {
            int multiplier = 1;
            if ((s.EndsWith("k")) || (s.EndsWith("m"))) {
                if (s.EndsWith("k")) {
                    multiplier = 1000;
                }
                else {
                    multiplier = 1000000;
                }
                s = s[0..^1];
            }
            return int.Parse(s) * multiplier;
        }

        protected static EventTimestamp parse_event_timestamp(string s) {
            Match match = EVENT_TIMESTAMP_EXP.Match(s);
            if (!match.Success) {
                return null;
            }
            if (match.Groups["timestamp"].Success) {
                return new EventTimestamp(match.Groups["timestamp"].Value, null);
            }
            if (match.Groups["interval"].Success) {
                return new EventTimestamp(null, match.Groups["interval"].Value);
            }
            return null;
        }

        protected static LogEvent parse_function(LogReference reference, MarkupFunction function) {
            switch (function.name) {
            //TODO: inventory functions
            case "event": {
                    if (function.args.Count < 2) {
                        return null;
                    }
                    string name = function.args[0];
                    EventTimestamp timestamp = parse_event_timestamp(function.args[1]);
                    string description = null;
                    if (function.args.Count > 2) {
                        StringBuilder descBuilder = new StringBuilder(function.args[2]);
                        for (int i = 3; i < function.args.Count; i++) {
                            descBuilder.Append("; ");
                            descBuilder.Append(function.args[i]);
                        }
                        description = descBuilder.ToString();
                    }
                    return new EventAddEvent(reference, name, timestamp, description);
                }
            case "event completed":
            case "event cleared":
            case "event done":
            case "event over":
            case "event remove":
                if (function.args.Count != 1) {
                    return null;
                }
                return new EventCompletionEvent(reference, function.args[0]);
            case "task": {
                    if (function.args.Count < 1) {
                        return null;
                    }
                    string name = function.args[0];
                    EventTimestamp due = null;
                    string description = null;
                    if (function.args.Count > 1) {
                        int startIdx = 1, endIdx = function.args.Count;
                        Match match = DUE_TIMESTAMP_EXP.Match(function.args[1]);
                        if (match.Success) {
                            due = parse_event_timestamp(match.Groups["due"].Value);
                            if (due is not null) {
                                startIdx += 1;
                            }
                        }
                        else {
                            match = DUE_TIMESTAMP_EXP.Match(function.args[^1]);
                            if (match.Success) {
                                due = parse_event_timestamp(match.Groups["due"].Value);
                                if (due is not null) {
                                    endIdx -= 1;
                                }
                            }
                        }
                        if (startIdx < endIdx) {
                            StringBuilder descBuilder = new StringBuilder(function.args[startIdx]);
                            for (int i = startIdx + 1; i < endIdx; i++) {
                                descBuilder.Append("; ");
                                descBuilder.Append(function.args[i]);
                            }
                            description = descBuilder.ToString();
                        }
                    }
                    return new TaskAddEvent(reference, name, description, due);
                }
            case "task completed":
            case "task cleared":
            case "task done":
            case "task failed":
            case "task over":
            case "task remove":
                if (function.args.Count != 1) {
                    return null;
                }
                return new TaskCompletionEvent(reference, function.args[0]);
            }
            return null;
        }

        public abstract void apply(CampaignState state);

        protected static string trim_character_name(string name) {
            if ((name.StartsWith("@{")) && (name.EndsWith("}"))) {
                return name[2..^1];
            }
            else if (name.StartsWith("@")) {
                return name[1..];
            }
            return name;
        }

        protected static string get_full_character_name(CampaignState state, string name) {
            name = trim_character_name(name);
            if ((state.model.characters.ContainsKey(name)) || (state.characters.ContainsKey(name))) {
                return name;
            }
            Regex exp = new Regex($"\\b{name}\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            int matchCount = 0;
            string nameMatch = null;
            foreach (string charName in state.model.characters.Keys) {
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

    public class TimestampEvent : LogEvent {
        public string timestamp;

        public TimestampEvent(LogReference reference, string timestamp) : base(reference) {
            this.timestamp = timestamp;
        }

        public override void apply(CampaignState state) {
            state.timestamp = this.timestamp;
        }
    }

    public class TopicReferenceEvent : LogEvent {
        public HashSet<string> topics;
        public HashSet<StateReference> relations;

        public TopicReferenceEvent(LogReference reference, HashSet<string> topics, HashSet<StateReference> relations) : base(reference) {
            this.topics = topics;
            this.relations = relations;
        }

        public override void apply(CampaignState state) {
            HashSet<StateReference> relations = new HashSet<StateReference>();
            foreach (StateReference stateRef in this.relations) {
                if (stateRef.type == StateReference.ReferenceType.Character) {
                    if (stateRef.name == EVERY_CHARACTER) {
                        foreach (string charName in state.characters.Keys) {
                            relations.Add(new StateReference(StateReference.ReferenceType.Character, charName));
                        }
                    }
                    else {
                        relations.Add(new StateReference(StateReference.ReferenceType.Character, get_full_character_name(state, stateRef.name)));
                    }
                }
                else {
                    relations.Add(stateRef);
                }
            }
            foreach (string topic in this.topics) {
                if (state.topics.ContainsKey(topic)) {
                    state.topics[topic].relations.UnionWith(relations);
                }
                else {
                    state.topics[topic] = new TopicState(relations);
                }
                // remove self-reference
                state.topics[topic].relations.Remove(new StateReference(StateReference.ReferenceType.Topic, topic));
                state.topics[topic].references.Add(this.reference);
            }
        }
    }

    public abstract class CharacterEvent : LogEvent {
        // base class for events which operate on one or more characters; characters null => all current party
        public HashSet<string> characters;

        public CharacterEvent(LogReference reference, HashSet<string> characters) : base(reference) {
            this.characters = characters;
        }
    }

    public class CharacterReferenceEvent : CharacterEvent {
        public CharacterReferenceEvent(LogReference reference, HashSet<string> characters) : base(reference, characters) { }

        public override void apply(CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(state, name);
                if (state.characters.ContainsKey(fullName)) {
                    state.characters[fullName].references.Add(this.reference);
                }
            }
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

        public override void apply(CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(state, name);
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

        public override void apply(CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(state, name);
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

        public override void apply(CampaignState state) {
            IEnumerable<string> characters = this.characters;
            if (characters is null) {
                characters = state.characters.Keys;
            }
            foreach (string name in characters) {
                string fullName = get_full_character_name(state, name);
                if (state.characters.ContainsKey(fullName)) {
                    state.characters.Remove(fullName);
                }
            }
        }
    }

    //TODO: inventory

    public abstract class EventEvent : LogEvent {
        public string name;

        public EventEvent(LogReference reference, string name) : base(reference) {
            this.name = name;
        }
    }

    public class EventAddEvent : EventEvent {
        public EventTimestamp timestamp;
        public string description;

        public EventAddEvent(LogReference reference, string name, EventTimestamp timestamp, string description) : base(reference, name) {
            this.timestamp = timestamp;
            this.description = description;
        }

        public override void apply(CampaignState state) {
            EventTimestamp timestamp = this.timestamp;
            if (timestamp.delta is not null) {
                timestamp = new EventTimestamp(state.timestamp, timestamp.delta);
            }
            if (!state.model.validate_event_timestamp(timestamp)) {
                //TODO: log error
                return;
            }
            state.events[this.name] = new EventState(timestamp, this.description);
            state.events[this.name].references.Add(this.reference);
        }
    }

    public class EventCompletionEvent : EventEvent {
        public EventCompletionEvent(LogReference reference, string name) : base(reference, name) { }

        public override void apply(CampaignState state) {
            state.events.Remove(this.name);
        }
    }

    public abstract class TaskEvent : LogEvent {
        public string name;

        public TaskEvent(LogReference reference, string name) : base(reference) {
            this.name = name;
        }
    }

    public class TaskAddEvent : TaskEvent {
        public string description;
        public EventTimestamp due;

        public TaskAddEvent(LogReference reference, string name, string description, EventTimestamp due) : base(reference, name) {
            this.description = description;
            this.due = due;
        }

        public override void apply(CampaignState state) {
            EventTimestamp timestamp = new EventTimestamp(state.timestamp, null);
            EventTimestamp due = this.due;
            if ((due is not null) && (due.delta is not null)) {
                due = new EventTimestamp(state.timestamp, due.delta);
            }
            if (!state.model.validate_event_timestamp(due)) {
                //TODO: log error
                return;
            }
            state.tasks[this.name] = new TaskState(timestamp, this.description, due);
            state.tasks[this.name].references.Add(this.reference);
        }
    }

    public class TaskCompletionEvent : TaskEvent {
        public TaskCompletionEvent(LogReference reference, string name) : base(reference, name) { }

        public override void apply(CampaignState state) {
            state.tasks.Remove(this.name);
        }
    }
}
