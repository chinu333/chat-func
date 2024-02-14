using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults();

hostBuilder.ConfigureAppConfiguration((context, config) =>
{
    config.AddUserSecrets<Program>();
});

hostBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IKernel>(sp =>
    {
        // Retrieve the OpenAI API key from the configuration.
        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        // string openAiApiKey = ""; //configuration["OPENAI_APIKEY"];

        string memoryCollectionName = configuration["memoryCollectionName"] ?? "";
        string csURL = configuration["csURL"] ?? "";
        string csKey = configuration["csKey"] ?? "";
        string openAIURL = configuration["openAIURL"] ?? "";
        string openAIDeploymentName = configuration["openAIDeploymentName"] ?? "";
        string openAIKey = configuration["openAIKey"] ?? "";

        // Console.WriteLine("Memory Collection Name : " + memoryCollectionName);
        // Console.WriteLine("Cognitive Search URL : " + csURL);
        // Console.WriteLine("Cognitive Search KEY: " + csKey);
        // Console.WriteLine("OPEN API URL: " + openAIURL);
        // Console.WriteLine("OPEN API Deployment Name: " + openAIDeploymentName);
        // Console.WriteLine("OPEN API KEY: " + openAIKey);

        QdrantMemoryStore memoryStore = new QdrantMemoryStore(
            host: "http://localhost",
            port: 6333,
            vectorSize: 1536,
            logger: sp.GetRequiredService<ILogger<QdrantMemoryStore>>());

        AzureCognitiveSearchMemory csmemory = new AzureCognitiveSearchMemory(csURL, csKey);

        IKernel kernel = new KernelBuilder()
            .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
            .Configure(config => config.AddAzureChatCompletionService(
                deploymentName: openAIDeploymentName,
                endpoint: openAIURL,
                apiKey: openAIKey))
            // .Configure(c => c.AddAzureTextEmbeddingGenerationService(
            //     deploymentName: "text-embedding-ada-002",
            //     endpoint: "https://openaimtcdemoinstance.openai.azure.com/",
            //     apiKey: openAiApiKey))
            // .WithMemoryStorage(memoryStore)
            .WithMemory(csmemory)
            .Build();

        return kernel;
    });

    // Provide a chat completion service client to our function.
    services.AddSingleton<IChatCompletion>(sp =>
        sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

    // Provide a persistant in-memory chat history store with the 
    // initial ChatGPT system message.
    const string instructions = "You are a helpful friendly assistant.";
    services.AddSingleton<ChatHistory>(sp =>
        sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
});

hostBuilder.Build().Run();
