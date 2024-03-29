using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel;

namespace My.MyChatFunction
{
    public class MyChatFunction
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;
        private readonly IChatCompletion _chat;
        private readonly ChatHistory _chatHistory;

        public MyChatFunction(ILoggerFactory loggerFactory, IKernel kernel, ChatHistory chatHistory, IChatCompletion chat)
        {
            _logger = loggerFactory.CreateLogger<MyChatFunction>();
            _kernel = kernel;
            _chat = chat;
            _chatHistory = chatHistory;
        }

        [Function("MyChatFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            // Add the user's chat message to the history.
            // _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);

            string message = await SearchMemoriesAsync(_kernel, await req.ReadAsStringAsync() ?? string.Empty);
            _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, message);

            // Send the chat history to the AI and receive a reply.
            string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

            // Add the AI's reply to the chat history for next time.
            _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);

            // Send the AI's response back to the caller.
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(reply);
            return response;
        }

        private async Task<string> SearchMemoriesAsync(IKernel kernel, string query)
        {
            StringBuilder result = new StringBuilder();
            result.Append("The below is relevant information.\n[START INFO]");
            
            // Search for memories that are similar to the user's input.
            // const string memoryCollectionName = "ms10k";
            // const string memoryCollectionName = "alsc";
            string memoryCollectionName = System.Environment.GetEnvironmentVariable("memoryCollectionName") ?? "";
            Console.WriteLine("Memory Collection Name : " + memoryCollectionName);
            // const string memoryCollectionName = "mohawkemails";
            // const string memoryCollectionName = "novant";
            IAsyncEnumerable<MemoryQueryResult> queryResults = 
                kernel.Memory.SearchAsync(memoryCollectionName, query, limit: 3, minRelevanceScore: 0.77);

            // For each memory found, try to get previous and next memories.
            await foreach (MemoryQueryResult r in queryResults)
            {
                int id = int.Parse(r.Metadata.Id);
                MemoryQueryResult? rb2 = await kernel.Memory.GetAsync(memoryCollectionName, (id - 2).ToString());
                MemoryQueryResult? rb = await kernel.Memory.GetAsync(memoryCollectionName, (id - 1).ToString());
                MemoryQueryResult? ra = await kernel.Memory.GetAsync(memoryCollectionName, (id + 1).ToString());
                MemoryQueryResult? ra2 = await kernel.Memory.GetAsync(memoryCollectionName, (id + 2).ToString());

                if (rb2 != null) result.Append("\n " + rb2.Metadata.Id + ": " + rb2.Metadata.Description + "\n");
                if (rb != null) result.Append("\n " + rb.Metadata.Description + "\n");
                if (r != null) result.Append("\n " + r.Metadata.Description + "\n");
                if (ra != null) result.Append("\n " + ra.Metadata.Description + "\n");
                if (ra2 != null) result.Append("\n " + ra2.Metadata.Id + ": " + ra2.Metadata.Description + "\n");
            }

            result.Append("\n[END INFO]");
            result.Append($"\n{query}");

            return result.ToString();
        }
        
                
    }
}
