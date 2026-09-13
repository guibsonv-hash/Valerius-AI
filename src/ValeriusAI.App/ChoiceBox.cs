using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using ValeriusAI.Core;
namespace ValeriusAI.App;

// O peer padrão do Avalonia 11 anuncia AXValue editável, mas lança
// NotSupportedException em SetValue. Este peer implementa a seleção real.
public sealed class ChoiceBox : ComboBox
{
    protected override Type StyleKeyOverride=>typeof(ComboBox);
    protected override AutomationPeer OnCreateAutomationPeer()=>new ChoicePeer(this);
    private sealed class ChoicePeer(ChoiceBox owner):ComboBoxAutomationPeer(owner),IValueProvider
    {
        public bool IsReadOnly=>!owner.IsEnabled;
        public string Value=>owner.SelectedItem?.ToString()??"";
        public void SetValue(string? value)
        {
            if(IsReadOnly)return;
            var item=owner.Items.OfType<object>().FirstOrDefault(item=>string.Equals(item.ToString(),value,StringComparison.OrdinalIgnoreCase)||(item is LocalModel model&&model.Name==value));
            if(item is not null)owner.SelectedItem=item;
        }
    }
}
