using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace CampaignLogger {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private static readonly TimeSpan TYPING_POLL_INTERVAL = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan TYPING_DELAY = TimeSpan.FromSeconds(5);

        private static readonly Regex CHARACTER_EXP = new Regex(
            @"^((?<player>[^:]+):)?\s+([(](?<departure>[^)]+)[)]\s+)?(?<name>[^(]+)(\s+[(](?<desc>[^)]+)[)])?$",
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
        private static readonly Regex SESSION_IN_GAME_TIMESTAMP_EXP = new Regex(
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

        private LogModel model;
        private DateTime? players_update_due = null;
        private DateTime? timeline_update_due = null;
        private int timeline_update_session;
        private bool timeline_update_session_dirty;
        private DispatcherTimer dispatcher_timer;
        private List<CharacterEntry> party_display;

        public MainWindow() {
            this.model = new LogModel();
            this.dispatcher_timer = new DispatcherTimer();
            this.dispatcher_timer.Interval = TYPING_POLL_INTERVAL;
            this.dispatcher_timer.Tick += this.log_update_timer_tick;
            this.party_display = new List<CharacterEntry>();
            InitializeComponent();
            this.party_list.ItemsSource = this.party_display;
            this.dispatcher_timer.Start();
        }

        private void update_party_list() {
            HashSet<string> seenCharacters = new HashSet<string>();
            this.party_display.Clear();
            foreach (string name in this.model.campaign_state.characters.Keys) {
                CharacterState character = this.model.campaign_state.characters[name];
                this.party_display.Add(new CharacterEntry(name, character.level, character.xp));
                seenCharacters.Add(name);
            }
            if (this.party_departed_box.IsChecked == true) {
                // add characters listed in the players section but not currently in the party
                foreach (string name in this.model.characters.Keys) {
                    if (seenCharacters.Contains(name)) {
                        continue;
                    }
                    this.party_display.Add(new CharacterEntry(name));
                }
            }
            this.party_display.Sort((x, y) => x.name.CompareTo(y.name));
            this.party_list.Items.Refresh();
            fix_listview_column_widths(this.party_list);
            //TODO: selection
        }

        private void do_players_update() {
            HashSet<string> unreferencedChars = new HashSet<string>(this.model.characters.Keys);
            TextPointer startPointer = this.log_box.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);
            TextPointer endPointer = this.model.players_section_end;
            if (endPointer is null) {
                endPointer = this.log_box.Document.ContentEnd;
            }
            string player = null;
            bool gotEmpty = false;
            TextPointer lineEnd;
            for (TextPointer lineStart = startPointer; lineStart.GetOffsetToPosition(endPointer) > 0; lineStart = lineEnd) {
                lineEnd = lineStart.GetLineStartPosition(1);
                if (lineEnd is null) {
                    lineEnd = endPointer;
                }
                string line = new TextRange(lineStart, lineEnd).Text.TrimEnd();
                if ((line == "") && (!gotEmpty)) {
                    // got a valid empty line; note that and move on
                    gotEmpty = true;
                    continue;
                }
                Match match = CHARACTER_EXP.Match(line);
                if ((match.Success) && ((player is not null) || (match.Groups["player"].Success))) {
                    // got a valid character line; handle it
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
                // out of valid character lines; mark end of players section if necessary then bail
                if (player is not null) {
                    // got at least one valid character; mark end of players section at start of invalid line with right-affinity
                    this.model.players_section_end = lineStart.GetInsertionPosition(LogicalDirection.Forward);
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

        private void do_timeline_update() {
            List<SessionRecord> sessions = new List<SessionRecord>();
            SessionRecord curSession = null, updateSession = null;
            int updateOffset = 0;
            TextPointer startPointer = this.model.timeline_section_start;
            if (startPointer is null) {
                startPointer = this.model.players_section_end;
            }
            if (startPointer is null) {
                startPointer = this.log_box.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);
            }
            TextPointer endPointer = this.log_box.Document.ContentEnd;
            if (this.timeline_update_session < this.model.sessions.Count) {
                updateSession = this.model.sessions[this.timeline_update_session];
                updateOffset = updateSession.events.Count;
                if (!this.timeline_update_session_dirty) {
                    // end before start of clean session's lines; we'll need to pick up the new lines to append after
                    endPointer = updateSession.start;
                }
                else if (this.timeline_update_session > 0) {
                    // just roll through end of dirty session
                    endPointer = this.model.sessions[this.timeline_update_session - 1].start;
                }
            }
            TextPointer prevLine = null, lineEnd = null, nextLineStart;
            bool needStart = (this.model.timeline_section_start is null);
            string curLine = null;
            TextPointer curLineStart = null;
            bool needTail = (updateSession is not null) && (!this.timeline_update_session_dirty), firstTail = false, inTail = false;
            for (TextPointer lineStart = startPointer; lineStart.GetOffsetToPosition(endPointer) > 0; lineStart = nextLineStart) {
                if (firstTail) {
                    // first line of end of clean update session; process outstanding line if we have one
                    if (curLine is not null) {
                        curSession.events.AddRange(LogEvent.parse(new LogReference(curLine, curLineStart, lineEnd)));
                        curLine = null;
                    }
                    curSession = updateSession;
                    inTail = true;
                    //TODO: handle continuation of final line of clean updateSession
                    firstTail = false;
                }
                lineEnd = lineStart.GetLineStartPosition(1);
                if (lineEnd is null) {
                    lineEnd = endPointer;
                }
                nextLineStart = lineEnd;
                if ((needTail) && (nextLineStart.GetOffsetToPosition(endPointer) <= 0)) {
                    // adjust start/end pointers to pick up new lines at end of update session
                    nextLineStart = updateSession.end;
                    if (this.timeline_update_session > 0) {
                        endPointer = this.model.sessions[this.timeline_update_session - 1].start;
                    }
                    else {
                        endPointer = this.log_box.Document.ContentEnd;
                    }
                    needTail = false;
                    firstTail = true;
                }
                string line = new TextRange(lineStart, lineEnd).Text.TrimEnd();
                Match match;
                if (curLine is not null) {
                    // we had an entry line before; extend it if this is a continuation...
                    match = SESSION_ENTRY_CONTINUATION_EXP.Match(line);
                    if (match.Success) {
                        curLine += " " + match.Groups["continuation"];
                        continue;
                    }
                    // ...or process it if this isn't a continuation
                    curSession.events.AddRange(LogEvent.parse(new LogReference(curLine, curLineStart, lineEnd)));
                    curLine = null;
                }
                match = SESSION_EXP.Match(line);
                if (match.Success) {
                    if (needStart) {
                        if (prevLine is not null) {
                            // record start of timeline section as just before previous (blank) line with left-affinity
                            this.model.timeline_section_start = prevLine.GetNextInsertionPosition(LogicalDirection.Backward);
                        }
                        needStart = false;
                    }
                    if ((inTail) && (curSession == updateSession)) {
                        // session(s) added earlier than update session; we'll have to roll back as if update session were dirty
                        this.timeline_update_session_dirty = true;
                        sessions.Add(updateSession);
                    }
                    curSession = new SessionRecord(
                        int.Parse(match.Groups["id"].Value),
                        match.Groups["relative"].Success,
                        match.Groups["date"].Value,
                        // make sure session start has right-affinity so it stays at start of session header
                        lineStart.GetInsertionPosition(LogicalDirection.Forward),
                        // make sure session end has left-affinity so it stays exactly after newline at end of session content
                        lineEnd.GetInsertionPosition(LogicalDirection.Backward)
                    );
                    sessions.Add(curSession);
                    continue;
                }
                prevLine = lineStart;
                if ((curSession is null) || (line == "")) {
                    continue;
                }
                match = SESSION_ENTRY_EXP.Match(line);
                if (match.Success) {
                    curLine = match.Groups["line"].Value;
                    curLineStart = lineStart;
                    // update session end, making sure it has left-affinity so it stays exactly after newline at end of session content
                    curSession.end = lineEnd.GetInsertionPosition(LogicalDirection.Backward);
                    continue;
                }
                match = SESSION_IN_GAME_TIMESTAMP_EXP.Match(line);
                if (match.Success) {
                    // update session in-game end timestamp
                    curSession.in_game_end = match.Groups["timestamp"].Value;
                }
            }
            // process outstanding line if we have one
            if (curLine is not null) {
                curSession.events.AddRange(LogEvent.parse(new LogReference(curLine, curLineStart, lineEnd)));
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
            if ((!this.timeline_update_session_dirty) && (updateSession is not null)) {
                updateSession.apply(this.model, this.model.campaign_state, updateOffset);
            }
            sessions.Reverse();
            this.model.sessions.AddRange(sessions);
            foreach (SessionRecord session in sessions) {
                session.start_state = this.model.campaign_state.copy();
                session.apply(this.model, this.model.campaign_state);
            }
            // update display
            //TODO: update topics list
            this.update_party_list();
            //TODO: update other left panel stuff
            if (this.model.sessions.Count > 0) {
                SessionRecord lastSession = this.model.sessions[this.model.sessions.Count - 1];
                this.timestamp_box.Content = lastSession.in_game_end;
                this.session_box.Content = (lastSession.is_relative ? "+" : "") + (lastSession.index.ToString());
            }
            else {
                this.timestamp_box.Content = "";
                this.session_box.Content = "";
            }
        }

        private void log_update_timer_tick(object sender, EventArgs e) {
            DateTime now = DateTime.Now;
            if ((this.players_update_due is not null) && (now >= this.players_update_due)) {
                this.players_update_due = null;
                this.do_players_update();
            }
            if ((this.timeline_update_due is not null) && (now >= this.timeline_update_due)) {
                this.timeline_update_due = null;
                this.do_timeline_update();
            }
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

        //TODO: topics handlers

        private void toggle_party_departed(object sender, RoutedEventArgs e) {
            this.update_party_list();
        }

        //TODO: party_list selection handler

        //TODO: inventory handlers, events handlers, tasks handlers, sessions handlers

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
            TextPointer insertPoint = this.log_box.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Forward);
            int sessionId = 1;
            string relative = "";
            string inGameTimestamp = null;
            if (this.model.sessions.Count > 0) {
                SessionRecord lastSession = this.model.sessions[this.model.sessions.Count - 1];
                insertPoint = lastSession.start;
                sessionId = lastSession.index + 1;
                if (lastSession.is_relative) {
                    relative = "+";
                }
                inGameTimestamp = lastSession.in_game_end;
            }
            string date = DateTime.Today.ToString("yyyy-MM-dd");
            insertPoint.InsertTextInRun($"s{relative}{sessionId} ({date}):");
            insertPoint.InsertLineBreak();
            if (inGameTimestamp is not null) {
                insertPoint.InsertTextInRun($"{inGameTimestamp} (continued):");
                insertPoint.InsertLineBreak();
            }
            if (this.model.sessions.Count > 0) {
                insertPoint.InsertLineBreak();
            }
        }

        private void on_log_key_down(object sender, KeyEventArgs e) {
            //TODO: maybe only check this in timeline section
            if ((e.Key == Key.OemSemicolon) && (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))) {
                // check if colon will be inserted at start of line; insert timestamp if so
                if (this.log_box.CaretPosition.IsAtLineStartPosition) {
                    this.log_box.CaretPosition.InsertTextInRun(DateTime.Now.ToString("HHmm"));
                }
            }
            //TODO: if inserting space at start of line in players or timeline section, insert one less than necessary for line continuation
            //TODO: start/progress autocomplete if necessary: @ (D2+Shift) for characters, # (D3+Shift) for topics
        }

        private void on_log_change(object sender, TextChangedEventArgs e) {
            if (e.Changes.Count <= 0) {
                return;
            }

            //TODO: handle any outstanding autocomplete
            //TODO: set Document.PageWidth to max line length + some buffer

            int playersSectionLength = int.MaxValue;
            if (this.model.players_section_end is not null) {
                playersSectionLength = this.log_box.Document.ContentStart.GetOffsetToPosition(this.model.players_section_end);
            }
            int timelineSectionOffset = 0;
            if (this.model.timeline_section_start is not null) {
                timelineSectionOffset = this.log_box.Document.ContentStart.GetOffsetToPosition(this.model.timeline_section_start);
            }
            bool needPlayersUpdate = false;
            bool needTimelineUpdate = false;
            int updateSession = this.model.sessions.Count;
            bool updateSessionDirty = false;
            foreach (TextChange change in e.Changes) {
                if (change.Offset < playersSectionLength) {
                    // got a change to players section; we'll need to mark players section for update
                    needPlayersUpdate = true;
                }
                if (change.Offset + change.AddedLength < timelineSectionOffset) {
                    continue;
                }
                // got a change to timeline section; determine which session is the earliest in need of update
                needTimelineUpdate = true;
                for (int i = updateSession - 1; i >= 0; i--) {
                    SessionRecord session = this.model.sessions[i];
                    int sessionOffset = this.log_box.Document.ContentStart.GetOffsetToPosition(session.start);
                    if (change.Offset + change.AddedLength > sessionOffset) {
                        updateSession = i;
                        // we'll determine if this session is dirty below; for now, mark newly-minimal session as unmodified
                        updateSessionDirty = false;
                    }
                }
                if (updateSession < this.model.sessions.Count) {
                    // this change affects a valid session in need of update; determine if this change dirties it (rather than appends to it)
                    int endOffset = this.log_box.Document.ContentStart.GetOffsetToPosition(this.model.sessions[updateSession].end);
                    if (change.Offset < endOffset) {
                        updateSessionDirty = true;
                    }
                }
            }
            DateTime updateTime = DateTime.Now + TYPING_DELAY;
            if (needPlayersUpdate) {
                // signal that we'll need an update of the players section once typing has stopped for a bit
                this.players_update_due = updateTime;
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
