// See https://aka.ms/new-console-template for more information
public interface IDiscordBot
{
    public Task StartAsync();
    Task StopAsync();
}