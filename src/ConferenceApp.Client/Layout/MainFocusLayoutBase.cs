using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ConferenceApp.Client.Layout;

/// <summary>
/// Gemeinsame Basis für PublicLayout und AdminLayout: Skip-Link-Ziel und Fokus nach Navigation.
/// Das Layout rendert den Skip-Link als erstes Element und <c>&lt;main id="inhalt" tabindex="-1" @ref="MainElement"&gt;</c>.
/// </summary>
public abstract class MainFocusLayoutBase : LayoutComponentBase, IDisposable
{
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected ElementReference MainElement;

    private string _path = "";
    private bool _focusMain;

    protected override void OnInitialized()
    {
        _path = CurrentPath();
        Navigation.LocationChanged += HandleLocationChanged;
    }

    /// <summary>Wird bei jeder Navigation aufgerufen (z. B. um einen temporären Drawer zu schließen). Rückgabe true = neu rendern.</summary>
    protected virtual bool OnNavigated() => false;

    // Nach einer Navigation den Fokus auf den Inhalt setzen – beim ersten Laden nicht,
    // damit der erste Tab-Stopp der Skip-Link bleibt. Reine Query-/Fragment-Wechsel ignorieren.
    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var render = OnNavigated();
        var path = CurrentPath();
        if (path != _path)
        {
            _path = path;
            _focusMain = true;
            render = true;
        }
        if (render) InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusMain) return;
        _focusMain = false;
        await MainElement.FocusAsync();
    }

    protected async Task SkipToContentAsync() => await MainElement.FocusAsync();

    private string CurrentPath() => Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0];

    public virtual void Dispose() => Navigation.LocationChanged -= HandleLocationChanged;
}
