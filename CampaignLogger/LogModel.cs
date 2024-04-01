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
        //TODO: topics
        public Dictionary<string, CharacterExtraInfo> characters;
        //TODO: inventory, events, tasks
        public Calendar calendar;
        public List<SessionRecord> sessions;
        public CampaignState campaign_state;

        public string timestamp => this.campaign_state?.timestamp;

        public LogModel() {
            //TODO: topics
            this.characters = new Dictionary<string, CharacterExtraInfo>();
            //TODO: inventory, events, tasks
            this.sessions = new List<SessionRecord>();
            this.campaign_state = new CampaignState(this);
        }

        public string match_timestamp(string s) {
            if (this.calendar is null) {
                this.calendar = Calendar.get_calendar_from_timestamp(s);
                if (this.calendar is null) {
                    return null;
                }
            }
            return this.calendar.match_timestamp(s);
        }
    }
}
