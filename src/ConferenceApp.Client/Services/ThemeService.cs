using Microsoft.JSInterop;

namespace ConferenceApp.Client.Services;

public enum ThemeMode { System, Light, Dark }

/// <summary>
/// Farbschema (Wie System / Hell / Dunkel). App.razor liest daraus <see cref="IsDarkMode"/> für den MudThemeProvider,
/// der <c>ThemeToggle</c> in PublicLayout und AdminLayout setzt den Modus.
/// Systemmodus beobachtet js/theme.js selbst; bei manueller Wahl überschreibt ein Systemwechsel den Modus nicht.
/// </summary>
public sealed class ThemeService(IJSRuntime js) : IAsyncDisposable
{
    private bool _initialized;
    private bool _systemDark;
    private DotNetObjectReference<ThemeService>? _selfRef;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public bool IsDarkMode { get; private set; }

    /// <summary>Wird nach jeder Änderung von <see cref="Mode"/> oder <see cref="IsDarkMode"/> ausgelöst.</summary>
    public event Action? Changed;

    /// <summary>Gespeicherte Wahl und Systemmodus lesen – vor dem ersten Rendern, damit die App nicht erst hell aufblitzt.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            Mode = await js.InvokeAsync<string?>("conferenceTheme.getPreference") switch
            {
                "light" => ThemeMode.Light,
                "dark" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
            _systemDark = await js.InvokeAsync<bool>("conferenceTheme.systemPrefersDark");
        }
        catch (JSException) { /* theme.js nicht verfügbar → Systemmodus, hell */ }

        await ApplyAsync();
    }

    /// <summary>Systemwechsel beobachten (nach dem ersten Rendern aufrufen).</summary>
    public async Task WatchSystemAsync()
    {
        if (_selfRef is not null) return;
        _selfRef = DotNetObjectReference.Create(this);
        try { await js.InvokeVoidAsync("conferenceTheme.watchSystem", _selfRef); }
        catch (JSException) { }
    }

    // Aufruf aus theme.js, wenn der Systemmodus wechselt; wirkt nur bei „Wie System“.
    [JSInvokable]
    public async Task OnSystemDarkModeChanged(bool isDark)
    {
        _systemDark = isDark;
        await ApplyAsync();
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        try { await js.InvokeVoidAsync("conferenceTheme.setPreference", PreferenceValue(mode)); }
        catch (JSException) { }
        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        IsDarkMode = Mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => _systemDark,
        };
        try { await js.InvokeVoidAsync("conferenceTheme.applyDarkMode", IsDarkMode); }
        catch (JSException) { }
        Changed?.Invoke();
    }

    private static string? PreferenceValue(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => null,
    };

    public async ValueTask DisposeAsync()
    {
        try { await js.InvokeVoidAsync("conferenceTheme.unwatchSystem"); }
        catch (JSException) { }
        catch (JSDisconnectedException) { }
        _selfRef?.Dispose();
    }
}
