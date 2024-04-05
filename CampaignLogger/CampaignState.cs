using System;
using System.Collections.Generic;

namespace CampaignLogger {
    public class StateReference : IEquatable<StateReference>, IComparable<StateReference> {
        public enum ReferenceType {
            Topic,
            Character,
        }

        public ReferenceType type;
        public string name;

        public StateReference self => this;
        public string type_str => this.type.ToString();
        public string name_str => this.name;

        public StateReference(ReferenceType type, string name) {
            this.type = type;
            this.name = name;
        }

        public override string ToString() {
            return this.name + "; " + this.type_str;
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        public bool Equals(StateReference other) {
            return (other.type == this.type) && (other.name == this.name);
        }

        public override bool Equals(object other) {
            if (other is StateReference otherReference) {
                return this.Equals(otherReference);
            }
            return false;
        }

        public int CompareTo(StateReference other) {
            int cmp = this.name.CompareTo(other.name);
            if (cmp != 0) {
                return cmp;
            }
            return this.type_str.CompareTo(other.type.ToString());
        }
    }

    public class TopicState {
        public HashSet<StateReference> relations;
        public List<LogReference> references;

        public TopicState(IEnumerable<StateReference> relations) {
            this.relations = new HashSet<StateReference>(relations);
            this.references = new List<LogReference>();
        }

        public TopicState copy() {
            TopicState result = new TopicState(this.relations);
            result.references.AddRange(this.references);
            return result;
        }
    }

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
        public Dictionary<string, TopicState> topics;
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
            this.topics = new Dictionary<string, TopicState>();
            this.characters = new Dictionary<string, CharacterState>();
        }

        protected CampaignState(CampaignState prev) : this(prev.model) {
            foreach (string topic in prev.topics.Keys) {
                this.topics[topic] = prev.topics[topic].copy();
            }
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
