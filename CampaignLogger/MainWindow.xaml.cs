using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace CampaignLogger {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private static readonly TimeSpan TYPING_POLL_INTERVAL = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan TYPING_DELAY = TimeSpan.FromSeconds(5);

        private static readonly Regex COMMENT_EXP = new Regex(@"^(?<content>.*?)\s*//.*$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static readonly Regex TOKEN_END_EXP = new Regex(
            @"^[\s,;]?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex TOKEN_SPLIT_EXP = new Regex(
            @"[\s,;]", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex CHARACTER_EXP = new Regex(
            @"^((?<player>[^:]+):)?\s+([(](?<departure>[^)]+)[)]\s+)?(?<name>[^(\s][^(]*?)(\s+[(](?<desc>[^)]+)[)])?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex SESSION_EXP = new Regex(
            @"^s(?<relative>[+])?(?<id>\d+)\s+[(](?<date>[^)]+)[)]:$", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex SESSION_ENTRY_EXP = new Regex(
            @"^\d{4}:\s+(?<line>.*)", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex SESSION_ENTRY_CONTINUATION_EXP = new Regex(
            @"^\s+(?<continuation>.+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
        private static readonly Regex TIMESTAMP_EXP = new Regex(
            @"^(?<timestamp>.+?)(\s+[(]continued[)])?:$", RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        private class CharacterEntry {
            private string _name;
            private string _level;
            private string _xp;

            public string name => this._name;
            public string level => this._level;
            public string xp => this._xp;

            public CharacterEntry(string name, int level = 0, int xp = 0) {
                this._name = name;
                this._level = (level > 0 ? level.ToString() : "");
                this._xp = (level > 0 ? xp.ToString() : "");
            }
        }

        //TODO: display type for inventory

        private class EventEntry {
            private string _name;
            private string _timestamp;
            private string _delta;

            public string name => this._name;
            public string timestamp => this._timestamp;
            public string delta => this._delta;

            public EventEntry(string name, string timestamp, string delta) {
                this._name = name;
                this._timestamp = timestamp;
                this._delta = delta;
            }
        }

        private class TaskEntry {
            private string _name;
            private string _due;
            private string _delta;

            public string name => this._name;
            public string due => this._due;
            public string delta => this._delta;

            public TaskEntry(string name, string due, string delta) {
                this._name = name;
                this._due = due;
                this._delta = delta;
            }
        }

        private class SessionEntry {
            private SessionRecord _session;
            private string _index;
            private string _date;

            public SessionRecord session => this._session;
            public string index => this._index;
            public string date => this._date;

            public SessionEntry(SessionRecord session) {
                this._session = session;
                this._index = (session.is_relative ? "+" : "") + session.index.ToString();
                this._date = session.date;
            }
        }

        private class ReferenceEntry {
            public LogReference _reference;
            private string _session;

            public LogReference reference => this._reference;
            public string session => this._session;
            public string line => this._reference.line;

            public ReferenceEntry(LogReference reference) {
                this._reference = reference;
                this._session = (reference.session.is_relative ? "+" : "") + reference.session.index.ToString();
            }
        }

        private class CompletionEntry : ICompletionData {
            public const double PRIORITY_DEFAULT = 5;
            public const double PRIORITY_LOW = 1;

            private string _text;
            private string _content;
            private double _priority;

            public string Text {
                get => this._text;
                set => this._text = value;
            }
            public object Content => this._content;
            public object Description => this._content;
            public double Priority => this._priority;
            public System.Windows.Media.ImageSource Image => null;

            public CompletionEntry(string text, double priority = PRIORITY_DEFAULT) {
                this._text = text;
                this._content = text;
                this._priority = priority;
            }

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) {
                string repl = this._text;
                if ((TOKEN_SPLIT_EXP.IsMatch(repl)) && (!repl.StartsWith("{"))) {
                    repl = "{" + repl + "}";
                }
                textArea.Document.Replace(completionSegment, repl);
            }
        }

        private LogModel model;
        private DateTime? header_update_due = null;
        private DateTime? timeline_update_due = null;
        private int timeline_update_session;
        private bool timeline_update_session_dirty;
        private DispatcherTimer dispatcher_timer;
        private List<string> topics_display;
        private List<CharacterEntry> party_display;
        //TODO: inventory display
        private List<EventEntry> events_display;
        private List<TaskEntry> tasks_display;
        private List<SessionEntry> sessions_display;
        private TopicInfoControl topic_info;
        private CharacterInfoControl character_info;
        //TODO: other info controls
        private EventInfoControl event_info;
        private TaskInfoControl task_info;
        private SessionInfoControl session_info;
        private List<ReferenceEntry> references_display;
        private CompletionWindow completion_window = null;
        private bool completion_first;

        public MainWindow() {
            this.model = new LogModel();
            this.dispatcher_timer = new DispatcherTimer();
            this.dispatcher_timer.Interval = TYPING_POLL_INTERVAL;
            this.dispatcher_timer.Tick += this.log_update_timer_tick;
            this.topics_display = new List<string>();
            this.party_display = new List<CharacterEntry>();
            //TODO: inventory display
            this.events_display = new List<EventEntry>();
            this.tasks_display = new List<TaskEntry>();
            this.sessions_display = new List<SessionEntry>();
            this.topic_info = new TopicInfoControl(this);
            this.character_info = new CharacterInfoControl();
            //TODO: other info controls
            this.event_info = new EventInfoControl();
            this.task_info = new TaskInfoControl();
            this.session_info = new SessionInfoControl();
            this.references_display = new List<ReferenceEntry>();
            InitializeComponent();
            this.log_box.TextArea.Options.IndentationSize = 8;
            this.log_box.TextArea.TextEntering += this.on_log_text_entering;
            this.log_box.TextArea.TextEntered += this.on_log_text_entered;
            this.log_box.Document.Changing += this.on_log_change;
            this.topic_list.ItemsSource = this.topics_display;
            this.party_list.ItemsSource = this.party_display;
            //TODO: inventory display
            this.events_list.ItemsSource = this.events_display;
            this.tasks_list.ItemsSource = this.tasks_display;
            this.session_list.ItemsSource = this.sessions_display;
            this.reference_list.ItemsSource = this.references_display;
            this.dispatcher_timer.Start();
        }

        private void update_topic_list() {
            string selected = this.topic_list.SelectedValue as string;
            bool selectionValid = (selected is not null) && (this.model.campaign_state.topics.ContainsKey(selected));
            this.topics_display.Clear();
            this.topics_display.AddRange(this.model.campaign_state.topics.Keys);
            this.topics_display.Sort();
            this.topic_list.Items.Refresh();
            if (selectionValid) {
                this.topic_list.SelectedValue = selected;
            }
        }

        private void update_party_list() {
            string selected = this.party_list.SelectedValue as string;
            bool selectionValid = (selected is not null) && (this.model.campaign_state.characters.ContainsKey(selected));
            HashSet<string> seenCharacters = new HashSet<string>();
            this.party_display.Clear();
            foreach (string name in this.model.campaign_state.characters.Keys) {
                CharacterState character = this.model.campaign_state.characters[name];
                this.party_display.Add(new CharacterEntry(name, character.level, character.xp));
                seenCharacters.Add(name);
            }
            if (this.party_departed_box.IsChecked == true) {
                // add characters listed in the header section but not currently in the party
                foreach (string name in this.model.characters.Keys) {
                    if (seenCharacters.Contains(name)) {
                        continue;
                    }
                    this.party_display.Add(new CharacterEntry(name));
                }
                if ((selected is not null) && (this.model.characters.ContainsKey(selected))) {
                    selectionValid = true;
                }
            }
            this.party_display.Sort((x, y) => x.name.CompareTo(y.name));
            this.party_list.Items.Refresh();
            fix_listview_column_widths(this.party_list);
            if (selectionValid) {
                this.party_list.SelectedValue = selected;
            }
        }

        //TODO: update inventory list

        private void update_events_list() {
            if (this.model.calendar is null) {
                return;
            }
            string selected = this.events_list.SelectedValue as string;
            bool selectionValid = (selected is not null) && (this.model.campaign_state.events.ContainsKey(selected));
            this.events_display.Clear();
            bool showPast = (this.events_past_box.IsChecked == true);
            foreach (string name in this.model.campaign_state.events.Keys) {
                EventState evt = this.model.campaign_state.events[name];
                if ((!showPast) && (this.model.calendar.compare_timestamps(evt.timestamp, this.model.campaign_state.timestamp) < 0)) {
                    // event timestamp is in the past; skip it
                    continue;
                }
                string timestamp = this.model.calendar.format_timestamp(evt.timestamp);
                string delta = this.model.calendar.subtract_timestamp(evt.timestamp, this.model.campaign_state.timestamp);
                this.events_display.Add(new EventEntry(name, timestamp, delta ?? ""));
            }
            this.events_display.Sort(
                (x, y) => this.model.calendar.compare_timestamps(
                    this.model.campaign_state.events[x.name].timestamp, this.model.campaign_state.events[y.name].timestamp
                )
            );
            this.events_list.Items.Refresh();
            fix_listview_column_widths(this.events_list);
            if (selectionValid) {
                this.events_list.SelectedValue = selected;
            }
        }

        private int task_list_cmp(string t1, string t2) {
            TaskState task1 = this.model.campaign_state.tasks[t1];
            TaskState task2 = this.model.campaign_state.tasks[t2];
            int result = 0;
            // treat null due as infinite (i.e. push to end of list)
            if (task1.due is null) {
                if (task2 is not null) {
                    return 1;
                }
            }
            else if (task2.due is null) {
                return -1;
            }
            else {
                result = this.model.calendar.compare_timestamps(task1.due, task2.due);
            }
            if (result != 0) {
                return result;
            }
            // due dates the same; fall back to when the task was taken on (most recent first)
            return this.model.calendar.compare_timestamps(task2.timestamp, task1.timestamp);
        }

        private void update_tasks_list() {
            string selected = this.tasks_list.SelectedValue as string;
            bool selectionValid = (selected is not null) && (this.model.campaign_state.tasks.ContainsKey(selected));
            this.tasks_display.Clear();
            foreach (string name in this.model.campaign_state.tasks.Keys) {
                TaskState task = this.model.campaign_state.tasks[name];
                string due = "", delta = "";
                if (this.model.calendar is not null) {
                    due = this.model.calendar.format_timestamp(task.due);
                    delta = this.model.calendar.subtract_timestamp(task.due, this.model.campaign_state.timestamp);
                }
                this.tasks_display.Add(new TaskEntry(name, due, delta ?? ""));
            }
            if (this.model.calendar is not null) {
                this.tasks_display.Sort((x, y) => this.task_list_cmp(x.name, y.name));
            }
            else {
                this.tasks_display.Sort((x, y) => x.name.CompareTo(y.name));
            }
            this.tasks_list.Items.Refresh();
            fix_listview_column_widths(this.tasks_list);
            if (selectionValid) {
                this.tasks_list.SelectedValue = selected;
            }
        }

        private void update_session_list() {
            SessionRecord selected = this.session_list.SelectedValue as SessionRecord;
            bool selectionValid = false;
            this.sessions_display.Clear();
            foreach (SessionRecord session in this.model.sessions) {
                this.sessions_display.Add(new SessionEntry(session));
                if (session == selected) {
                    selectionValid = true;
                }
            }
            this.sessions_display.Reverse();
            this.session_list.Items.Refresh();
            fix_listview_column_widths(this.session_list);
            if (selectionValid) {
                this.session_list.SelectedValue = selected;
            }
        }

        private void update_reference_list(List<LogReference> references) {
            this.references_display.Clear();
            if (references is not null) {
                foreach (LogReference entry in references) {
                    this.references_display.Add(new ReferenceEntry(entry));
                }
            }
            this.references_display.Reverse();
            this.reference_list.Items.Refresh();
            fix_listview_column_widths(this.reference_list);
        }

        private void update_topic_references() {
            string selected = this.topic_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.topics.ContainsKey(selected))) {
                this.update_reference_list(this.model.campaign_state.topics[selected].references);
            }
            else {
                this.update_reference_list(null);
            }
        }

        private void update_party_references() {
            string selected = this.party_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.characters.ContainsKey(selected))) {
                this.update_reference_list(this.model.campaign_state.characters[selected].references);
            }
            else {
                this.update_reference_list(null);
            }
        }

        //TODO: other update_*_references

        private void update_event_references() {
            string selected = this.events_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.events.ContainsKey(selected))) {
                this.update_reference_list(this.model.campaign_state.events[selected].references);
            }
            else {
                this.update_reference_list(null);
            }
        }

        private void update_task_references() {
            string selected = this.tasks_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.tasks.ContainsKey(selected))) {
                this.update_reference_list(this.model.campaign_state.tasks[selected].references);
            }
            else {
                this.update_reference_list(null);
            }
        }

        private void update_session_references() {
            List<LogReference> references = new List<LogReference>();
            SessionRecord selected = this.session_list.SelectedValue as SessionRecord;
            if (selected is not null) {
                string index = (selected.is_relative ? "+" : "") + (selected.index.ToString());
                string line = $"s{index} ({selected.date})";
                references.Add(new LogReference(selected, line, selected.start, selected.end));
            }
            this.update_reference_list(references);
        }

        private bool handle_header_function(MarkupFunction function) {
            switch (function.name) {
            case "calendar":
                if (function.args.Count != 1) {
                    //TODO: log error
                    return false;
                }
                this.model.calendar = Calendar.get_calendar(function.args[0]);
                return true;
            }
            return false;
        }

        private void do_header_update() {
            HashSet<string> unreferencedChars = new HashSet<string>(this.model.characters.Keys);
            int endLine = this.log_box.Document.LineCount;
            if ((this.model.header_section_end is not null) && (this.model.header_section_end.Line - 1 < endLine)) {
                // TextAnchor line indices are 1-based
                endLine = this.model.header_section_end.Line - 1;
            }
            string player = null;
            bool gotEmpty = false, gotHeader = false;
            for (int i = 0; i < endLine; i++) {
                DocumentLine lineSpec = this.log_box.Document.Lines[i];
                string line = this.log_box.Document.GetText(lineSpec.Offset, lineSpec.Length);
                if ((line == "") && (!gotEmpty)) {
                    // got a valid empty line; note that and move on
                    gotEmpty = true;
                    continue;
                }
                Match match = COMMENT_EXP.Match(line);
                if (match.Success) {
                    // trim out end-of-line comment
                    line = match.Groups["content"].Value;
                    if (line == "") {
                        // comment was whole line; skip line
                        continue;
                    }
                }
                MarkupFunction function = LogParser.parse_function(line);
                if (function is not null) {
                    // got a valid markup function; handle it
                    if (this.handle_header_function(function)) {
                        gotHeader = true;
                    }
                    continue;
                }
                match = CHARACTER_EXP.Match(line);
                if ((match.Success) && ((player is not null) || (match.Groups["player"].Success))) {
                    // got a valid character line; handle it
                    gotHeader = true;
                    if (match.Groups["player"].Success) {
                        player = match.Groups["player"].Value;
                    }
                    if (match.Groups["name"].Value.Equals("gm", StringComparison.OrdinalIgnoreCase)) {
                        // skip "gm" meta-character
                        continue;
                    }
                    if (this.model.characters.ContainsKey(match.Groups["name"].Value)) {
                        CharacterExtraInfo character = this.model.characters[match.Groups["name"].Value];
                        character.player = player;
                        if (match.Groups["description"].Success) {
                            character.description = match.Groups["description"].Value;
                        }
                        character.departure = (match.Groups["departure"].Success ? match.Groups["departure"].Value : null);
                    }
                    else {
                        string description = (match.Groups["description"].Success ? match.Groups["description"].Value : "");
                        string departure = (match.Groups["departure"].Success ? match.Groups["departure"].Value : null);
                        CharacterExtraInfo character = new CharacterExtraInfo(player, description, departure);
                        this.model.characters[match.Groups["name"].Value] = character;
                    }
                    if (unreferencedChars.Contains(match.Groups["name"].Value)) {
                        unreferencedChars.Remove(match.Groups["name"].Value);
                    }
                    gotEmpty = false;
                    continue;
                }
                // out of valid header lines; mark end of header section if necessary then bail
                if (gotHeader) {
                    // got at least one valid character; mark end of header section at start of invalid line with right-affinity
                    this.model.header_section_end = this.log_box.Document.CreateAnchor(lineSpec.Offset);
                    this.model.header_section_end.MovementType = AnchorMovementType.AfterInsertion;
                }
                break;
            }
            // trim unreferenced characters
            foreach (string name in unreferencedChars) {
                if (!this.model.characters.ContainsKey(name)) {
                    continue;
                }
                this.model.characters.Remove(name);
            }
            // update party display
            this.update_party_list();
        }

        private class SessionEventRecord {
            public SessionRecord session;
            public List<LogEvent> events;

            public SessionEventRecord(SessionRecord session) {
                this.session = session;
                this.events = new List<LogEvent>();
            }
        }

        private void do_timeline_update() {
            List<SessionEventRecord> sessions = new List<SessionEventRecord>();
            SessionEventRecord curSession = null;
            SessionRecord updateSession = null;
            int startLine = 0;
            if (this.model.timeline_section_start is not null) {
                // TextAnchor line indices are 1-based
                startLine = this.model.timeline_section_start.Line - 1;
            }
            else if (this.model.header_section_end is not null) {
                // TextAnchor line indices are 1-based
                startLine = this.model.header_section_end.Line - 1;
            }
            int endLine = this.log_box.Document.LineCount;
            if (this.timeline_update_session < this.model.sessions.Count) {
                updateSession = this.model.sessions[this.timeline_update_session];
                if (!this.timeline_update_session_dirty) {
                    // update session is clean; end before start of update session and we'll need to pick up the new lines to append after
                    endLine = updateSession.start.Line - 1;
                }
                else if (this.timeline_update_session > 0) {
                    // update session is dirty; end at the start of the next session
                    endLine = this.model.sessions[this.timeline_update_session - 1].start.Line - 1;
                }
            }
            int nextLine, prevLineOffset = -1;
            string curLine = null;
            int curLineStart = -1, curLineEnd = -1;
            bool needStart = (this.model.timeline_section_start is null);
            bool needTail = (updateSession is not null) && (!this.timeline_update_session_dirty), firstTail = false, inTail = false;
            for (int i = startLine; i < endLine; i = nextLine) {
                nextLine = i + 1;
                if (firstTail) {
                    // first line of end of clean update session; process outstanding line if we have one
                    if (curLine is not null) {
                        TextAnchor lineStart = this.log_box.Document.CreateAnchor(curLineStart);
                        // make sure line start has right-affinity so it stays at start of line as it currently exists
                        lineStart.MovementType = AnchorMovementType.AfterInsertion;
                        TextAnchor lineEnd = this.log_box.Document.CreateAnchor(curLineEnd);
                        // make sure line end has left-affinity so it stays at end of line as it currently exists
                        lineStart.MovementType = AnchorMovementType.BeforeInsertion;
                        curSession.events.AddRange(LogEvent.parse(new LogReference(curSession.session, curLine, lineStart, lineEnd)));
                        curLine = null;
                    }
                    curSession = new SessionEventRecord(updateSession);
                    inTail = true;
                    firstTail = false;
                }
                DocumentLine lineSpec = this.log_box.Document.Lines[i];
                if ((needTail) && (nextLine >= endLine)) {
                    // adjust nextLine and endLine to pick up new lines at end of otherwise-clean update session
                    nextLine = updateSession.end.Line - 1;
                    if (this.timeline_update_session > 0) {
                        // TextAnchor line indices are 1-based
                        endLine = this.model.sessions[this.timeline_update_session - 1].start.Line - 1;
                    }
                    else {
                        endLine = this.log_box.Document.LineCount;
                    }
                    needTail = false;
                    firstTail = true;
                }
                string line = this.log_box.Document.GetText(lineSpec.Offset, lineSpec.Length);
                Match match = COMMENT_EXP.Match(line);
                if (match.Success) {
                    // trim out end-of-line comment
                    line = match.Groups["content"].Value;
                    if (line == "") {
                        // comment was whole line; skip line
                        continue;
                    }
                }
                if (curLine is not null) {
                    // we had an entry line before; extend it if this is a continuation...
                    match = SESSION_ENTRY_CONTINUATION_EXP.Match(line);
                    if (match.Success) {
                        curLine += " " + match.Groups["continuation"];
                        curLineEnd = lineSpec.EndOffset;
                        continue;
                    }
                    // ...or process it if this isn't a continuation
                    TextAnchor lineStart = this.log_box.Document.CreateAnchor(curLineStart);
                    // make sure line start has right-affinity so it stays at start of line as it currently exists
                    lineStart.MovementType = AnchorMovementType.AfterInsertion;
                    TextAnchor lineEnd = this.log_box.Document.CreateAnchor(curLineEnd);
                    // make sure line end has left-affinity so it stays at end of line as it currently exists
                    lineStart.MovementType = AnchorMovementType.BeforeInsertion;
                    curSession.events.AddRange(LogEvent.parse(new LogReference(curSession.session, curLine, lineStart, lineEnd)));
                    curLine = null;
                }
                match = SESSION_EXP.Match(line);
                if (match.Success) {
                    if (needStart) {
                        // we haven't yet noted the start of the timeline session; do so now if possible
                        if (prevLineOffset >= 0) {
                            // record start of timeline section as just before previous (blank) line with left-affinity
                            this.model.timeline_section_start = this.log_box.Document.CreateAnchor(prevLineOffset);
                            this.model.timeline_section_start.MovementType = AnchorMovementType.BeforeInsertion;
                        }
                        needStart = false;
                    }
                    if ((inTail) && (curSession.session == updateSession)) {
                        // session(s) added earlier than update session; we'll have to roll back as if update session were dirty
                        this.timeline_update_session_dirty = true;
                        sessions.Add(curSession);
                    }
                    TextAnchor sessionStart = this.log_box.Document.CreateAnchor(lineSpec.Offset);
                    // make sure session start has right-affinity so it stays at start of session header
                    sessionStart.MovementType = AnchorMovementType.AfterInsertion;
                    TextAnchor sessionEnd = this.log_box.Document.CreateAnchor(lineSpec.EndOffset + lineSpec.DelimiterLength);
                    // make sure session end has left-affinity so it stays exactly after newline at end of session content
                    sessionEnd.MovementType = AnchorMovementType.BeforeInsertion;
                    SessionRecord session = new SessionRecord(
                        int.Parse(match.Groups["id"].Value),
                        match.Groups["relative"].Success,
                        match.Groups["date"].Value,
                        sessionStart,
                        sessionEnd
                    );
                    curSession = new SessionEventRecord(session);
                    sessions.Add(curSession);
                    continue;
                }
                prevLineOffset = lineSpec.Offset;
                if ((curSession is null) || (line == "")) {
                    continue;
                }
                match = SESSION_ENTRY_EXP.Match(line);
                if (match.Success) {
                    curLine = match.Groups["line"].Value;
                    curLineStart = lineSpec.Offset;
                    curLineEnd = lineSpec.EndOffset;
                    // update session end, making sure it has left-affinity so it stays exactly after newline at end of session content
                    curSession.session.end = this.log_box.Document.CreateAnchor(lineSpec.EndOffset + lineSpec.DelimiterLength);
                    curSession.session.end.MovementType = AnchorMovementType.BeforeInsertion;
                    continue;
                }
                match = TIMESTAMP_EXP.Match(line);
                if (match.Success) {
                    string timestamp = match.Groups["timestamp"].Value;
                    if (this.model.validate_timestamp(timestamp)) {
                        // update session in-game end timestamp
                        TextAnchor lineStart = this.log_box.Document.CreateAnchor(lineSpec.Offset);
                        // make sure line start has right-affinity so it stays at start of line as it currently exists
                        lineStart.MovementType = AnchorMovementType.AfterInsertion;
                        TextAnchor lineEnd = this.log_box.Document.CreateAnchor(lineSpec.EndOffset);
                        // make sure line end has left-affinity so it stays at end of line as it currently exists
                        lineStart.MovementType = AnchorMovementType.BeforeInsertion;
                        curSession.events.Add(new TimestampEvent(new LogReference(curSession.session, line, lineStart, lineEnd), timestamp));
                        continue;
                    }
                }
            }
            // process outstanding line if we have one
            if (curLine is not null) {
                TextAnchor lineStart = this.log_box.Document.CreateAnchor(curLineStart);
                // make sure line start has right-affinity so it stays at start of line as it currently exists
                lineStart.MovementType = AnchorMovementType.AfterInsertion;
                TextAnchor lineEnd = this.log_box.Document.CreateAnchor(curLineEnd);
                // make sure line end has left-affinity so it stays at end of line as it currently exists
                lineStart.MovementType = AnchorMovementType.BeforeInsertion;
                curSession.events.AddRange(LogEvent.parse(new LogReference(curSession.session, curLine, lineStart, lineEnd)));
            }
            // rollback if necessary
            int rollbackIdx = this.timeline_update_session;
            if (!this.timeline_update_session_dirty) {
                rollbackIdx += 1;
            }
            if ((rollbackIdx >= 0) && (rollbackIdx < this.model.sessions.Count)) {
                this.model.campaign_state = this.model.sessions[rollbackIdx].start_state.copy();
                this.model.sessions.RemoveRange(rollbackIdx, this.model.sessions.Count - rollbackIdx);
            }
            // apply new sessions
            sessions.Reverse();
            foreach (SessionEventRecord session in sessions) {
                session.session.start_state = this.model.campaign_state.copy();
                this.model.sessions.Add(session.session);
                foreach (LogEvent evt in session.events) {
                    evt.apply(this.model.campaign_state);
                }
            }
            // update display
            this.update_topic_list();
            this.update_party_list();
            //TODO: update inventory list
            this.update_events_list();
            this.update_tasks_list();
            this.update_session_list();
            if (this.model.sessions.Count > 0) {
                SessionRecord lastSession = this.model.sessions[this.model.sessions.Count - 1];
                this.timestamp_box.Content = this.model.timestamp ?? "";
                this.session_box.Content = (lastSession.is_relative ? "+" : "") + (lastSession.index.ToString());
            }
            else {
                this.timestamp_box.Content = "";
                this.session_box.Content = "";
            }
        }

        private void log_update_timer_tick(object sender, EventArgs e) {
            if (!this.log_box.TextArea.IsFocused) {
                // HACK: ListView selection changes steal focus and change handler can't give it back, so periodically grab it here
                this.log_box.TextArea.Focus();
            }
            DateTime now = DateTime.Now;
            if ((this.header_update_due is not null) && (now >= this.header_update_due)) {
                this.header_update_due = null;
                this.do_header_update();
            }
            if ((this.timeline_update_due is not null) && (now >= this.timeline_update_due)) {
                this.timeline_update_due = null;
                this.do_timeline_update();
            }
        }

        private void scroll_to(int offset) {
            this.log_box.TextArea.Caret.Offset = offset;
            this.log_box.TextArea.Caret.BringCaretToView();
        }

        private void scroll_to(TextAnchor loc) {
            this.scroll_to(loc.Offset);
        }

        //TODO: menu handlers

        private static void fix_listview_column_widths(ListView listView) {
            GridView grid = listView.View as GridView;
            if (grid is null) {
                return;
            }
            foreach (GridViewColumn col in grid.Columns) {
                col.Width = col.ActualWidth;
                col.Width = double.NaN;
            }
        }

        private void topics_tab_selected(object sender, RoutedEventArgs e) {
            if (sender is not TabItem) {
                return;
            }
            this.details_tab.Content = this.topic_info;
            this.update_topic_references();
        }

        private void party_tab_selected(object sender, RoutedEventArgs e) {
            if (sender is not TabItem) {
                return;
            }
            this.details_tab.Content = this.character_info;
            this.update_party_references();
        }

        //TODO: inventory_tab_selected

        private void events_tab_selected(object sender, RoutedEventArgs e) {
            if (sender is not TabItem) {
                return;
            }
            this.details_tab.Content = this.event_info;
            this.update_event_references();
        }

        private void tasks_tab_selected(object sender, RoutedEventArgs e) {
            if (sender is not TabItem) {
                return;
            }
            this.details_tab.Content = this.task_info;
            this.update_task_references();
        }

        private void session_tab_selected(object sender, RoutedEventArgs e) {
            if (sender is not TabItem) {
                return;
            }
            this.details_tab.Content = this.session_info;
            this.update_session_references();
        }

        public void select_entry(StateReference.ReferenceType type, string name) {
            switch (type) {
            case StateReference.ReferenceType.Topic:
                this.topics_tab.IsSelected = true;
                this.topic_list.SelectedValue = name;
                break;
            case StateReference.ReferenceType.Character:
                this.party_tab.IsSelected = true;
                this.party_list.SelectedValue = name;
                break;
            //TODO: inventory reference
            case StateReference.ReferenceType.Event:
                this.events_tab.IsSelected = true;
                this.events_list.SelectedValue = name;
                break;
            case StateReference.ReferenceType.Task:
                this.tasks_tab.IsSelected = true;
                this.tasks_list.SelectedValue = name;
                break;
            }
        }

        private static void set_optional_info_field(Label fieldLbl, Label fieldBox, string content) {
            if (string.IsNullOrEmpty(content)) {
                fieldLbl.Visibility = Visibility.Collapsed;
                fieldBox.Visibility = Visibility.Collapsed;
            }
            else {
                fieldLbl.Visibility = Visibility.Visible;
                fieldBox.Content = content;
                fieldBox.Visibility = Visibility.Visible;
            }
        }

        private void topic_list_changed(object sender, SelectionChangedEventArgs e) {
            string selected = this.topic_list.SelectedValue as string;
            if (selected is null) {
                this.topic_info.topic_box.Content = "";
                this.topic_info.relation_list.Items.Clear();
            }
            if (this.model.campaign_state.topics.ContainsKey(selected)) {
                TopicState topic = this.model.campaign_state.topics[selected];
                this.topic_info.topic_box.Content = selected;
                List<StateReference> relations = new List<StateReference>(topic.relations);
                relations.Sort();
                this.topic_info.relation_list.ItemsSource = relations;
            }
            fix_listview_column_widths(this.topic_info.relation_list);
            this.update_topic_references();
        }

        private void toggle_party_departed(object sender, RoutedEventArgs e) {
            this.update_party_list();
        }

        private void party_list_changed(object sender, SelectionChangedEventArgs e) {
            string selected = this.party_list.SelectedValue as string;
            if (selected is null) {
                this.character_info.name_box.Content = "";
                this.character_info.player_lbl.Visibility = Visibility.Collapsed;
                this.character_info.player_box.Visibility = Visibility.Collapsed;
                this.character_info.level_lbl.Visibility = Visibility.Collapsed;
                this.character_info.level_box.Visibility = Visibility.Collapsed;
                this.character_info.xp_lbl.Visibility = Visibility.Collapsed;
                this.character_info.xp_box.Visibility = Visibility.Collapsed;
                this.character_info.description_lbl.Visibility = Visibility.Collapsed;
                this.character_info.description_box.Visibility = Visibility.Collapsed;
                this.character_info.departure_lbl.Visibility = Visibility.Collapsed;
                this.character_info.departure_box.Visibility = Visibility.Collapsed;
                return;
            }
            if (this.model.campaign_state.characters.ContainsKey(selected)) {
                CharacterState character = this.model.campaign_state.characters[selected];
                this.character_info.name_box.Content = selected;
                if (character.level > 0) {
                    this.character_info.level_lbl.Visibility = Visibility.Visible;
                    this.character_info.level_box.Content = character.level.ToString();
                    this.character_info.level_box.Visibility = Visibility.Visible;
                    this.character_info.xp_lbl.Visibility = Visibility.Visible;
                    this.character_info.xp_box.Content = character.xp.ToString();
                    this.character_info.xp_box.Visibility = Visibility.Visible;
                }
                else {
                    this.character_info.level_lbl.Visibility = Visibility.Collapsed;
                    this.character_info.level_box.Visibility = Visibility.Collapsed;
                    this.character_info.xp_lbl.Visibility = Visibility.Collapsed;
                    this.character_info.xp_box.Visibility = Visibility.Collapsed;
                }
            }
            if (this.model.characters.ContainsKey(selected)) {
                CharacterExtraInfo character = this.model.characters[selected];
                this.character_info.name_box.Content = selected;
                set_optional_info_field(this.character_info.player_lbl, this.character_info.player_box, character.player);
                set_optional_info_field(this.character_info.description_lbl, this.character_info.description_box, character.description);
                set_optional_info_field(this.character_info.departure_lbl, this.character_info.departure_box, character.departure);
            }
            this.update_party_references();
        }

        //TODO: inventory handlers

        private void toggle_events_past(object sender, RoutedEventArgs e) {
            this.update_events_list();
        }

        private void events_list_changed(object sender, SelectionChangedEventArgs e) {
            string selected = this.events_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.events.ContainsKey(selected))) {
                EventState evt = this.model.campaign_state.events[selected];
                this.event_info.name_box.Content = selected;
                string delta = null;
                if (this.model.calendar is not null) {
                    this.event_info.timestamp_lbl.Visibility = Visibility.Visible;
                    this.event_info.timestamp_box.Content = this.model.calendar.format_timestamp(evt.timestamp);
                    this.event_info.timestamp_box.Visibility = Visibility.Visible;
                    delta = this.model.calendar.subtract_timestamp(evt.timestamp, this.model.campaign_state.timestamp);
                }
                else {
                    this.event_info.timestamp_lbl.Visibility = Visibility.Collapsed;
                    this.event_info.timestamp_box.Visibility = Visibility.Collapsed;
                }
                if (delta is not null) {
                    this.event_info.delta_lbl.Visibility = Visibility.Visible;
                    this.event_info.delta_box.Content = delta;
                    this.event_info.delta_box.Visibility = Visibility.Visible;
                }
                else {
                    this.event_info.delta_lbl.Visibility = Visibility.Collapsed;
                    this.event_info.delta_box.Visibility = Visibility.Collapsed;
                }
                this.event_info.desc_box.Text = evt.description ?? "";
            }
            else {
                this.event_info.name_box.Content = "";
                this.event_info.timestamp_lbl.Visibility = Visibility.Collapsed;
                this.event_info.timestamp_box.Visibility = Visibility.Collapsed;
                this.event_info.delta_lbl.Visibility = Visibility.Collapsed;
                this.event_info.delta_box.Visibility = Visibility.Collapsed;
                this.event_info.desc_box.Text = "";
            }
            this.update_event_references();
        }

        private void tasks_list_changed(object sender, SelectionChangedEventArgs e) {
            string selected = this.tasks_list.SelectedValue as string;
            if ((selected is not null) && (this.model.campaign_state.tasks.ContainsKey(selected))) {
                TaskState task = this.model.campaign_state.tasks[selected];
                this.task_info.name_box.Content = selected;
                string due = null, delta = null;
                if (this.model.calendar is not null) {
                    this.task_info.timestamp_box.Content = this.model.calendar.format_timestamp(task.timestamp);
                    if (task.due is not null) {
                        due = this.model.calendar.format_timestamp(task.due);
                        delta = this.model.calendar.subtract_timestamp(task.due, this.model.campaign_state.timestamp);
                    }
                }
                else {
                    this.task_info.timestamp_box.Content = task.timestamp.timestamp;
                    if (task.due is not null) {
                        due = task.due.timestamp;
                    }
                }
                if (due is not null) {
                    this.task_info.due_lbl.Visibility = Visibility.Visible;
                    this.task_info.due_box.Content = due;
                    this.task_info.due_box.Visibility = Visibility.Visible;
                }
                else {
                    this.task_info.due_lbl.Visibility = Visibility.Collapsed;
                    this.task_info.due_box.Visibility = Visibility.Collapsed;
                }
                if (delta is not null) {
                    this.task_info.delta_lbl.Visibility = Visibility.Visible;
                    this.task_info.delta_box.Content = delta;
                    this.task_info.delta_box.Visibility = Visibility.Visible;
                }
                else {
                    this.task_info.delta_lbl.Visibility = Visibility.Collapsed;
                    this.task_info.delta_box.Visibility = Visibility.Collapsed;
                }
                this.task_info.desc_box.Text = task.description ?? "";
            }
            else {
                this.task_info.name_box.Content = "";
                this.task_info.timestamp_box.Content = "";
                this.task_info.due_lbl.Visibility = Visibility.Collapsed;
                this.task_info.due_box.Visibility = Visibility.Collapsed;
                this.task_info.delta_lbl.Visibility = Visibility.Collapsed;
                this.task_info.delta_box.Visibility = Visibility.Collapsed;
                this.task_info.desc_box.Text = "";
            }
            this.update_task_references();
        }

        private void session_list_changed(object sender, SelectionChangedEventArgs e) {
            SessionRecord selected = this.session_list.SelectedValue as SessionRecord;
            if (selected is not null) {
                this.session_info.index_box.Content = (selected.is_relative ? "+" : "") + (selected.index.ToString());
                this.session_info.date_box.Content = selected.date;
            }
            else {
                this.session_info.index_box.Content = "";
                this.session_info.date_box.Content = "";
            }
            this.update_session_references();
        }

        private void reference_list_changed(object sender, SelectionChangedEventArgs e) {
            LogReference selected = this.reference_list.SelectedValue as LogReference;
            if (selected is null) {
                return;
            }
            this.reference_list.UnselectAll();
            this.scroll_to(selected.start);
        }

        private void suppress_command(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = false;
            e.Handled = true;
        }

        private void add_new_session(object sender, RoutedEventArgs e) {
            // handle any outstanding updates first so we have the most up-to-date info
            if (this.timeline_update_due is not null) {
                this.timeline_update_due = null;
                this.do_timeline_update();
            }
            int insertPoint = this.log_box.Document.TextLength, sessionId = 1;
            string relative = "";
            string inGameTimestamp = this.model.timestamp;
            if (this.model.sessions.Count > 0) {
                SessionRecord lastSession = this.model.sessions[this.model.sessions.Count - 1];
                insertPoint = lastSession.start.Offset;
                sessionId = lastSession.index + 1;
                if (lastSession.is_relative) {
                    relative = "+";
                }
            }
            string date = DateTime.Today.ToString("yyyy-MM-dd");
            string sessionHeader = $"s{relative}{sessionId} ({date}):{Environment.NewLine}";
            int newlines = 1;
            if (inGameTimestamp is not null) {
                sessionHeader += $"{inGameTimestamp} (continued):{Environment.NewLine}";
            }
            if (this.model.sessions.Count > 0) {
                sessionHeader += Environment.NewLine;
                newlines += 1;
            }
            int headerLen = sessionHeader.Length - (newlines * Environment.NewLine.Length);
            this.log_box.Document.Insert(insertPoint, sessionHeader);
            this.scroll_to(insertPoint + headerLen);
            this.log_box.TextArea.Focus();
        }

        private void on_log_text_entering(object sender, TextCompositionEventArgs e) {
            if (this.completion_window is not null) {
                if ((this.completion_first) && (e.Text == "{")) {
                    foreach (CompletionEntry ent in this.completion_window.CompletionList.CompletionData) {
                        ent.Text = "{" + ent.Text + "}";
                    }
                    this.completion_first = false;
                    return;
                }
                this.completion_first = false;
            }
            if ((e.Text == ":") && (this.log_box.TextArea.Caret.Location.Column == 1)) {
                // colon will be inserted at start of line; insert timestamp before it
                this.log_box.Document.Insert(this.log_box.CaretOffset, DateTime.Now.ToString("HHmm"));
                return;
            }
            //TODO: if inserting space at start of line in header or timeline section, insert one less than necessary for line continuation
        }

        private void populate_completions(IEnumerable<string> items) {
            List<CompletionEntry> completions = new List<CompletionEntry>();
            foreach (string item in items) {
                completions.Add(new CompletionEntry(item));
                string[] splits = TOKEN_SPLIT_EXP.Split(item);
                if (splits.Length > 1) {
                    foreach (string token in splits) {
                        completions.Add(new CompletionEntry(token, CompletionEntry.PRIORITY_LOW));
                    }
                }
            }
            completions.Sort((x, y) => x.Text.CompareTo(y.Text));
            foreach (CompletionEntry completion in completions) {
                this.completion_window.CompletionList.CompletionData.Add(completion);
            }
        }

        private void populate_topic_completions() {
            this.populate_completions(this.model.campaign_state.topics.Keys);
        }

        private void populate_character_completions() {
            List<string> characters = new List<string>(this.model.campaign_state.characters.Keys);
            foreach (string name in this.model.characters.Keys) {
                if (!this.model.campaign_state.characters.ContainsKey(name)) {
                    characters.Add(name);
                }
            }
            this.populate_completions(characters);
        }

        private void on_log_text_entered(object sender, TextCompositionEventArgs e) {
            if (((e.Text == "#") || (e.Text == "@")) && (this.completion_window is null)) {
                string nextChar = "";
                if (this.log_box.CaretOffset < this.log_box.Document.TextLength) {
                    nextChar = this.log_box.Document.GetText(this.log_box.CaretOffset, 1);
                }
                if (TOKEN_END_EXP.IsMatch(nextChar)) {
                    this.completion_window = new CompletionWindow(this.log_box.TextArea);
                    this.completion_window.Closed += (sdr, evt) => this.completion_window = null;
                    if (e.Text == "#") {
                        this.populate_topic_completions();
                    }
                    else {
                        this.populate_character_completions();
                    }
                    if (this.completion_window.CompletionList.CompletionData.Count > 0) {
                        this.completion_first = true;
                        this.completion_window.Show();
                    }
                    else {
                        this.completion_window = null;
                    }
                }
            }
        }

        private void on_log_change(object sender, DocumentChangeEventArgs e) {
            bool needPlayersUpdate = false;
            bool needTimelineUpdate = false;
            int updateSession = this.model.sessions.Count;
            bool updateSessionDirty = false;

            if (this.model.header_section_end is not null) {
                if ((this.model.header_section_end.IsDeleted) || (this.model.header_section_end.Offset <= 0)) {
                    this.model.header_section_end = null;
                }
            }
            if (this.model.timeline_section_start is not null) {
                if ((this.model.timeline_section_start.IsDeleted) || (this.model.timeline_section_start.Offset <= 0)) {
                    this.model.timeline_section_start = null;
                }
            }

            if ((this.model.header_section_end is null) || (e.Offset < this.model.header_section_end.Offset)) {
                // got a change to header section; we'll need to mark header section for update
                needPlayersUpdate = true;
            }
            int timelineSectionOffset = 0;
            if ((this.model.timeline_section_start is not null) && (this.model.timeline_section_start.Offset > timelineSectionOffset)) {
                timelineSectionOffset = this.model.timeline_section_start.Offset;
            }
            if (e.Offset + e.InsertionLength + e.RemovalLength >= timelineSectionOffset) {
                // got a change to timeline section; determine which session is the earliest in need of update
                needTimelineUpdate = true;

                if (this.timeline_update_due is not null) {
                    // if there's already a pending update we can skip anything it already has covered
                    updateSession = this.timeline_update_session;
                }

                int firstCheckSession = updateSession;
                if ((this.timeline_update_session_dirty) || (firstCheckSession >= this.model.sessions.Count)) {
                    // pending update session is already marked dirty; move on to check next session
                    firstCheckSession -= 1;
                }
                for (int i = firstCheckSession; i >= 0; i--) {
                    SessionRecord session = this.model.sessions[i];
                    if (e.Offset + e.RemovalLength < session.start.Offset) {
                        break;
                    }
                    updateSession = i;
                    // we'll determine if this session is dirty below; for now, mark session as unmodified
                    updateSessionDirty = false;
                }
                if (updateSession < this.model.sessions.Count) {
                    // this change affects a valid session in need of update; determine if this change dirties it (rather than appends to it)
                    if (e.Offset < this.model.sessions[updateSession].end.Offset) {
                        updateSessionDirty = true;
                    }
                }
            }

            DateTime updateTime = DateTime.Now + TYPING_DELAY;
            if (needPlayersUpdate) {
                // signal that we'll need an update of the header section once typing has stopped for a bit
                this.header_update_due = updateTime;
            }
            if (needTimelineUpdate) {
                // signal that we'll need an update of the timeline section once typing has stopped for a bit
                if (this.timeline_update_due is null) {
                    this.timeline_update_session = this.model.sessions.Count;
                    this.timeline_update_session_dirty = false;
                }
                this.timeline_update_due = updateTime;
                if (updateSession < this.timeline_update_session) {
                    // the earliest session to update is earlier than before; replace entirely with this one
                    this.timeline_update_session = updateSession;
                    this.timeline_update_session_dirty = updateSessionDirty;
                }
                else if (updateSession == this.timeline_update_session) {
                    // the earliest session to update is same as before; mark dirty if necessary
                    this.timeline_update_session_dirty |= updateSessionDirty;
                }
            }
        }
    }
}
