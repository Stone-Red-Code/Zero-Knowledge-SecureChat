namespace ZeroKnowledgeSecureChat.Api;

internal static class TaskExtensions
{
    public static async Task WaitUntil(Func<bool> condition, TimeSpan timeout, int frequency = 100)
    {
        Task waitTask = Task.Run(async () =>
        {
            while (!condition())
            {
                await Task.Delay(frequency);
            }
        });

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
        {
            throw new TimeoutException();
        }
    }

    public static async Task WaitUntil(Func<bool> condition, int timeout, int frequency = 100)
    {
        await WaitUntil(condition, TimeSpan.FromMilliseconds(timeout), frequency);
    }
}