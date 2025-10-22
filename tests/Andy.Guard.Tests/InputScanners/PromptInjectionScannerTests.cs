
namespace Andy.Guard.Tests.InputScanners;

// TODO Critical Testing to do using with Test Containers
// Assume there is an image andy-inference-models-inference-service:latest running locally in my Docker environment
// and that the InferenceApiClient can connect to it at http://localhost:5158 (check out /Users/Thomas_1/Sites/andy-inference-models/README.md for list of available endpoints)
// Use xUnit and typical best practices for C# testing (fixtures) based on TestContainers
// Use FluentAssertions for assertions
// End goal. Spin the up dependencies in containers and then call the scanner with inputs that should trigger prompt injection detection
// Focus on a single test for now that demonstrates the approach. Don't worry about covering all edge cases yet. It is ok if the container is not up and you cant verify. I will do this later
public class PromptInjectionScannerTests
{

}

