using System.Collections.Generic;
using System.Windows.Documents;

using ICSharpCode.AvalonEdit.Document;

namespace CampaignLogger {
    public class LogReference {
        public SessionRecord session;
        public string line;
        public TextAnchor start;
        public TextAnchor end;

        public LogReference(SessionRecord session, string line, TextAnchor start, TextAnchor end) {
            this.session = session;
            this.line = line;
            this.start = start;
            this.end = end;
        }
    }

    //TODO: topics

    public class CharacterExtraInfo {
        public string player;
        public string description;
        public string departure;

        public CharacterExtraInfo(string player, string description, string departure = null) {
            this.player = player;
            this.description = description;
            this.departure = departure;
        }
    }

    //TODO: inventory, events, tasks

    public class SessionRecord {
        public int index;
        public bool is_relative;
        public string date;
        public TextAnchor start;
        public TextAnchor end;
        public CampaignState start_state;

        public SessionRecord(int index, bool isRelative, string date, TextAnchor start, TextAnchor end) {
            this.index = index;
            this.is_relative = isRelative;
            this.date = date;
            this.start = start;
            this.end = end;
            this.start_state = null;
        }
    }

    public class LogModel {
        public TextAnchor players_section_end;
        public TextAnchor timeline_section_start;
        public Dictionary<string, CharacterExtraInfo> characters;
        public Calendar calendar;
        public List<SessionRecord> sessions;
        public CampaignState campaign_state;

        public string timestamp => this.campaign_state?.timestamp;

        public LogModel() {
            this.characters = new Dictionary<string, CharacterExtraInfo>();
            this.sessions = new List<SessionRecord>();
            this.campaign_state = new CampaignState(this);
        }

        public bool validate_timestamp(string s) {
            if (s is null) {
                return false;
            }
            if (this.calendar is null) {
                this.calendar = Calendar.get_calendar_from_timestamp(s);
                if (this.calendar is null) {
                    return false;
                }
            }
            return this.calendar.validate_timestamp(s);
        }

        public bool validate_interval(string s) {
            if (s is null) {
                return false;
            }
            if (this.calendar is null) {
                this.calendar = Calendar.get_get_calendar_from_interval(s);
                if (this.calendar is null) {
                    return false;
                }
            }
            return this.calendar.validate_interval(s);
        }

        public bool validate_event_timestamp(EventTimestamp ts) {
            if ((ts is null) || ((ts.timestamp is null) && (ts.delta is null))) {
                return false;
            }
            bool result = true;
            if (ts.timestamp is not null) {
                result = result && this.validate_timestamp(ts.timestamp);
            }
            if (ts.delta is not null) {
                result = result && this.validate_interval(ts.delta);
            }
            return result;
        }
    }
}
