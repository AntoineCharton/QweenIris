using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace QweenIris
{
    internal class DiscordBot
    {
        private readonly DiscordSocketClient client;
        private readonly string token;
        private readonly ulong chatChannelID;

        public DiscordBot(Action<string, string, string, string, string, string> action, ulong instructionsID, ulong codeInstructionsID, ulong newsInstructionID, ulong channelID)
        {
            chatChannelID = channelID;
            this.client = new DiscordSocketClient();
            var config = Config.Load();
            token = config["Discord:Token"];
            this.client.MessageReceived += async message =>
            {
                // Avoid blocking the gateway task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (message.Channel.Id != channelID || message.Author.IsBot)
                            return;

                        Console.WriteLine("New message received:");
                        var instructionsChannel = client.GetChannel(instructionsID) as SocketTextChannel;
                        var instruction = "";
                        var instructionChannelMessages = await instructionsChannel.GetMessagesAsync(limit: 1).FlattenAsync();
                        foreach (var instructionMessage in instructionChannelMessages)
                        {
                            instruction += " " + instructionMessage.Content;
                        }

                        var codeInstructionsChannel = client.GetChannel(codeInstructionsID) as SocketTextChannel;
                        var codeInstruction = "";
                        var codeInstructionChannelMessages = await codeInstructionsChannel.GetMessagesAsync(limit: 1).FlattenAsync();
                        foreach (var instructionMessage in codeInstructionChannelMessages)
                        {
                            codeInstruction += " " + instructionMessage.Content;
                        }

                        var newsInstructionChannel = client.GetChannel(newsInstructionID) as SocketTextChannel;
                        var newsInstruction = "";
                        var newsInstructionChannelMessages = await newsInstructionChannel.GetMessagesAsync(limit: 1).FlattenAsync();
                        foreach (var instructionMessage in newsInstructionChannelMessages)
                        {
                            newsInstruction += " " + instructionMessage.Content;
                        }


                        var channel = client.GetChannel(channelID) as SocketTextChannel;
                        var messages = await channel.GetMessagesAsync(limit: 1).FlattenAsync();
                        var history = await channel.GetMessagesAsync(limit: 20).FlattenAsync();
                        var parsedHistory = "";

                        var currentPastMessageLooked = 0;
                        foreach (var pastMessage in history)
                        {
                            if (currentPastMessageLooked > 0 && !pastMessage.Author.IsBot)
                            {
                                parsedHistory += $"from: {pastMessage.Author} Message:{pastMessage.Content}\n";
                            }
                            currentPastMessageLooked++;
                        }

                        var messageLooked = 0;
                        foreach (var message in messages)
                        {
                            action.Invoke(instruction, codeInstruction, newsInstruction, parsedHistory, message.Content, message.Author.Username);
                        }


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in MessageHandler: {ex.Message}");
                    }
                });
            };
        }

        public async Task ReplyAsync(string response)
        {
            var message = client.GetChannel(chatChannelID) as SocketTextChannel;
            if (response.Length > 2000)
            {
                string[] splitBlocks = response.Split(new string[] { "--BLOCK--" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in splitBlocks)
                {
                    string [] lines = { block };
                    if (block.Length > 2000)
                    {
                        lines = block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    foreach (var line in lines)
                    {
                        await ReplyAsync(line);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(response) && response.Length > 0)
            {
                response = response.Replace("--BLOCK--", "");
                await message.SendMessageAsync(response);
            }

        }

        public async Task TriggerTyping()
        {
            Console.WriteLine("Trigger typing");
            var channel = client.GetChannel(chatChannelID) as SocketTextChannel;
            await channel.TriggerTypingAsync();
        }
        public async Task StartAsync()
        {

            this.client.Log += LogFuncAsync;

            await this.client.LoginAsync(TokenType.Bot, token);
            await this.client.StartAsync();
            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.Write(message.ToString());
        }

    }
}
