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
        //TODO: topics
        public Dictionary<string, CharacterState> characters;
        //TODO: inventory, events, tasks

        public CampaignState() {
            this.characters = new Dictionary<string, CharacterState>();
        }

        protected CampaignState(CampaignState prev) : this() {
            //TODO: topics
            foreach (string charName in prev.characters.Keys) {
                this.characters[charName] = prev.characters[charName].copy();
            }
            //TODO: inventory, events, tasks
        }

        public CampaignState copy() {
            return new CampaignState(this);
        }
    }
}
