using ConferenceApp.Client.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ConferenceApp.Client.Components;

public static class DialogExtensions
{
    // Schließen per X/Escape übernimmt FormDialog selbst (eigener X-Button, OnKeyDown, OnBackdropClick),
    // damit bei ungespeicherten Änderungen nachgefragt werden kann.
    private static readonly DialogOptions FormOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = false, CloseOnEscapeKey = false };

    /// <summary>Öffnet einen Formular-Dialog; true, wenn gespeichert wurde.</summary>
    public static async Task<bool> ShowFormAsync<TDialog>(this IDialogService dialogs, string title, DialogParameters<TDialog> parameters)
        where TDialog : Microsoft.AspNetCore.Components.ComponentBase
    {
        var dialog = await dialogs.ShowAsync<TDialog>(title, parameters, FormOptions);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>Rückfrage vor dem Verwerfen ungespeicherter Änderungen; true = verwerfen.</summary>
    public static async Task<bool> ConfirmDiscardAsync(this IDialogService dialogs)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Text, "Du hast Änderungen, die noch nicht gespeichert sind. Möchtest du sie verwerfen?" },
            { x => x.ConfirmText, "Verwerfen" },
            { x => x.CancelText, "Weiter bearbeiten" },
        };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>("Ungespeicherte Änderungen", parameters, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall });
        return await dialog.Result is { Canceled: false };
    }

    /// <summary>Erfolgsmeldung nach dem Speichern, optional mit Link „Ansehen“.</summary>
    public static void AddSaved(this ISnackbar snackbar, string text, NavigationManager? navigation = null, string? href = null)
    {
        snackbar.Add(text, Severity.Success, options =>
        {
            if (navigation is null || href is null) return;
            options.Action = "Ansehen";
            options.ActionColor = Color.Inherit;
            options.OnClick = _ =>
            {
                navigation.NavigateTo(href);
                return Task.CompletedTask;
            };
        });
    }

    /// <summary>Fragt nach Bestätigung und führt danach die Löschaktion aus; Fehler landen in der Snackbar.</summary>
    public static async Task<bool> ConfirmDeleteAsync(this IDialogService dialogs, ISnackbar snackbar, string text, Func<Task> delete)
    {
        var parameters = new DialogParameters<ConfirmDialog> { { x => x.Text, text } };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>("Löschen bestätigen", parameters, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall });
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
