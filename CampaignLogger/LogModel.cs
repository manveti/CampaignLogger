using System.Collections.Generic;
using System.Windows.Documents;

namespace CampaignLogger {
    public class LogReference {
        public string line;
        public TextPointer start;
        public TextPointer end;

        public LogReference(string line, TextPointer start, TextPointer end) {
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
        public TextPointer start;
        public TextPointer end;
        public string in_game_end;
        public CampaignState start_state;
        public List<LogEvent> events;

        public SessionRecord(int index, bool isRelative, string date, TextPointer start, TextPointer end) {
            this.index = index;
            this.is_relative = isRelative;
            this.date = date;
            this.start = start;
            this.end = end;
            this.start_state = null;
            this.events = new List<LogEvent>();
        }

        public void apply(LogModel model, CampaignState state, int offset = 0) {
            for (int i = offset; i < this.events.Count; i++) {
                this.events[i].apply(model, state);
            }
        }
    }

    public class LogModel {
        public TextPointer players_section_end;
        public TextPointer timeline_section_start;
        //TODO: topics
        public Dictionary<string, CharacterExtraInfo> characters;
        //TODO: inventory, events, tasks
        public List<SessionRecord> sessions;
        public CampaignState campaign_state;

        public LogModel() {
            //TODO: topics
            this.characters = new Dictionary<string, CharacterExtraInfo>();
            //TODO: inventory, events, tasks
            this.sessions = new List<SessionRecord>();
            this.campaign_state = new CampaignState();
        }
    }
}
