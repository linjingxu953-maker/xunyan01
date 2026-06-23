using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tests;

public sealed class AgentPersonalityTests
{
    [Fact]
    public void DefaultPersonality_UsesDefaultCharacterNameAndProjectDescription()
    {
        var personality = new AgentPersonality();

        Assert.Equal("枫林渔人", personality.Name);
        Assert.Equal("寻研01桌面助手", personality.Description);
        Assert.Contains("你是 枫林渔人，寻研01桌面助手。", personality.BuildSystemPrompt("无"));
    }
}
