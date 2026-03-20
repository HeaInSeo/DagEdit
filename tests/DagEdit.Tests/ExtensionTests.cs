namespace DagEdit.Tests;

using Xunit;

public class ExtensionTests
{
    [Fact]
    public void TryWriteErrorsToFile_CreatesMissingDirectoryAndWritesMessage()
    {
        string root = Path.Combine(Path.GetTempPath(), "DagEdit.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(root, "nested", "ErrorsLog.txt");

        try
        {
            bool success = Extension.TryWriteErrorsToFile("test message", filePath);

            Assert.True(success);
            Assert.True(File.Exists(filePath));
            Assert.Contains("test message", File.ReadAllText(filePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void TryWriteErrorsToFile_WhenWriteFails_InvokesFallbackHint()
    {
        var fallbackMessages = new List<string>();
        string invalidPath = Path.Combine(Path.GetTempPath(), "DagEdit.Tests", "\0invalid");

        bool success = Extension.TryWriteErrorsToFile(
            "test message",
            invalidPath,
            fallbackMessages.Add);

        Assert.False(success);
        Assert.Single(fallbackMessages);
        Assert.Contains("Error log write failed", fallbackMessages[0]);
    }
}
