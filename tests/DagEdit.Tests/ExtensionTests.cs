namespace DagEdit.Tests;

using Xunit;

public class ExtensionTests
{
    [Fact]
    public void TryWriteErrorsToFile_CreatesMissingDirectoryAndWritesMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "DagEdit.Tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "nested", "ErrorsLog.txt");

        try
        {
            var success = Extension.TryWriteErrorsToFile("test message", filePath);

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
        var fallbackMessages = new System.Collections.Generic.List<string>();
        var invalidPath = Path.Combine(Path.GetTempPath(), "DagEdit.Tests", "\0invalid");

        var success = Extension.TryWriteErrorsToFile(
            "test message",
            invalidPath,
            fallbackMessages.Add);

        Assert.False(success);
        Assert.Single(fallbackMessages);
        Assert.Contains("Error log write failed", fallbackMessages[0]);
    }
}
