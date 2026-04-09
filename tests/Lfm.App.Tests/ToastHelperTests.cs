using Microsoft.FluentUI.AspNetCore.Components;
using Moq;
using Lfm.App.Services;
using Xunit;

namespace Lfm.App.Tests;

public class ToastHelperTests
{
    [Fact]
    public void ShowSuccess_Delegates_To_ToastService()
    {
        var mock = new Mock<IToastService>();
        var helper = new ToastHelper(mock.Object);

        helper.ShowSuccess("Run saved");

        mock.Verify(s => s.ShowSuccess("Run saved", null, null, null), Times.Once);
    }

    [Fact]
    public void ShowError_Delegates_To_ToastService()
    {
        var mock = new Mock<IToastService>();
        var helper = new ToastHelper(mock.Object);

        helper.ShowError("Something failed");

        mock.Verify(s => s.ShowError("Something failed", null, null, null), Times.Once);
    }
}
