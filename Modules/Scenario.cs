using Newtonsoft.Json; 
using Newtonsoft.Json.Linq; 

namespace ScenarioBot.Modules {
    class Scenario {
        public string name;
        public string description;
        public ScenarioStage[]? stages;

        public Scenario(JObject j) {
            this.name = j["info"]["name"].ToString();
            this.description = j["info"]["description"].ToString();
        }
    }

    class ScenarioStage {
        public string text;
        public Tuple<string, string>[] obs;
        public Question[] questions;
    }

    class Question {
        string q;
        string a;
    }
}