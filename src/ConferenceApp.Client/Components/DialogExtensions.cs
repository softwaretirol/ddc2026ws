using ConferenceApp.Client.Services;
using MudBlazor;

namespace ConferenceApp.Client.Components;

public static class DialogExtensions
{
    private static readonly DialogOptions FormOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };

    /// <summary>Öffnet einen Formular-Dialog; true, wenn gespeichert wurde.</summary>
    public static async Task<bool> ShowFormAsync<TDialog>(this IDialogService dialogs, string title, DialogParameters<TDialog> parameters)
        where TDialog : Microsoft.AspNetCore.Components.ComponentBase
    {
        var dialog = await dialogs.ShowAsync<TDialog>(title, parameters, FormOptions);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>
    /// Fragt nach Bestätigung und führt danach die Löschaktion aus; Fehler landen in der Snackbar.
    /// Mit <paramref name="confirmName"/> muss dieser Name zur Bestätigung exakt eingetippt werden.
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(this IDialogService dialogs, ISnackbar snackbar, string text, Func<Task> delete, string? confirmName = null)
    {
        var parameters = new DialogParameters<ConfirmDialog> { { x => x.Text, text }, { x => x.ConfirmName, confirmName } };
        var options = confirmName is null
            ? new DialogOptions { MaxWidth = MaxWidth.ExtraSmall }
            : new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>("Löschen bestätigen", parameters, options);
        if (await dialog.Result is not { Canceled: false }) return false;

        try
        {
            await delete();
            snackbar.Add("Gelöscht.", Severity.Success);
            return true;
        }
        catch (ApiException ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            return false;
        }
    }
}
