namespace QweenIris
{
    public class PromptContext
    {
        public bool promptSet { get; private set; }
        public bool historySet { get; private set; }
        public bool characterInstructionsSet { get; private set; }

        public bool userSet { get; private set; }

        private string history;
        private string shortHistory;
        private string characterId;
        private string normalInstructions;
        private string codeInstructions;
        private string newsSearchInstructions;
        private string prompt;
        private string user;

        public void SetHistory(string history, string shortHistory)
        {
            this.history = history;
            this.shortHistory = shortHistory;
            historySet = true;
        }

        public void SetInstructions(string characterId, string normal, string code, string newsSearch)
        {
            this.characterId = characterId;
            this.normalInstructions = normal;
            this.codeInstructions = code;
            this.newsSearchInstructions = newsSearch;
            characterInstructionsSet = true;
        }

        public void SetUser(string user)
        {
            this.user = user;
            userSet = true;
        }

        public void SetPrompt(string prompt)
        {
            this.prompt = prompt;
            promptSet = !string.IsNullOrEmpty(prompt);
        }

        public string User
        {
            get
            {
                if (!userSet)
                    throw new InvalidOperationException("History has not been set or is invalid.");
                return user;
            }
        }

        public string History
        {
            get
            {
                if (!historySet)
                    throw new InvalidOperationException("History has not been set or is invalid.");
                return history;
            }
        }

        public string ShortHistory
        {
            get
            {
                if (!historySet)
                    throw new InvalidOperationException("ShortHistory has not been set or is invalid.");
                return shortHistory;
            }
        }

        public string CharacterId
        {
            get
            {
                if (!characterInstructionsSet)
                    throw new InvalidOperationException("CharacterId has not been set or is invalid.");
                return characterId;
            }
        }
        public string NormalInstructions
        {
            get
            {
                if (!characterInstructionsSet)
                    throw new InvalidOperationException("Normal has not been set or is invalid.");
                return normalInstructions;
            }
        }
        public string CodeInstructions
        {
            get
            {
                if (!characterInstructionsSet)
                    throw new InvalidOperationException("Code has not been set or is invalid.");
                return codeInstructions;
            }
        }
        public string NewsSearchInstructions
        {
            get
            {
                if (!characterInstructionsSet)
                    throw new InvalidOperationException("NewsSearch has not been set or is invalid.");
                return newsSearchInstructions;
            }
        }
        public string Prompt
        {
            get
            {
                if (!promptSet)
                    throw new InvalidOperationException("Prompt has not been set or is invalid.");
                return prompt;
            }
        }
        // Validation method (unchanged)
        public void validate()
        {
            if (!promptSet && !historySet &&!characterInstructionsSet)
            {
                Console.WriteLine("Validation failed.");
            }
            else
            {
                Console.WriteLine("All fields are valid.");
            }
        }
    }
}
