#pragma warning disable OPENAI001
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;

var apiKey = "sk-proj-XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
var client = new OpenAIClient(apiKey);
var fileClient = client.GetOpenAIFileClient();
var assistantClient = client.GetAssistantClient();

Console.Write("Document file path: ");
var filePath = Console.ReadLine();

var resumeFile = fileClient.UploadFile(filePath, FileUploadPurpose.Assistants);
Console.WriteLine("File uploaded successfully with ID: " + resumeFile.Value.Id);

var assistantOptions = new AssistantCreationOptions
{
    Name = "Resume Extractor",
    Instructions = "You are an assistant that extracts resume data from a PDF file.",
    ToolResources = new() { FileSearch = new() }
};
assistantOptions.Tools.Add(new FileSearchToolDefinition());
assistantOptions.ToolResources.FileSearch.NewVectorStores.Add(new VectorStoreCreationHelper([resumeFile.Value.Id]));

var assistant = assistantClient.CreateAssistant("gpt-4o", assistantOptions);
Console.WriteLine("Assistant created with ID: " + assistant.Value.Id);
Console.WriteLine();

while (true)
{
    try
    {
        Console.Write("Your question: ");
        var prompt = Console.ReadLine();

        if (prompt == "quit") break;

        var threadOptions = new ThreadCreationOptions
        {
            InitialMessages = { prompt }
        };

        var threadRun = assistantClient.CreateThreadAndRun(assistant.Value.Id, threadOptions);

        while (!threadRun.Value.Status.IsTerminal)
        {
            threadRun = assistantClient.GetRun(threadRun.Value.ThreadId, threadRun.Value.Id);
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }

        var messages = assistantClient.GetMessages(threadRun.Value.ThreadId);

        foreach (var message in messages)
        {
            if (message.Role != MessageRole.Assistant) continue;

            foreach (var content in message.Content)
            {
                var text = content.Text;

                foreach (var annotation in content.TextAnnotations)
                {
                    text = text.Replace(annotation.TextToReplace, "");
                }

                Console.WriteLine(text);
            }
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERROR: " + ex.Message);
    }
}

assistantClient.DeleteAssistant(assistant.Value.Id);
fileClient.DeleteFile(resumeFile.Value.Id);