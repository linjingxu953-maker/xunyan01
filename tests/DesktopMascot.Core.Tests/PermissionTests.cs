using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Security;

namespace DesktopMascot.Core.Tests;

public class PermissionManagerTests
{
    [Fact]
    public async Task RequestPermission_L0_ShouldAutoAllow()
    {
        var manager = new PermissionManager();
        var request = new PermissionRequest
        {
            TaskId = "test-1",
            Level = PermissionLevel.L0_Chat,
            Title = "普通聊天",
            Target = "chat"
        };

        var response = await manager.RequestPermissionAsync(request);

        Assert.Equal(PermissionDecision.Allow, response.Decision);
    }

    [Fact]
    public async Task RequestPermission_L6_ShouldDeny()
    {
        var manager = new PermissionManager();
        var request = new PermissionRequest
        {
            TaskId = "test-2",
            Level = PermissionLevel.L6_Forbidden,
            Title = "危险操作",
            Target = "dangerous"
        };

        var response = await manager.RequestPermissionAsync(request);

        Assert.Equal(PermissionDecision.Deny, response.Decision);
    }

    [Fact]
    public async Task HasPermission_WithPermanentGrant_ShouldReturnTrue()
    {
        var manager = new PermissionManager();
        await manager.GrantPermanentPermissionAsync("test_op", PermissionLevel.L3_FileRead);

        var hasPermission = await manager.HasPermissionAsync("test_op", PermissionLevel.L3_FileRead);

        Assert.True(hasPermission);
    }

    [Fact]
    public async Task RevokePermission_ShouldRemoveAccess()
    {
        var manager = new PermissionManager();
        await manager.GrantPermanentPermissionAsync("test_op", PermissionLevel.L4_FileWrite);
        await manager.RevokePermissionAsync("test_op");

        var hasPermission = await manager.HasPermissionAsync("test_op", PermissionLevel.L4_FileWrite);

        Assert.False(hasPermission);
    }

    [Fact]
    public async Task LogAudit_ShouldRecordEntry()
    {
        var manager = new PermissionManager();
        var entry = new AuditLogEntry
        {
            TaskId = "test-3",
            Operation = "test",
            Decision = PermissionDecision.Allow
        };

        await manager.LogAuditAsync(entry);
        var logs = await manager.GetAuditLogsAsync();

        Assert.Single(logs);
    }

    [Fact]
    public async Task RequestPermission_WithPermanentAuth_ShouldAutoApprove()
    {
        var manager = new PermissionManager();
        await manager.GrantPermanentPermissionAsync("write_file", PermissionLevel.L4_FileWrite);

        var request = new PermissionRequest
        {
            TaskId = "test-4",
            Level = PermissionLevel.L4_FileWrite,
            Title = "写入文件",
            Target = "write_file"
        };

        var response = await manager.RequestPermissionAsync(request);

        Assert.Equal(PermissionDecision.AllowAlways, response.Decision);
    }
}

public class CommandRiskAssessorTests
{
    private readonly CommandRiskAssessor _assessor = new();

    [Theory]
    [InlineData("del test.txt")]
    [InlineData("rm -rf /")]
    [InlineData("format c:")]
    [InlineData("shutdown /s")]
    public void Assess_BlockedCommands_ShouldBeBlocked(string command)
    {
        var result = _assessor.Assess(command);

        Assert.True(result.IsBlocked);
        Assert.Equal(PermissionLevel.L6_Forbidden, result.RiskLevel);
    }

    [Theory]
    [InlineData("npm install")]
    [InlineData("dir")]
    [InlineData("ls")]
    public void Assess_NormalCommands_ShouldRequireConfirmation(string command)
    {
        var result = _assessor.Assess(command);

        Assert.False(result.IsBlocked);
        Assert.Equal(PermissionLevel.L5_CommandExec, result.RiskLevel);
    }

    [Fact]
    public void Assess_GitForcePush_ShouldHaveWarning()
    {
        var result = _assessor.Assess("git push --force");

        Assert.Contains(result.Warnings, w => w.Contains("强制推送"));
    }

    [Fact]
    public void AssessFileWrite_SystemPath_ShouldBeBlocked()
    {
        var level = _assessor.AssessFileWrite(@"C:\Windows\System32\test.dll");

        Assert.Equal(PermissionLevel.L6_Forbidden, level);
    }

    [Fact]
    public void AssessFileWrite_NormalPath_ShouldRequireConfirm()
    {
        var level = _assessor.AssessFileWrite(@"C:\Users\test\Documents\file.txt");

        Assert.Equal(PermissionLevel.L4_FileWrite, level);
    }

    [Fact]
    public void AssessFileRead_SensitiveFile_ShouldRequireConfirm()
    {
        var level = _assessor.AssessFileRead(@"C:\Users\test\.env");

        Assert.Equal(PermissionLevel.L4_FileWrite, level);
    }
}
