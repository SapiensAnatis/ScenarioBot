using Newtonsoft.Json; 
using Newtonsoft.Json.Linq;

using System.Linq;

namespace ScenarioBot {
    // Root class
    public class Scenario
    {
        public Info info { get; set; }
        public List<Stage> stages { get; set; }

        // Default constructor
        public Scenario() {}

        // Copy constructor
        public Scenario(Scenario s) {
            this.info = s.info;
            this.stages = s.stages;
        }
    }

    public class Info
    {
        public string name { get; set; }
        public string description { get; set; }
        public string id { get; set; }
    }

    public class Question
    {
        public string question { get; set; }
        public string answer { get; set; }
    }

    public class Stage
    {
        public string text { get; set; }
        public List<string>? obs { get; set; }
        public List<Question>? questions { get; set; }
    }

    public class Session
    {
        public ulong user_id { get; set; }
        public string scenario_id { get; set; }
        public int stage { get; set; }

        // Not set by json deserialization
        [JsonIgnore]
        public Scenario scenario_obj { get; set; }

        // Get underlying Stage object corresponding to current state of the Scenario.
        // Helper method that changes 
        //     string text = s.scenario_obj.stages[s.stage].text;
        // to
        //     string text = s.GetStage().text;
        public Stage GetStage() {
            return this.scenario_obj.stages[stage];
        }
    }
}