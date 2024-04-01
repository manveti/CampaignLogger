using System.Collections.Generic;

namespace CampaignLogger {
    public class CharacterState {
        public int level;
        public int xp;
        public List<LogReference> references;

        public CharacterState(int level, int xp) {
            this.level = level;
            this.xp = xp;
            this.references = new List<LogReference>();
        }

        public CharacterState copy() {
            CharacterState result = new CharacterState(this.level, this.xp);
            result.references.AddRange(this.references);
            return result;
        }
    }

    public class CampaignState {
        public LogModel model;
        //TODO: topics
        public Dictionary<string, CharacterState> characters;
        //TODO: inventory, events, tasks
        protected string _timestamp;

        public string timestamp {
            get {
                if (this._timestamp is not null) {
                    return this._timestamp;
                }
                if (this.model.calendar is not null) {
                    return this.model.calendar.default_timestamp();
                }
                return null;
            }
            set => this._timestamp = value;
        }

        public CampaignState(LogModel model) {
            this.model = model;
            this.characters = new Dictionary<string, CharacterState>();
        }

        protected CampaignState(CampaignState prev) : this(prev.model) {
            //TODO: topics
            foreach (string charName in prev.characters.Keys) {
                this.characters[charName] = prev.characters[charName].copy();
            }
            //TODO: inventory, events, tasks
            this._timestamp = prev._timestamp;
        }

        public CampaignState copy() {
            return new CampaignState(this);
        }
    }
}
