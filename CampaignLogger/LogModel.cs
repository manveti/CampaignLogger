using System.Collections.Generic;
using System.Windows.Documents;

namespace CampaignLogger {
    //TODO: topics

    public class CharacterRecord {
        public string player;
        public string description;
        public string departure;
        //TODO: ?level?, ?xp?, timeline references

        public CharacterRecord(string player, string description, string departure = null) {
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
        //TODO: rollback state?

        public SessionRecord(int index, bool isRelative, string date, TextPointer start, TextPointer end) {
            this.index = index;
            this.is_relative = isRelative;
            this.date = date;
            this.start = start;
            this.end = end;
        }
    }

    public class LogModel {
        public TextPointer players_section_end;
        public TextPointer timeline_section_start;
        //TODO: topics
        public Dictionary<string, CharacterRecord> characters;
        //TODO: inventory, events, tasks
        public List<SessionRecord> sessions;

        public LogModel() {
            //TODO: topics
            this.characters = new Dictionary<string, CharacterRecord>();
            //TODO: inventory, events, tasks
            this.sessions = new List<SessionRecord>();
        }
    }
}
