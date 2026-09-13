using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using ValeriusAI.App;
using Xunit;
namespace ValeriusAI.Tests;
public sealed class AccessibilityTests
{
    [Fact] public void AccessibleChoiceChangesSelectionWithoutThrowing()
    {
        var combo=new ChoiceBox{ItemsSource=new[]{"Sistema","Claro","Escuro"},SelectedItem="Sistema"};
        var peer=Assert.IsAssignableFrom<IValueProvider>(ControlAutomationPeer.CreatePeerForElement(combo));
        peer.SetValue("Claro");Assert.Equal("Claro",combo.SelectedItem);
        peer.SetValue("Valor inexistente");Assert.Equal("Claro",combo.SelectedItem);
    }
}
